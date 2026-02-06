# InfluxDB3 Migration Tool

A Python-based tool for migrating data from InfluxDB3 Code to InfluxDB3 Enterprise with incremental sync capability, progress tracking, and validation.

## Features

- **Incremental Sync**: Automatically resume from last checkpoint and migrate only new data
- **Progress Tracking**: Real-time progress reporting with rows/sec rates
- **Resumability**: Survive interruptions with SQLite-based checkpointing
- **Validation**: Pre-flight checks and post-migration verification
- **Configurability**: Filter by time range, tags, and select specific tables
- **Safety**: Dry-run mode, retry logic with exponential backoff
- **Performance**: Configurable batching, chunking, and optional parallel table migration

## Requirements

- Python 3.8 or higher
- Access to both source and target InfluxDB3 instances
- Network connectivity between migration tool and databases

## Installation

### 1. Clone or navigate to the directory

```bash
cd influxdb3-migration
```

### 2. Create virtual environment (recommended)

```bash
python -m venv venv

# Activate on Windows
venv\Scripts\activate

# Activate on Linux/Mac
source venv/bin/activate
```

### 3. Install dependencies

```bash
pip install -r requirements.txt
```

## Configuration

### 1. Create configuration file

```bash
cp config.yaml.example config.yaml
```

### 2. Edit config.yaml

```yaml
migration:
  name: "My_Migration"
  mode: "incremental"  # or "full"
  dry_run: false

source:
  host: "https://source.influxdata.com"
  database: "my_database"
  token: "${INFLUX_SOURCE_TOKEN}"

target:
  host: "https://target.enterprise.com"
  database: "my_database"
  token: "${INFLUX_TARGET_TOKEN}"

tables:
  include:
    - "temperature_values"
    - "power_values"
    # Add more tables as needed
```

See [config.yaml.example](config.yaml.example) for full configuration options.

### 3. Set environment variables

```bash
# Windows
set INFLUX_SOURCE_TOKEN=your_source_token
set INFLUX_TARGET_TOKEN=your_target_token

# Linux/Mac
export INFLUX_SOURCE_TOKEN="your_source_token"
export INFLUX_TARGET_TOKEN="your_target_token"
```

## Usage

### Dry Run (recommended first step)

Test your configuration without writing data:

```bash
python migrate.py --config config.yaml --dry-run
```

This will:
- Validate connectivity to source and target
- Count rows to be migrated
- Estimate migration time
- Display what would be migrated

### Run Full Migration

Execute the actual migration:

```bash
python migrate.py --config config.yaml
```

### Incremental Sync

For ongoing synchronization, simply re-run the same command:

```bash
python migrate.py --config config.yaml
```

The tool automatically resumes from the last checkpoint and migrates only new data.

### Migrate Specific Tables

Override configuration to migrate specific tables:

```bash
python migrate.py --config config.yaml --tables temperature_values,power_values
```

### Override Log Level

```bash
python migrate.py --config config.yaml --log-level DEBUG
```

### List Migration Runs

View history of migration runs:

```bash
python migrate.py --config config.yaml --list-runs
```

### Show Run Details

View details of a specific migration run:

```bash
python migrate.py --config config.yaml --show-run <run-id>
```

### Clean Up Old Runs

Keep only the N most recent runs:

```bash
python migrate.py --config config.yaml --cleanup-runs 10
```

## Migration Modes

### Full Migration

```yaml
migration:
  mode: "full"
```

- Migrates all historical data
- Ignores existing checkpoints
- Use for initial migration or complete refresh

### Incremental Migration

```yaml
migration:
  mode: "incremental"
```

- Resumes from last checkpoint
- Migrates only new data
- Use for ongoing synchronization

## Time-Based Filtering

Migrate only data within a specific time range:

```yaml
filters:
  time_range:
    start: "2024-01-01T00:00:00Z"  # ISO 8601 format
    end: "2024-12-31T23:59:59Z"
```

