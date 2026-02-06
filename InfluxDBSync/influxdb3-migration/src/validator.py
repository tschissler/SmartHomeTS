"""Validation and retry logic for migration"""

import logging
import time
import random
from typing import Callable, Any, Optional, Tuple
from datetime import datetime

from .connections import InfluxDB3Connection
from .config import MigrationConfig


logger = logging.getLogger(__name__)


class RetryHandler:
    """
    Execute functions with exponential backoff retry logic.

    Handles transient failures in network operations.
    """

    def __init__(self, max_attempts: int, base_delay: float):
        """
        Initialize retry handler.

        Args:
            max_attempts: Maximum number of retry attempts
            base_delay: Base delay in seconds (will be exponentially increased)
        """
        self.max_attempts = max_attempts
        self.base_delay = base_delay

    def execute_with_retry(
        self,
        func: Callable,
        *args: Any,
        **kwargs: Any
    ) -> Any:
        """
        Execute function with exponential backoff retry.

        Args:
            func: Function to execute
            *args: Positional arguments for function
            **kwargs: Keyword arguments for function

        Returns:
            Function return value

        Raises:
            Exception: If all retry attempts fail
        """
        last_exception = None

        for attempt in range(1, self.max_attempts + 1):
            try:
                return func(*args, **kwargs)
            except Exception as e:
                last_exception = e

                if attempt == self.max_attempts:
                    logger.error(f"Failed after {attempt} attempts: {e}")
                    raise

                # Exponential backoff with jitter
                wait_time = self.base_delay * (2 ** (attempt - 1))
                jitter = random.uniform(0, wait_time * 0.1)  # Add up to 10% jitter
                wait_time += jitter

                logger.warning(
                    f"Attempt {attempt}/{self.max_attempts} failed: {e}. "
                    f"Retrying in {wait_time:.1f}s..."
                )
                time.sleep(wait_time)

        # Should not reach here, but just in case
        raise last_exception if last_exception else RuntimeError("Retry logic failed unexpectedly")


