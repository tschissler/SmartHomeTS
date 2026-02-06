"""Core migration orchestration logic"""

import logging
from datetime import datetime, timedelta
from typing import Optional

from .config import MigrationConfig
from .connections import InfluxDB3Connection
from .state_manager import StateManager
from .validator import MigrationValidator
from .progress import ProgressTracker, DryRunReporter, setup_logging
from .utils import generate_time_chunks, chunk_dataframe, get_tag_columns


logger = logging.getLogger(__name__)


class InfluxDB3Migrator:
    """
    Main migration orchestrator.

    Coordinates:
    - Source and target connections
    - State management and checkpointing
    - Progress tracking
    - Validation
    - Incremental sync logic
    """

    def __init__(self, config: MigrationConfig):
        """
        Initialize migrator.

        Args:
            config: Migration configuration
        """
        self.config = config

        # Setup logging
        self.logger = setup_logging(
            level=config.logging.level,
            log_file=config.logging.file,
            console=config.logging.console
        )

        # Initialize components
        self.source = InfluxDB3Connection(config.source, name="source")
        self.target = InfluxDB3Connection(config.target, name="target")
        self.state = StateManager(config.state.checkpoint_db)
        self.validator = MigrationValidator(config)

        # Get tables to migrate
        self.tables = config.tables.get_tables_to_migrate()
        self.progress = ProgressTracker(len(self.tables), self.logger)

        # Dry run reporter
        self.dry_run_reporter = DryRunReporter(self.logger) if config.migration.dry_run else None

    def run(self) -> bool:
        """
        Execute migration.

        Returns:
            True if migration successful, False otherwise
        """
        try:
            self.logger.info("\n" + "═"*70)
            self.logger.info(f"  🚀 InfluxDB3 Migration Tool")
            self.logger.info("═"*70)
            self.logger.info(f"  Migration: {self.config.migration.name}")
            self.logger.info(f"  Mode:      {self.config.migration.mode}")
            if self.config.migration.dry_run:
                self.logger.info(f"  Type:      DRY RUN (no data will be written)")
            self.logger.info("═"*70)

            # Pre-flight validation
            if self.config.validation.pre_flight:
                if not self.validator.pre_flight_checks(self.source, self.target, self.tables):
                    self.logger.error("Pre-flight checks failed. Aborting migration.")
                    return False

            # Create migration run
            config_dict = {
                'name': self.config.migration.name,
                'mode': self.config.migration.mode,
                'tables': self.tables
            }
            run_id = self.state.create_run(config_dict, self.config.migration.dry_run)
            self.logger.info(f"\nMigration run ID: {run_id}\n")

            try:
                # Migrate each table
                for table in self.tables:
                    try:
                        self._migrate_table(run_id, table)
                    except Exception as e:
                        self.logger.error(f"Failed to migrate {table}: {e}", exc_info=True)
                        self.state.save_validation_result(
                            run_id, 'table_migration', 'fail',
                            table=table, details=str(e)
                        )
                        # Optionally continue with other tables
                        # For now, we'll raise to stop migration
                        raise

                # Mark run as complete
                self.state.complete_run(run_id, 'completed')

                # Print summary
                if self.config.migration.dry_run and self.dry_run_reporter:
                    self.dry_run_reporter.print_report()
                else:
                    self.logger.info(self.progress.get_summary())

                return True

            except Exception as e:
                self.state.complete_run(run_id, 'failed')
                raise

        except KeyboardInterrupt:
            self.logger.warning("\n\nMigration interrupted by user (Ctrl+C)")
            self.logger.info("Progress has been checkpointed. You can resume with the same configuration.")
            return False
        except Exception as e:
            self.logger.error(f"Migration failed: {e}", exc_info=True)
            return False
        finally:
            # Cleanup connections
            self.source.close()
            self.target.close()

    def _migrate_table(self, run_id: str, table: str) -> None:
        """
        Migrate a single table.

        Args:
            run_id: Migration run ID
            table: Table name
        """
        self.progress.start_table(table)

        # Determine time range
        start_time, end_time = self._get_migration_time_range(table)

        if start_time is None or end_time is None:
            self.progress.skip_table(table, "No data in source or invalid time range")
            return

        # Check if there's data to migrate
        if start_time >= end_time:
            self.progress.skip_table(table, f"No new data (checkpoint: {start_time.date()})")
            return

        # Calculate duration
        duration = end_time - start_time
        days = duration.days

        self.logger.info(f"  📅 Time range:  {start_time.date()} to {end_time.date()} ({days} days)")
        self.logger.info(f"  📦 Chunk size:  {self.config.batching.chunk_size_days} day(s)")
        self.logger.info(f"  📝 Batch size:  {self.config.batching.batch_size_rows:,} rows")

        # Dry run mode - just count and report
        if self.config.migration.dry_run:
            self._dry_run_table(table, start_time, end_time)
            self.progress.complete_table(0)
            return

        # Generate time chunks
        chunks = list(generate_time_chunks(
            start_time,
            end_time,
            self.config.batching.chunk_size_days
        ))

        self.logger.info(f"  🔄 Processing {len(chunks)} chunk(s)...")
        self.logger.info("")

        total_rows = 0

        # Process each chunk
        for i, (chunk_start, chunk_end) in enumerate(chunks, 1):
            chunk_rows = self._migrate_chunk(table, chunk_start, chunk_end)
            total_rows += chunk_rows

            # Checkpoint after each chunk
            if chunk_rows > 0:
                self.state.save_checkpoint(
                    run_id, table, chunk_end, total_rows, self.config.target.database
                )

            self.progress.update(total_rows, chunk_rows)

            # Show chunk progress
            if chunk_rows > 0:
                self.logger.info(
                    f"     Chunk {i}/{len(chunks)}: {chunk_rows:,} rows "
                    f"({chunk_start.date()} → {chunk_end.date()})"
                )

        # Post-migration validation
        if self.config.validation.post_migration and total_rows > 0:
            validation_passed = self.validator.post_migration_validation(
                self.source, self.target, table, (start_time, end_time), expected_rows=total_rows
            )

            self.state.save_validation_result(
                run_id,
                'post_migration',
                'pass' if validation_passed else 'fail',
                table=table
            )

            if not validation_passed:
                raise RuntimeError(f"Post-migration validation failed for {table}")

        self.progress.complete_table(total_rows)

    def _migrate_chunk(
        self,
        table: str,
        start_time: datetime,
        end_time: datetime
    ) -> int:
        """
        Migrate a single time chunk.

        Args:
            table: Table name
            start_time: Chunk start time
            end_time: Chunk end time

        Returns:
            Number of rows migrated
        """
        # Query source data with retries
        arrow_table = self.validator.retry.execute_with_retry(
            self._query_chunk,
            table,
            start_time,
            end_time
        )

        if arrow_table.num_rows == 0:
            return 0

        # Convert to DataFrame
        df = arrow_table.to_pandas()
        tag_columns = get_tag_columns(table)

        # Write to target in batches
        for batch_df in chunk_dataframe(df, self.config.batching.batch_size_rows):
            self.validator.retry.execute_with_retry(
                self.target.write_dataframe,
                batch_df,
                table,
                tag_columns
            )

        return arrow_table.num_rows

    def _query_chunk(
        self,
        table: str,
        start_time: datetime,
        end_time: datetime
    ):
        """
        Query a time chunk from source.

        Args:
            table: Table name
            start_time: Start time
            end_time: End time

        Returns:
            PyArrow Table with results
        """
        return self.source.query_time_range(
            table,
            start_time,
            end_time,
            tag_filters=self.config.filters.tags
        )

    def _get_migration_time_range(self, table: str) -> tuple:
        """
        Get time range to migrate for a table.

        Considers:
        - Configuration filters (highest priority)
        - Existing checkpoints (for incremental mode)
        - Source data availability

        Args:
            table: Table name

        Returns:
            Tuple of (start_time, end_time)
        """
        # Check if user has configured explicit time range
        config_start = self.config.filters.time_range.get_start_datetime()
        config_end = self.config.filters.time_range.get_end_datetime()

        # If user configured time range, use it (skip auto-detection)
        if config_start is not None or config_end is not None:
            # User wants specific time range - use it
            if config_start is None:
                # No start specified, get from source
                source_min, _ = self.source.get_table_time_range(table)
                if source_min is None:
                    return (None, None)
                start_time = source_min
            else:
                start_time = config_start

            if config_end is None:
                # No end specified, use now
                end_time = datetime.utcnow()
            else:
                end_time = config_end

            # For incremental mode, check if we have a more recent checkpoint
            if self.config.migration.mode == 'incremental':
                checkpoint = self.state.get_last_checkpoint(table, self.config.target.database)
                if checkpoint and checkpoint > start_time:
                    start_time = checkpoint
                    self.logger.info(f"  📍 Resuming from checkpoint: {start_time.date()}")

            return (start_time, end_time)

        # No configured time range - try to auto-detect
        source_min, source_max = self.source.get_table_time_range(table)

        if source_min is None or source_max is None:
            self.logger.warning(f"  No data found in source table: {table}")
            return (None, None)

        # Determine start time
        if self.config.migration.mode == 'incremental':
            # For incremental mode, use checkpoint if available
            checkpoint = self.state.get_last_checkpoint(table, self.config.target.database)
            if checkpoint:
                # Start from checkpoint (exclusive - we've already migrated up to this point)
                start_time = checkpoint
                self.logger.info(f"  📍 Resuming from checkpoint: {start_time.date()}")
            else:
                # No checkpoint, use source min
                start_time = source_min
        else:
            # Full mode - use source min
            start_time = source_min

        # Use source max as end time
        end_time = source_max

        return (start_time, end_time)

    def _dry_run_table(
        self,
        table: str,
        start_time: datetime,
        end_time: datetime
    ) -> None:
        """
        Perform dry run for a table (count only, no writes).

        Args:
            table: Table name
            start_time: Start time
            end_time: End time
        """
        try:
            row_count = self.source.count_rows(table, (start_time, end_time))
            self.logger.info(f"  Would migrate: {row_count:,} rows")

            if self.dry_run_reporter:
                self.dry_run_reporter.add_table_info(
                    table,
                    row_count,
                    (start_time, end_time)
                )

        except Exception as e:
            self.logger.error(f"  Failed to count rows: {e}")
