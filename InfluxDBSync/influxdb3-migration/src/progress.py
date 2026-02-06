"""Progress tracking and logging configuration"""

import logging
import logging.handlers
from datetime import datetime
from typing import Optional
import sys


class ProgressTracker:
    """
    Track and report migration progress.

    Provides:
    - Per-table progress tracking
    - Overall migration progress
    - Rate calculation (rows/sec)
    - Elapsed time tracking
    """

    def __init__(self, total_tables: int, logger: logging.Logger):
        """
        Initialize progress tracker.

        Args:
            total_tables: Total number of tables to migrate
            logger: Logger instance for output
        """
        self.total_tables = total_tables
        self.completed_tables = 0
        self.current_table: Optional[str] = None
        self.current_table_rows = 0
        self.total_rows = 0
        self.start_time = datetime.utcnow()
        self.table_start_time: Optional[datetime] = None
        self.logger = logger

    def start_table(self, table: str) -> None:
        """
        Mark start of table migration.

        Args:
            table: Table name
        """
        self.current_table = table
        self.current_table_rows = 0
        self.table_start_time = datetime.utcnow()
        self.logger.info("")
        self.logger.info("─" * 70)
        self.logger.info(f"  📊 Table {self.completed_tables + 1}/{self.total_tables}: {table}")
        self.logger.info("─" * 70)

    def update(self, rows_processed: int, chunk_rows: Optional[int] = None) -> None:
        """
        Update progress for current table.

        Args:
            rows_processed: Total rows processed for this table
            chunk_rows: Rows in current chunk (for rate calculation)
        """
        if self.current_table is None:
            return

        self.current_table_rows = rows_processed
        self.total_rows += chunk_rows if chunk_rows else 0

        # Calculate rates
        elapsed = (datetime.utcnow() - self.start_time).total_seconds()
        overall_rate = self.total_rows / elapsed if elapsed > 0 else 0

        table_elapsed = (datetime.utcnow() - self.table_start_time).total_seconds() if self.table_start_time else 0
        table_rate = rows_processed / table_elapsed if table_elapsed > 0 else 0

        # Log progress
        self.logger.info(
            f"  {self.current_table}: {rows_processed:,} rows "
            f"({table_rate:.0f} rows/sec, overall: {overall_rate:.0f} rows/sec)"
        )

    def complete_table(self, final_rows: int) -> None:
        """
        Mark completion of current table.

        Args:
            final_rows: Final row count for table
        """
        if self.current_table is None:
            return

        table_elapsed = (datetime.utcnow() - self.table_start_time).total_seconds() if self.table_start_time else 0
        table_rate = final_rows / table_elapsed if table_elapsed > 0 else 0

        self.logger.info("")
        self.logger.info(f"  ✓ Completed: {final_rows:,} rows in {table_elapsed:.1f}s ({table_rate:.0f} rows/sec)")
        self.completed_tables += 1
        self.current_table = None
        self.table_start_time = None

    def skip_table(self, table: str, reason: str) -> None:
        """
        Mark table as skipped.

        Args:
            table: Table name
            reason: Reason for skipping
        """
        self.logger.info("")
        self.logger.info(f"  ⊘ Skipped: {reason}")
        self.completed_tables += 1

    def get_summary(self) -> str:
        """
        Get migration summary.

        Returns:
            Summary string
        """
        elapsed = (datetime.utcnow() - self.start_time).total_seconds()
        overall_rate = self.total_rows / elapsed if elapsed > 0 else 0

        hours, remainder = divmod(int(elapsed), 3600)
        minutes, seconds = divmod(remainder, 60)

        time_str = ""
        if hours > 0:
            time_str = f"{hours}h {minutes}m {seconds}s"
        elif minutes > 0:
            time_str = f"{minutes}m {seconds}s"
        else:
            time_str = f"{seconds}s"

        return (
            f"\n\n{'═'*70}\n"
            f"  ✅ MIGRATION COMPLETE\n"
            f"{'═'*70}\n\n"
            f"  📊 Tables migrated:  {self.completed_tables}/{self.total_tables}\n"
            f"  📝 Total rows:       {self.total_rows:,}\n"
            f"  ⏱️  Duration:         {time_str}\n"
            f"  ⚡ Average rate:     {overall_rate:,.0f} rows/sec\n\n"
            f"{'═'*70}\n"
        )


