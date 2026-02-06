"""Utility functions and constants for InfluxDB3 migration"""

from datetime import datetime, timedelta
from typing import Iterator, Tuple
import pandas as pd

# Table schemas from SmartHome.DataHub/Influx3Connector/InfluxDB3Connector.cs
TABLE_SCHEMAS = {
    "temperature_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_temp"],
    },
    "power_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_watt"],
    },
    "energy_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_delta_kwh", "value_cumulated_kwh"],
    },
    "voltage_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_volt"],
    },
    "percent_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_percent"],
    },
    "status_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_status"],
    },
    "counter_values": {
        "tags": ["measurement_id", "category", "sub_category", "sensor_type",
                 "location", "device", "measurement"],
        "fields": ["value_counter"],
    }
}


def generate_time_chunks(
    start_time: datetime,
    end_time: datetime,
    chunk_days: int
) -> Iterator[Tuple[datetime, datetime]]:
    """
    Generate time windows for incremental processing.

    Args:
        start_time: Start of the time range
        end_time: End of the time range
        chunk_days: Size of each chunk in days

    Yields:
        Tuples of (chunk_start, chunk_end) datetime objects

    Example:
        >>> list(generate_time_chunks(
        ...     datetime(2024, 1, 1),
        ...     datetime(2024, 1, 3),
        ...     1
        ... ))
        [(2024-01-01, 2024-01-02), (2024-01-02, 2024-01-03)]
    """
    current = start_time
    while current < end_time:
        chunk_end = min(current + timedelta(days=chunk_days), end_time)
        yield (current, chunk_end)
        current = chunk_end


def chunk_dataframe(df: pd.DataFrame, chunk_size: int) -> Iterator[pd.DataFrame]:
    """
    Split DataFrame into smaller chunks.

    Args:
        df: DataFrame to split
        chunk_size: Number of rows per chunk

    Yields:
        DataFrame chunks of size chunk_size (last chunk may be smaller)

    Example:
        >>> df = pd.DataFrame({'a': range(100)})
        >>> chunks = list(chunk_dataframe(df, 30))
        >>> len(chunks)
        4
        >>> len(chunks[0])
        30
        >>> len(chunks[-1])
        10
    """
    for start_idx in range(0, len(df), chunk_size):
        yield df.iloc[start_idx:start_idx + chunk_size]


def escape_sql(value: str) -> str:
    """
    Escape SQL string value to prevent injection.

    Args:
        value: String value to escape

    Returns:
        Escaped string safe for SQL queries

    Note:
        This is a basic implementation. For production use,
        consider using parameterized queries when available.
    """
    if value is None:
        return "NULL"

    # Escape single quotes by doubling them
    return value.replace("'", "''")


def format_timestamp(dt: datetime) -> str:
    """
    Format datetime for InfluxDB3 SQL queries.

    Args:
        dt: Datetime object to format

    Returns:
        ISO8601 formatted timestamp string
    """
    return dt.isoformat()


def get_tag_columns(table_name: str) -> list:
    """
    Get tag column names for a table.

    Args:
        table_name: Name of the table

    Returns:
        List of tag column names

    Raises:
        KeyError: If table_name is not in TABLE_SCHEMAS
    """
    if table_name not in TABLE_SCHEMAS:
        raise KeyError(f"Unknown table: {table_name}")

    return TABLE_SCHEMAS[table_name]["tags"]


def get_field_columns(table_name: str) -> list:
    """
    Get field column names for a table.

    Args:
        table_name: Name of the table

    Returns:
        List of field column names

    Raises:
        KeyError: If table_name is not in TABLE_SCHEMAS
    """
    if table_name not in TABLE_SCHEMAS:
        raise KeyError(f"Unknown table: {table_name}")

    return TABLE_SCHEMAS[table_name]["fields"]