## Tag-Based Filtering

Migrate only data matching specific tags:

```yaml
filters:
  tags:
    location: "M3"
    device: "EnvoyM3"
```

## Performance Tuning

### Batch Size

Larger batches = faster, more memory:

```yaml
batching:
  batch_size_rows: 50000  # Default: 10000
```

### Chunk Size

Larger chunks = fewer checkpoints, faster, more memory:

```yaml
batching:
  chunk_size_days: 7  # Default: 1
```

### Parallel Table Migration (Experimental)

Migrate multiple tables simultaneously:

```yaml
performance:
  parallel_tables: true  # Default: false
```

**Warning**: Uses more memory and database connections.

## Architecture

### Components

- **config.py**: YAML configuration parsing with environment variable expansion
- **connections.py**: InfluxDB3 client wrapper with query and write methods
- **state_manager.py**: SQLite-based checkpoint persistence
- **migrator.py**: Core migration orchestration
- **validator.py**: Pre-flight and post-migration validation
- **progress.py**: Progress tracking and logging
- **utils.py**: Helper functions and table schemas

### Data Flow

```
1. Load configuration (config.yaml)
2. Pre-flight validation (connectivity, tables, permissions)
3. For each table:
   a. Determine time range (config + checkpoints)
   b. Split into daily chunks
   c. For each chunk:
      - Query source
      - Write to target in batches
      - Save checkpoint
      - Update progress
   d. Post-migration validation
4. Complete run, display summary
```

### Checkpointing

Migration state is stored in SQLite (`migration_state.db`):

- **migration_runs**: History of migration executions
- **table_checkpoints**: Per-table progress watermarks
- **validation_results**: Validation outcomes

This enables:
- Resuming after interruption
- Incremental sync tracking
- Migration history auditing

## Validation

### Pre-Flight Checks

Before migration starts:

1. Test source connection
2. Test target connection
3. Verify tables exist in source
4. Test write permissions on target (writes a test record)

### Post-Migration Validation

After each table migration:

1. **Row Count Comparison**: Verify source and target row counts match
2. **Sample Verification**: Query random samples and verify data integrity

## Logging

Logs are written to:
- Console (stdout) - configurable
- Log file (`migration.log`) - rotated at 10MB

Log levels: DEBUG, INFO, WARNING, ERROR

Example log output:

```
2025-02-05 10:30:15 [INFO    ] ============================================================
2025-02-05 10:30:15 [INFO    ] InfluxDB3 Migration: SmartHome_Prod_to_Enterprise
2025-02-05 10:30:15 [INFO    ] Mode: incremental
2025-02-05 10:30:15 [INFO    ] ============================================================
2025-02-05 10:30:16 [INFO    ] ✓ All pre-flight checks passed
2025-02-05 10:30:16 [INFO    ]
2025-02-05 10:30:16 [INFO    ] ============================================================
2025-02-05 10:30:16 [INFO    ] Starting migration: temperature_values [1/7]
2025-02-05 10:30:16 [INFO    ] ============================================================
2025-02-05 10:30:17 [INFO    ]   temperature_values: 150,000 rows (12,500 rows/sec)
...
```

## Troubleshooting

### Connection Errors

**Problem**: `Failed to connect to InfluxDB3`

**Solutions**:
- Verify host URL is correct
- Check network connectivity
- Ensure authentication token is valid
- Check firewall rules

### Target Database Not Found

**Problem**: `Target database does not exist`

**What Happens**:

The tool will detect this during pre-flight checks and prompt you with options:

1. Create the database manually now (migration will wait)
2. Create it later (abort migration)
3. Continue anyway (may fail during write)

**How to Create Database**:

#### Option 1: InfluxDB UI

1. Open your InfluxDB host in a browser
2. Navigate to: Load Data → Databases
3. Create the database with the name from your config

#### Option 2: influxctl CLI (for InfluxDB Clustered)