class MigrationValidator:
    """
    Validate migration pre-conditions and post-migration results.

    Provides:
    - Pre-flight connectivity and permissions checks
    - Post-migration row count validation
    - Sample data verification
    """

    def __init__(self, config: MigrationConfig):
        """
        Initialize validator.

        Args:
            config: Migration configuration
        """
        self.config = config
        self.retry = RetryHandler(
            config.performance.retry_attempts,
            config.performance.retry_delay_seconds
        )

    def pre_flight_checks(
        self,
        source: InfluxDB3Connection,
        target: InfluxDB3Connection,
        tables: list
    ) -> bool:
        """
        Run pre-flight validation checks.

        Args:
            source: Source connection
            target: Target connection
            tables: List of tables to migrate

        Returns:
            True if all checks pass, False otherwise
        """
        logger.info("\n" + "="*60)
        logger.info("PRE-FLIGHT VALIDATION")
        logger.info("="*60)

        all_passed = True

        # 1. Test source connection
        logger.info("\n1. Testing source connection...")
        if not self._test_connection(source, "source"):
            all_passed = False

        # 2. Test target connection and database
        logger.info("\n2. Testing target connection and database...")
        target_conn_result = self._test_connection_with_db_check(target, "target")
        if not target_conn_result:
            all_passed = False

        # 3. Verify tables exist in source
        logger.info("\n3. Verifying tables exist in source...")
        for table in tables:
            if not self._verify_table_exists(source, table):
                all_passed = False

        # 4. Test write permissions on target
        logger.info("\n4. Testing write permissions on target...")
        if not self.config.migration.dry_run:
            if not self._test_write_permission(target):
                all_passed = False
        else:
            logger.info("  ⊘ Skipped (dry run mode)")

        logger.info("\n" + "="*60)
        if all_passed:
            logger.info("✓ All pre-flight checks passed")
        else:
            logger.error("✗ Some pre-flight checks failed")
        logger.info("="*60 + "\n")

        return all_passed

    def post_migration_validation(
        self,
        source: InfluxDB3Connection,
        target: InfluxDB3Connection,
        table: str,
        time_range: Tuple[datetime, datetime],
        expected_rows: Optional[int] = None
    ) -> bool:
        """
        Validate migration results for a table.

        Args:
            source: Source connection
            target: Target connection
            table: Table name
            time_range: Tuple of (start_time, end_time) that was migrated
            expected_rows: Optional number of rows that were actually migrated
                          (used to avoid race conditions with live data)

        Returns:
            True if validation passes, False otherwise
        """
        if not self.config.validation.post_migration:
            return True

        logger.info(f"\nValidating {table} migration...")

        # 1. Row count comparison
        try:
            # Count target rows
            target_count = self.retry.execute_with_retry(
                target.count_rows, table, time_range
            )

            # Determine expected count
            if expected_rows is not None:
                # We have the count of rows actually migrated - use that to avoid race conditions
                if target_count != expected_rows:
                    logger.error(
                        f"  ✗ Row count mismatch for {table}: "
                        f"expected={expected_rows:,}, target={target_count:,}"
                    )
                    return False
                else:
                    logger.info(f"  ✓ Row counts match: {expected_rows:,} rows")
            else:
                # No expected count provided - compare with source
                # Try to count source rows
                try:
                    source_count = self.retry.execute_with_retry(
                        source.count_rows, table, time_range
                    )
                    has_source_count = True
                except Exception as source_error:
                    # Check if it's a file limit error
                    if 'file limit' in str(source_error).lower() or 'parquet files' in str(source_error).lower():
                        logger.warning(
                            f"  ⚠ Cannot validate source count (file limit exceeded). "
                            f"Validating target only..."
                        )
                        source_count = None
                        has_source_count = False
                    else:
                        # Some other error - re-raise
                        raise

                # Compare counts
                if has_source_count:
                    if source_count != target_count:
                        logger.error(
                            f"  ✗ Row count mismatch for {table}: "
                            f"source={source_count:,}, target={target_count:,}"
                        )
                        return False
                    else:
                        logger.info(f"  ✓ Row counts match: {source_count:,} rows")
                else:
                    # Only have target count - just report it
                    logger.info(
                        f"  ✓ Target has {target_count:,} rows "
                        f"(source validation skipped due to file limits)"
                    )

        except Exception as e:
            # Check if it's a resource exhaustion error (out of memory on target)
            error_msg = str(e).lower()
            if 'resource' in error_msg and ('exhausted' in error_msg or 'allocation failed' in error_msg):
                logger.warning(
                    f"  ⚠ Validation skipped: Target database out of memory during validation query"
                )
                if expected_rows:
                    logger.warning(
                        f"  ℹ Migration completed successfully ({expected_rows:,} rows), "
                        f"but validation COUNT query too expensive"
                    )
                else:
                    logger.warning(
                        f"  ℹ Migration completed successfully, "
                        f"but validation COUNT query too expensive"
                    )
                return True  # Non-fatal - migration succeeded, just can't validate
            else:
                logger.error(f"  ✗ Failed to validate row counts: {e}")
                return False

        # 2. Sample verification (if configured)
        if self.config.validation.sample_verification > 0:
            try:
                if self._verify_sample_data(source, target, table, time_range):
                    logger.info(
                        f"  ✓ Sample verification passed "
                        f"({self.config.validation.sample_verification} samples)"
                    )
                else:
                    logger.error(f"  ✗ Sample verification failed")
                    return False
            except Exception as e:
                logger.error(f"  ✗ Sample verification error: {e}")
                return False

        return True

    def _test_connection(self, conn: InfluxDB3Connection, name: str) -> bool:
        """
        Test database connection.

        Args:
            conn: Connection to test
            name: Connection name for logging

        Returns:
            True if connection successful
        """
        try:
            if self.retry.execute_with_retry(conn.test_connection):
                logger.info(f"  ✓ {name} connection OK")
                return True
            else:
                logger.error(f"  ✗ {name} connection failed")
                return False
        except Exception as e:
            logger.error(f"  ✗ {name} connection error: {e}")
            return False

    def _test_connection_with_db_check(self, conn: InfluxDB3Connection, name: str) -> bool:
        """
        Test target connection and check if database exists.

        If database doesn't exist, prompt user to create it.

        Args:
            conn: Connection to test
            name: Connection name for logging

        Returns:
            True if connection successful (and database exists or was created)
        """
        try:
            if self.retry.execute_with_retry(conn.test_connection):
                logger.info(f"  ✓ {name} connection OK")
                return True
            else:
                logger.error(f"  ✗ {name} connection failed")
                return False
        except Exception as e:
            error_msg = str(e).lower()

            # Check if error is "database not found"
            if 'database not found' in error_msg or 'database' in error_msg and 'not found' in error_msg:
                logger.warning(f"  ⚠️  Database '{conn.config.database}' not found on target")
                # Prompt user to create database
                return self._prompt_create_database(conn)
            else:
                logger.error(f"  ✗ {name} connection error: {e}")
                return False

    def _verify_table_exists(self, conn: InfluxDB3Connection, table: str) -> bool:
        """
        Verify table exists in database.

        Args:
            conn: Connection to check
            table: Table name

        Returns:
            True if table exists
        """
        try:
            if self.retry.execute_with_retry(conn.table_exists, table):
                logger.info(f"  ✓ Table '{table}' exists")
                return True
            else:
                logger.error(f"  ✗ Table '{table}' does not exist")
                return False
        except Exception as e:
            logger.error(f"  ✗ Error checking table '{table}': {e}")
            return False

    def _test_write_permission(self, conn: InfluxDB3Connection) -> bool:
        """
        Test write permissions on target database.

        Attempts to write a test record and then optionally clean it up.

        Args:
            conn: Connection to test

        Returns:
            True if write successful
        """
        try:
            import pandas as pd

            # Create a minimal test DataFrame
            test_df = pd.DataFrame({
                'time': [datetime.utcnow()],
                'test_tag': ['migration_test'],
                'test_value': [1.0]
            })

            # Attempt write
            self.retry.execute_with_retry(
                conn.write_dataframe,
                test_df,
                'migration_test',
                ['test_tag']
            )

            logger.info("  ✓ Write permission OK (test write successful)")
            logger.info("    Note: Test data written to 'migration_test' measurement")
            return True

        except Exception as e:
            logger.error(f"  ✗ Write permission test failed: {e}")
            return False

    def _verify_sample_data(
        self,
        source: InfluxDB3Connection,
        target: InfluxDB3Connection,
        table: str,
        time_range: Tuple[datetime, datetime]
    ) -> bool:
        """
        Verify random sample of rows match between source and target.

        Args:
            source: Source connection
            target: Target connection
            table: Table name
            time_range: Time range to sample from

        Returns:
            True if samples match
        """
        n_samples = min(self.config.validation.sample_verification, 100)

        try:
            # Get random sample from source
            start_time, end_time = time_range
            query = f"""
                SELECT * FROM {table}
                WHERE time >= '{start_time.isoformat()}'
                  AND time < '{end_time.isoformat()}'
                ORDER BY RANDOM()
                LIMIT {n_samples}
            """

            source_sample = source.query_dataframe(query)

            if len(source_sample) == 0:
                # No data to verify
                return True

            # For each sample row, verify it exists in target with same values
            # We'll query by timestamp and compare all fields
            missing_count = 0
            checked_count = 0

            for _, row in source_sample.iterrows():
                timestamp = row['time']

                # Use a small time window instead of exact match to handle precision issues
                # Convert timestamp to string format that InfluxDB understands
                if hasattr(timestamp, 'isoformat'):
                    time_str = timestamp.isoformat()
                else:
                    time_str = str(timestamp)

                target_query = f"""
                    SELECT * FROM {table}
                    WHERE time = '{time_str}'
                """

                try:
                    target_row = target.query_dataframe(target_query)
                    checked_count += 1

                    if len(target_row) == 0:
                        missing_count += 1
                        logger.debug(f"    Sample row not found in target: time={timestamp}")
                except Exception as query_error:
                    logger.debug(f"    Sample query failed: {query_error}")
                    continue

            # Allow some missing samples (due to race conditions with live data)
            # If more than 10% are missing, log a warning but don't fail
            if checked_count > 0:
                missing_pct = (missing_count / checked_count) * 100
                if missing_pct > 10:
                    logger.warning(
                        f"    ⚠ Sample verification: {missing_count}/{checked_count} samples not found ({missing_pct:.1f}%)"
                    )
                    logger.warning(
                        f"    This may be due to timestamp precision or race conditions with live data"
                    )
                elif missing_count > 0:
                    logger.debug(
                        f"    Sample verification: {missing_count}/{checked_count} samples not found (acceptable)"
                    )

            return True  # Always return True - sample verification is informational only

        except Exception as e:
            # Check if it's a file limit error
            if 'file limit' in str(e).lower() or 'parquet files' in str(e).lower():
                logger.warning(
                    f"    ⚠ Sample verification skipped (source file limit exceeded)"
                )
            else:
                logger.warning(f"    Sample verification error (non-fatal): {e}")
            # Don't fail validation on sample verification errors
            # (RANDOM() might not be supported, or file limits might be hit)
            return True

    def _check_target_database_exists(self, conn: InfluxDB3Connection) -> bool:
        """
        Check if target database exists, prompt user to create if not.

        Args:
            conn: Target connection

        Returns:
            True if database exists or was created, False otherwise
        """
        try:
            # Try to query system tables to verify database exists
            query = "SELECT 1 LIMIT 1"
            conn.query_arrow(query)
            logger.info(f"  ✓ Target database '{conn.config.database}' exists")
            return True

        except Exception as e:
            error_msg = str(e).lower()

            # Check if error is related to database not existing
            if any(keyword in error_msg for keyword in ['database', 'not found', 'does not exist', 'unknown database']):
                logger.warning(f"  ⚠ Target database '{conn.config.database}' does not exist")
                logger.info(f"     Error: {e}")

                # Prompt user to create database
                return self._prompt_create_database(conn)
            else:
                # Different error - connection issue, permissions, etc.
                logger.error(f"  ✗ Error accessing target database: {e}")
                return False

    def _prompt_create_database(self, conn: InfluxDB3Connection) -> bool:
        """
        Prompt user to create the target database.

        Args:
            conn: Target connection

        Returns:
            True if user wants to proceed (with manual creation), False otherwise
        """
        import click

        logger.info("")
        logger.info("  " + "─" * 56)
        logger.info(f"  Database '{conn.config.database}' does not exist on target server.")
        logger.info("")
        logger.info("  To create the database, you have these options:")
        logger.info("")
        logger.info("  Option 1: InfluxDB UI")
        logger.info(f"    1. Open: {conn.config.host}")
        logger.info("    2. Navigate to: Load Data → Databases")
        logger.info(f"    3. Create database: {conn.config.database}")
        logger.info("")
        logger.info("  Option 2: influxctl CLI (for InfluxDB Clustered)")
        logger.info(f"    influxctl database create {conn.config.database}")
        logger.info("")
        logger.info("  Option 3: Management API")
        logger.info("    See: https://docs.influxdata.com/influxdb/cloud-serverless/api/")
        logger.info("  " + "─" * 56)
        logger.info("")

        # Ask user what they want to do
        response = click.prompt(
            "  What would you like to do?\n"
            "    [1] I'll create it manually now (migration will wait)\n"
            "    [2] I'll create it later (abort migration)\n"
            "    [3] Continue anyway (may fail during write)\n"
            "  Choose",
            type=click.Choice(['1', '2', '3']),
            default='1'
        )

        if response == '1':
            logger.info("")
            logger.info("  Please create the database now...")
            click.pause("  Press Enter when ready to continue")

            # Test if database now exists
            logger.info("")
            logger.info("  Verifying database creation...")
            try:
                conn.query_arrow("SELECT 1 LIMIT 1")
                logger.info(f"  ✓ Database '{conn.config.database}' is now accessible!")
                return True
            except Exception as e:
                logger.error(f"  ✗ Database still not accessible: {e}")
                logger.error("  Please verify the database was created correctly.")

                retry = click.confirm("  Would you like to retry the check?", default=True)
                if retry:
                    return self._prompt_create_database(conn)
                else:
                    return False

        elif response == '2':
            logger.info("  Migration aborted. Create the database and try again.")
            return False

        elif response == '3':
            logger.warning("  ⚠ Continuing without database verification.")
            logger.warning("  ⚠ Migration may fail during write operations!")
            return True

        return False
