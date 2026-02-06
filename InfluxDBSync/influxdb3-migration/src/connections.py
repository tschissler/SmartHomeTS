"""InfluxDB3 connection wrapper for migration tool"""

from typing import Optional, Tuple, List
import logging
from datetime import datetime, timedelta
import pandas as pd

try:
    from influxdb_client_3 import InfluxDBClient3
except ImportError as e:
    raise ImportError(
        "influxdb3-python is required. Install with: pip install influxdb3-python>=0.7"
    ) from e

from .config import ConnectionConfig
from .utils import format_timestamp, escape_sql, get_tag_columns


logger = logging.getLogger(__name__)


class InfluxDB3Connection:
    """
    Wrapper around InfluxDBClient3 with helper methods for migration.

    Based on patterns from MCPServer/server.py
    """

    def __init__(self, config: ConnectionConfig, name: str = ""):
        """
        Initialize connection.

        Args:
            config: Connection configuration
            name: Optional name for logging (e.g., "source", "target")
        """
        self.config = config
        self.name = name
        self._client: Optional[InfluxDBClient3] = None

    @property
    def client(self) -> InfluxDBClient3:
        """
        Get InfluxDB3 client, creating it if needed.

        Returns:
            InfluxDBClient3 instance
        """
        if self._client is None:
            logger.debug(f"Creating InfluxDB3 client for {self.name}: {self.config.host}/{self.config.database}")
            self._client = InfluxDBClient3(
                host=self.config.host,
                token=self.config.token,
                database=self.config.database,
                enable_gzip=self.config.enable_gzip
            )
        return self._client

    def test_connection(self) -> bool:
        """
        Test database connectivity.

        Returns:
            True if connection successful, False otherwise
        """
        try:
            result = self.client.query(query="SELECT 1", language="sql")
            # Read result to ensure query actually executes
            _ = self._as_table(result)
            logger.info(f"✓ Connection test successful for {self.name}")
            return True
        except Exception as e:
            logger.error(f"✗ Connection test failed for {self.name}: {e}")
            return False

    def query_arrow(self, query: str):
        """
        Execute SQL query and return Arrow table.

        Args:
            query: SQL query string

        Returns:
            PyArrow Table with query results
        """
        logger.debug(f"Executing query on {self.name}: {query[:100]}...")
        result = self.client.query(query=query, language="sql")
        return self._as_table(result)

    def query_dataframe(self, query: str) -> pd.DataFrame:
        """
        Execute SQL query and return pandas DataFrame.

        Args:
            query: SQL query string

        Returns:
            Pandas DataFrame with query results
        """
        arrow_table = self.query_arrow(query)
        return arrow_table.to_pandas()

    def count_rows(self, table: str, time_range: Tuple[datetime, datetime]) -> int:
        """
        Count rows in table for time range.

        Args:
            table: Table name
            time_range: Tuple of (start_time, end_time)

        Returns:
            Number of rows
        """
        start_time, end_time = time_range
        query = f"""
            SELECT COUNT(*) as count FROM {table}
            WHERE time >= '{format_timestamp(start_time)}'
              AND time < '{format_timestamp(end_time)}'
        """
        result = self.query_dataframe(query)
        if len(result) == 0:
            return 0
        return int(result.iloc[0]['count'])

    def query_time_range(
        self,
        table: str,
        start_time: datetime,
        end_time: datetime,
        tag_filters: Optional[dict] = None
    ):
        """
        Query data for a time range with optional tag filtering.

        Args:
            table: Table name
            start_time: Start of time range
            end_time: End of time range
            tag_filters: Optional dict of tag name -> value filters

        Returns:
            PyArrow Table with results
        """
        # Build WHERE clause
        conditions = [
            f"time >= '{format_timestamp(start_time)}'",
            f"time < '{format_timestamp(end_time)}'"
        ]

        # Add tag filters
        if tag_filters:
            for tag_name, tag_value in tag_filters.items():
                conditions.append(f"{tag_name} = '{escape_sql(tag_value)}'")

        where_clause = " AND ".join(conditions)

        query = f"""
            SELECT * FROM {table}
            WHERE {where_clause}
            ORDER BY time
        """

        return self.query_arrow(query)

    def write_dataframe(
        self,
        df: pd.DataFrame,
        measurement_name: str,
        tag_columns: List[str]
    ) -> None:
        """
        Write pandas DataFrame to InfluxDB3.

        Args:
            df: DataFrame to write
            measurement_name: Target measurement/table name
            tag_columns: List of column names that are tags (vs fields)
        """
        if len(df) == 0:
            logger.debug(f"Skipping write to {self.name} - empty DataFrame")
            return

        logger.debug(f"Writing {len(df)} rows to {self.name}/{measurement_name}")

        try:
            self.client.write(
                record=df,
                data_frame_measurement_name=measurement_name,
                data_frame_timestamp_column="time",
                data_frame_tag_columns=tag_columns
            )
            logger.debug(f"Successfully wrote {len(df)} rows to {measurement_name}")
        except Exception as e:
            logger.error(f"Failed to write {len(df)} rows to {measurement_name}: {e}")
            raise

    def table_exists(self, table: str) -> bool:
        """
        Check if table exists in database.

        Args:
            table: Table name to check

        Returns:
            True if table exists, False otherwise
        """
        try:
            query = f"""
                SELECT table_name
                FROM information_schema.tables
                WHERE table_name = '{escape_sql(table)}'
            """
            result = self.query_dataframe(query)
            exists = len(result) > 0
            if exists:
                logger.debug(f"Table '{table}' exists in {self.name}")
            else:
                logger.warning(f"Table '{table}' does not exist in {self.name}")
            return exists
        except Exception as e:
            logger.error(f"Failed to check if table '{table}' exists: {e}")
            return False

    def get_table_time_range(self, table: str) -> Tuple[Optional[datetime], Optional[datetime]]:
        """
        Get the earliest and latest timestamps in a table.

        For tables with many files (InfluxDB3 Core limit), uses heuristic approach.

        Args:
            table: Table name

        Returns:
            Tuple of (min_time, max_time), or ('heuristic', max_time) for large tables
        """
        # First try: Query with a recent time range to check if table has data
        # This avoids the file limit for initial check
        try:
            test_start = datetime.utcnow() - timedelta(days=7)
            test_query = f"""
                SELECT time FROM {table}
                WHERE time >= '{test_start.isoformat()}'
                LIMIT 1
            """
            test_result = self.query_dataframe(test_query)

            if len(test_result) > 0:
                # Table has recent data - use heuristic for full range
                logger.info(f"  ✓ Table has data (using heuristic range to avoid file limits)")
                end_time = datetime.utcnow()
                start_time = end_time - timedelta(days=730)  # 2 years default
                logger.info(f"  📅 Range: {start_time.date()} to {end_time.date()} (heuristic)")
                logger.info(f"  💡 Use filters.time_range in config to override")
                # Return a special marker for heuristic mode
                return (start_time, end_time)

        except Exception as e:
            error_msg = str(e).lower()
            if 'file limit' in error_msg or 'parquet files' in error_msg:
                # Even recent query hits limit - use heuristic
                logger.warning(f"  ⚠️  File scan limit - using heuristic time range")
                end_time = datetime.utcnow()
                start_time = end_time - timedelta(days=730)  # 2 years
                logger.info(f"  📅 Range: {start_time.date()} to {end_time.date()} (heuristic)")
                logger.info(f"  💡 Adjust with filters.time_range in config if needed")
                return (start_time, end_time)

        # Try full query (for small tables)
        try:
            query = f"""
                SELECT
                    MIN(time) as min_time,
                    MAX(time) as max_time
                FROM {table}
            """
            result = self.query_dataframe(query)
            if len(result) == 0 or pd.isna(result.iloc[0]['min_time']):
                return (None, None)

            min_time = pd.to_datetime(result.iloc[0]['min_time'])
            max_time = pd.to_datetime(result.iloc[0]['max_time'])
            logger.info(f"  📅 Range: {min_time.date()} to {max_time.date()} (detected)")
            return (min_time, max_time)

        except Exception as e:
            error_msg = str(e).lower()

            if 'file limit' in error_msg or 'parquet files' in error_msg:
                # Use heuristic
                logger.warning(f"  ⚠️  File scan limit - using heuristic time range")
                end_time = datetime.utcnow()
                start_time = end_time - timedelta(days=730)  # 2 years
                logger.info(f"  📅 Range: {start_time.date()} to {end_time.date()} (heuristic)")
                logger.info(f"  💡 Adjust with filters.time_range in config if needed")
                return (start_time, end_time)
            else:
                logger.error(f"  ✗ Failed to get time range: {e}")
                return (None, None)

    def close(self) -> None:
        """Close the client connection."""
        if self._client is not None:
            try:
                self._client.close()
                logger.debug(f"Closed connection to {self.name}")
            except Exception as e:
                logger.warning(f"Error closing connection to {self.name}: {e}")
            finally:
                self._client = None

    @staticmethod
    def _as_table(obj):
        """
        Normalize query result to Arrow Table.

        influxdb3-python may return a Reader with .read_all() or directly a Table.
        This helper makes it consistent.
        """
        if hasattr(obj, "read_all") and callable(getattr(obj, "read_all")):
            try:
                return obj.read_all()
            except Exception:
                return obj
        return obj

    def __enter__(self):
        """Context manager entry."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()
        return False

    def __repr__(self) -> str:
        return f"InfluxDB3Connection(name='{self.name}', host='{self.config.host}', database='{self.config.database}')"