```bash
influxctl database create your_database_name
```

#### Option 3: Management API

See the [InfluxDB API documentation](https://docs.influxdata.com/influxdb/cloud-serverless/api/)

After creating the database, press Enter in the migration tool to continue.

### Table Not Found

**Problem**: `Table 'xyz' does not exist in source`

**Solutions**:
- Verify table name spelling
- List tables in source database:
  ```sql
  SELECT table_name FROM information_schema.tables
  ```
- Check source database name

### Out of Memory

**Problem**: Process runs out of memory during migration

**Solutions**:
- Reduce `batch_size_rows` (e.g., from 50000 to 10000)
- Reduce `chunk_size_days` (e.g., from 7 to 1)
- Lower `max_memory_mb` limit
- Disable `parallel_tables` if enabled
- Run on machine with more RAM

### Slow Migration

**Problem**: Migration is taking too long

**Solutions**:
- Increase `batch_size_rows` (e.g., to 50000)
- Increase `chunk_size_days` (e.g., to 7)
- Enable `parallel_tables: true` (use with caution)
- Check network bandwidth between tool and databases
- Reduce `sample_verification` or disable post-migration validation temporarily

### Validation Failures

**Problem**: Row counts don't match after migration

**Solutions**:
- Check for data writes to source during migration
- Verify time range filters
- Re-run migration to catch any missed data
- Check target database for write errors
- Review migration logs for errors

### Interrupted Migration

**Problem**: Migration was interrupted (Ctrl+C, crash, network issue)

**Solution**:
- Simply re-run the same migration command
- Tool will automatically resume from last checkpoint
- No data duplication - migration is idempotent

## Best Practices

### Initial Migration

1. Start with dry-run to validate configuration
2. Test with a small time range or single table
3. Monitor first full table migration closely
4. Verify row counts after migration
5. Once confident, migrate all tables

### Ongoing Synchronization

1. Use `mode: "incremental"`
2. Run on schedule (e.g., daily via cron/Task Scheduler)
3. Monitor logs for errors
4. Periodically verify data consistency
5. Clean up old migration runs: `--cleanup-runs 10`

### Performance vs. Safety Trade-offs

**Maximum Safety** (slower):
```yaml
batching:
  chunk_size_days: 1
  batch_size_rows: 5000
performance:
  parallel_tables: false
validation:
  post_migration: true
  sample_verification: 100
```

**Maximum Performance** (less safe):
```yaml
batching:
  chunk_size_days: 7
  batch_size_rows: 50000
performance:
  parallel_tables: true
validation:
  post_migration: false
  sample_verification: 0
```

**Recommended Balance**:
```yaml
batching:
  chunk_size_days: 1
  batch_size_rows: 10000
performance:
  parallel_tables: false
validation:
  post_migration: true
  sample_verification: 10
```

## Table Schemas

The tool migrates these measurement tables with their specific schemas:

| Table | Tags | Fields |
|-------|------|--------|
| temperature_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_temp |
| power_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_watt |
| energy_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_delta_kwh, value_cumulated_kwh |
| voltage_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_volt |
| percent_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_percent |
| status_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_status |
| counter_values | measurement_id, category, sub_category, sensor_type, location, device, measurement | value_counter |

## Performance Estimates

For medium data volumes (10GB, 50 million points across 7 tables):

- **Duration**: ~1-2 hours
- **Rate**: ~7,000-14,000 rows/sec
- **Memory**: <512MB
- **Network**: ~2-5 MB/sec sustained

*Actual performance depends on network speed, database performance, and configuration.*

## License

This tool is part of the SmartHomeTS project.

## Support

For issues, questions, or contributions, please contact the project maintainer or open an issue in the repository.

## Version History

- **1.0.0** (2025-02-05): Initial release
  - Incremental sync support
  - Progress tracking and resumability
  - Configurable filtering and batching
  - Validation and safety features
