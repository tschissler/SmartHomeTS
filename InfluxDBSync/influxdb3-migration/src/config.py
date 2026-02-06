"""Configuration management for InfluxDB3 migration"""

from dataclasses import dataclass, field
from typing import Optional, List, Dict, Any
import yaml
import os
import re
from datetime import datetime


@dataclass
class ConnectionConfig:
    """InfluxDB3 connection configuration"""
    host: str
    database: str
    token: str
    enable_gzip: bool = True

    def __post_init__(self):
        if not self.host:
            raise ValueError("host is required")
        if not self.database:
            raise ValueError("database is required")
        if not self.token:
            raise ValueError("token is required")


@dataclass
class TimeRangeConfig:
    """Time range filtering configuration"""
    start: Optional[str] = None
    end: Optional[str] = None

    def get_start_datetime(self) -> Optional[datetime]:
        """Convert start string to datetime (timezone-naive UTC)"""
        if self.start:
            # Replace 'Z' with '+00:00' for proper ISO parsing
            dt_str = self.start.replace('Z', '+00:00')
            dt = datetime.fromisoformat(dt_str)
            # Convert to naive UTC for consistent comparison
            if dt.tzinfo is not None:
                dt = dt.replace(tzinfo=None)
            return dt
        return None

    def get_end_datetime(self) -> Optional[datetime]:
        """Convert end string to datetime (timezone-naive UTC)"""
        if self.end:
            # Replace 'Z' with '+00:00' for proper ISO parsing
            dt_str = self.end.replace('Z', '+00:00')
            dt = datetime.fromisoformat(dt_str)
            # Convert to naive UTC for consistent comparison
            if dt.tzinfo is not None:
                dt = dt.replace(tzinfo=None)
            return dt
        return None


@dataclass
class FilterConfig:
    """Data filtering configuration"""
    time_range: TimeRangeConfig = field(default_factory=lambda: TimeRangeConfig())
    tags: Dict[str, str] = field(default_factory=dict)


@dataclass
class TableConfig:
    """Table selection configuration"""
    include: List[str] = field(default_factory=list)
    exclude: List[str] = field(default_factory=list)

    def get_tables_to_migrate(self) -> List[str]:
        """Get final list of tables to migrate"""
        if not self.include:
            # Default to all known tables if none specified
            from .utils import TABLE_SCHEMAS
            tables = list(TABLE_SCHEMAS.keys())
        else:
            tables = self.include.copy()

        # Remove excluded tables
        if self.exclude:
            tables = [t for t in tables if t not in self.exclude]

        return tables


@dataclass
class BatchingConfig:
    """Batching and chunking configuration"""
    chunk_size_days: int = 1
    batch_size_rows: int = 10000
    max_memory_mb: int = 512

    def __post_init__(self):
        if self.chunk_size_days <= 0:
            raise ValueError("chunk_size_days must be positive")
        if self.batch_size_rows <= 0:
            raise ValueError("batch_size_rows must be positive")
        if self.max_memory_mb <= 0:
            raise ValueError("max_memory_mb must be positive")


@dataclass
class PerformanceConfig:
    """Performance tuning configuration"""
    parallel_tables: bool = False
    query_timeout_seconds: int = 300
    write_timeout_seconds: int = 300
    retry_attempts: int = 3
    retry_delay_seconds: int = 5

    def __post_init__(self):
        if self.retry_attempts < 1:
            raise ValueError("retry_attempts must be at least 1")
        if self.retry_delay_seconds < 0:
            raise ValueError("retry_delay_seconds must be non-negative")


@dataclass
class ValidationConfig:
    """Validation configuration"""
    pre_flight: bool = True
    post_migration: bool = True
    sample_verification: int = 100

    def __post_init__(self):
        if self.sample_verification < 0:
            raise ValueError("sample_verification must be non-negative")


@dataclass
class LoggingConfig:
    """Logging configuration"""
    level: str = "INFO"
    file: str = "migration.log"
    console: bool = True

    def __post_init__(self):
        valid_levels = ["DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"]
        if self.level.upper() not in valid_levels:
            raise ValueError(f"log level must be one of {valid_levels}")


@dataclass
class StateConfig:
    """State management configuration"""
    checkpoint_db: str = "migration_state.db"
    checkpoint_frequency: str = "chunk"

    def __post_init__(self):
        valid_frequencies = ["chunk", "batch"]
        if self.checkpoint_frequency not in valid_frequencies:
            raise ValueError(f"checkpoint_frequency must be one of {valid_frequencies}")


@dataclass
class MigrationMetadata:
    """Migration metadata"""
    name: str
    mode: str
    dry_run: bool = False

    def __post_init__(self):
        valid_modes = ["incremental", "full"]
        if self.mode not in valid_modes:
            raise ValueError(f"mode must be one of {valid_modes}")


@dataclass
class MigrationConfig:
    """Complete migration configuration"""
    migration: MigrationMetadata
    source: ConnectionConfig
    target: ConnectionConfig
    tables: TableConfig
    filters: FilterConfig
    batching: BatchingConfig
    performance: PerformanceConfig
    validation: ValidationConfig
    logging: LoggingConfig
    state: StateConfig