def setup_logging(level: str, log_file: Optional[str] = None, console: bool = True) -> logging.Logger:
    """
    Setup logging configuration.

    Args:
        level: Log level (DEBUG, INFO, WARNING, ERROR)
        log_file: Optional log file path
        console: Whether to log to console

    Returns:
        Configured logger instance
    """
    # Get root logger for the application
    logger = logging.getLogger("influxdb3_migration")
    logger.setLevel(getattr(logging, level.upper()))

    # Clear existing handlers
    logger.handlers.clear()

    # Create formatter
    formatter = logging.Formatter(
        fmt='%(asctime)s [%(levelname)-8s] %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )

    # Console handler
    if console:
        console_handler = logging.StreamHandler(sys.stdout)
        console_handler.setLevel(getattr(logging, level.upper()))
        console_handler.setFormatter(formatter)
        logger.addHandler(console_handler)

    # File handler
    if log_file:
        try:
            file_handler = logging.handlers.RotatingFileHandler(
                log_file,
                maxBytes=10 * 1024 * 1024,  # 10MB
                backupCount=5,
                encoding='utf-8'
            )
            file_handler.setLevel(getattr(logging, level.upper()))
            file_handler.setFormatter(formatter)
            logger.addHandler(file_handler)
            logger.debug(f"Logging to file: {log_file}")
        except Exception as e:
            logger.warning(f"Failed to setup file logging: {e}")

    # Prevent propagation to root logger
    logger.propagate = False

    return logger


class DryRunReporter:
    """Report dry-run results without actual migration."""

    def __init__(self, logger: logging.Logger):
        """
        Initialize dry-run reporter.

        Args:
            logger: Logger instance
        """
        self.logger = logger
        self.tables_info = []

    def add_table_info(
        self,
        table: str,
        row_count: int,
        time_range: tuple,
        size_estimate_mb: Optional[float] = None
    ) -> None:
        """
        Add table information for dry-run report.

        Args:
            table: Table name
            row_count: Number of rows to migrate
            time_range: Tuple of (start_time, end_time)
            size_estimate_mb: Estimated size in MB
        """
        self.tables_info.append({
            'table': table,
            'rows': row_count,
            'time_range': time_range,
            'size_mb': size_estimate_mb
        })

    def print_report(self) -> None:
        """Print dry-run summary report."""
        total_rows = sum(t['rows'] for t in self.tables_info)
        total_size = sum(t['size_mb'] for t in self.tables_info if t['size_mb'])

        self.logger.info("\n" + "="*60)
        self.logger.info("DRY RUN REPORT")
        self.logger.info("="*60)
        self.logger.info("This is a dry run - NO data will be written to target\n")

        for info in self.tables_info:
            start, end = info['time_range']
            time_str = f"{start.strftime('%Y-%m-%d')} to {end.strftime('%Y-%m-%d')}" if start and end else "N/A"

            self.logger.info(f"Table: {info['table']}")
            self.logger.info(f"  Rows to migrate: {info['rows']:,}")
            self.logger.info(f"  Time range: {time_str}")
            if info['size_mb']:
                self.logger.info(f"  Est. size: {info['size_mb']:.1f} MB")
            self.logger.info("")

        self.logger.info(f"Total rows: {total_rows:,}")
        if total_size:
            self.logger.info(f"Total est. size: {total_size:.1f} MB")

        # Estimate duration (rough estimate: 10k rows/sec)
        est_seconds = total_rows / 10000
        if est_seconds > 60:
            est_minutes = est_seconds / 60
            if est_minutes > 60:
                est_hours = est_minutes / 60
                self.logger.info(f"Est. duration: ~{est_hours:.1f} hours")
            else:
                self.logger.info(f"Est. duration: ~{est_minutes:.1f} minutes")
        else:
            self.logger.info(f"Est. duration: ~{est_seconds:.0f} seconds")

        self.logger.info("\n" + "="*60)
        self.logger.info("To execute migration, set 'dry_run: false' in config")
        self.logger.info("="*60 + "\n")