class ConfigLoader:
    """Load and validate YAML configuration"""

    ENV_VAR_PATTERN = re.compile(r'\$\{([^}]+)\}')

    def load(self, config_path: str) -> MigrationConfig:
        """
        Load configuration from YAML file.

        Args:
            config_path: Path to configuration YAML file

        Returns:
            MigrationConfig object

        Raises:
            FileNotFoundError: If config file doesn't exist
            ValueError: If configuration is invalid
        """
        if not os.path.exists(config_path):
            raise FileNotFoundError(f"Configuration file not found: {config_path}")

        with open(config_path, 'r') as f:
            raw_config = yaml.safe_load(f)

        # Expand environment variables
        raw_config = self._expand_env_vars(raw_config)

        # Parse and validate configuration
        return self._parse_config(raw_config)

    def _expand_env_vars(self, obj: Any) -> Any:
        """
        Recursively expand environment variables in configuration.

        Environment variables should be in format: ${VAR_NAME}

        Args:
            obj: Configuration object (dict, list, str, or primitive)

        Returns:
            Object with environment variables expanded
        """
        if isinstance(obj, dict):
            return {k: self._expand_env_vars(v) for k, v in obj.items()}
        elif isinstance(obj, list):
            return [self._expand_env_vars(item) for item in obj]
        elif isinstance(obj, str):
            return self._expand_env_var_string(obj)
        else:
            return obj

    def _expand_env_var_string(self, s: str) -> str:
        """
        Expand environment variables in a string.

        Args:
            s: String potentially containing ${VAR_NAME} patterns

        Returns:
            String with environment variables expanded
        """
        def replace_env_var(match):
            var_name = match.group(1)
            value = os.environ.get(var_name)
            if value is None:
                raise ValueError(f"Environment variable not set: {var_name}")
            return value

        return self.ENV_VAR_PATTERN.sub(replace_env_var, s)

    def _parse_config(self, raw_config: Dict) -> MigrationConfig:
        """
        Parse raw configuration dict into MigrationConfig.

        Args:
            raw_config: Dictionary from YAML

        Returns:
            Validated MigrationConfig object
        """
        # Parse migration metadata
        migration_data = raw_config.get('migration', {})
        migration = MigrationMetadata(
            name=migration_data.get('name', 'unnamed_migration'),
            mode=migration_data.get('mode', 'incremental'),
            dry_run=migration_data.get('dry_run', False)
        )

        # Parse source connection
        source_data = raw_config.get('source', {})
        source = ConnectionConfig(
            host=source_data.get('host', ''),
            database=source_data.get('database', ''),
            token=source_data.get('token', ''),
            enable_gzip=source_data.get('enable_gzip', True)
        )

        # Parse target connection
        target_data = raw_config.get('target', {})
        target = ConnectionConfig(
            host=target_data.get('host', ''),
            database=target_data.get('database', ''),
            token=target_data.get('token', ''),
            enable_gzip=target_data.get('enable_gzip', True)
        )

        # Parse tables
        tables_data = raw_config.get('tables', {})
        tables = TableConfig(
            include=tables_data.get('include', []),
            exclude=tables_data.get('exclude', [])
        )

        # Parse filters
        filters_data = raw_config.get('filters', {})
        time_range_data = filters_data.get('time_range', {})
        time_range = TimeRangeConfig(
            start=time_range_data.get('start') if time_range_data else None,
            end=time_range_data.get('end') if time_range_data else None
        )
        filters = FilterConfig(
            time_range=time_range,
            tags=filters_data.get('tags', {})
        )

        # Parse batching
        batching_data = raw_config.get('batching', {})
        batching = BatchingConfig(
            chunk_size_days=batching_data.get('chunk_size_days', 1),
            batch_size_rows=batching_data.get('batch_size_rows', 10000),
            max_memory_mb=batching_data.get('max_memory_mb', 512)
        )

        # Parse performance
        perf_data = raw_config.get('performance', {})
        performance = PerformanceConfig(
            parallel_tables=perf_data.get('parallel_tables', False),
            query_timeout_seconds=perf_data.get('query_timeout_seconds', 300),
            write_timeout_seconds=perf_data.get('write_timeout_seconds', 300),
            retry_attempts=perf_data.get('retry_attempts', 3),
            retry_delay_seconds=perf_data.get('retry_delay_seconds', 5)
        )

        # Parse validation
        validation_data = raw_config.get('validation', {})
        validation = ValidationConfig(
            pre_flight=validation_data.get('pre_flight', True),
            post_migration=validation_data.get('post_migration', True),
            sample_verification=validation_data.get('sample_verification', 100)
        )

        # Parse logging
        logging_data = raw_config.get('logging', {})
        logging_config = LoggingConfig(
            level=logging_data.get('level', 'INFO'),
            file=logging_data.get('file', 'migration.log'),
            console=logging_data.get('console', True)
        )

        # Parse state
        state_data = raw_config.get('state', {})
        state = StateConfig(
            checkpoint_db=state_data.get('checkpoint_db', 'migration_state.db'),
            checkpoint_frequency=state_data.get('checkpoint_frequency', 'chunk')
        )

        return MigrationConfig(
            migration=migration,
            source=source,
            target=target,
            tables=tables,
            filters=filters,
            batching=batching,
            performance=performance,
            validation=validation,
            logging=logging_config,
            state=state
        )
