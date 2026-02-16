#!/usr/bin/env python3
"""
InfluxDB3 Migration Tool - CLI Entry Point

Migrate data from InfluxDB3 Code to InfluxDB3 Enterprise with:
- Incremental sync capability
- Progress tracking and resumability
- Configurability and filtering
- Safety and validation
"""

import sys
import click
from pathlib import Path

# Load .env file if it exists
try:
    from dotenv import load_dotenv
    env_file = Path(__file__).parent / '.env'
    if env_file.exists():
        load_dotenv(env_file)
except ImportError:
    # python-dotenv not installed, skip
    pass

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent / 'src'))

from src.config import ConfigLoader
from src.migrator import InfluxDB3Migrator
from src.state_manager import StateManager


@click.command()
@click.option(
    '--config',
    '-c',
    required=True,
    type=click.Path(exists=True),
    help='Path to configuration YAML file'
)
@click.option(
    '--dry-run',
    is_flag=True,
    help='Validate configuration and count rows without writing data'
)
@click.option(
    '--tables',
    '-t',
    help='Comma-separated list of tables to migrate (overrides config)'
)
@click.option(
    '--log-level',
    '-l',
    type=click.Choice(['DEBUG', 'INFO', 'WARNING', 'ERROR'], case_sensitive=False),
    help='Override logging level from config'
)
@click.option(
    '--list-runs',
    is_flag=True,
    help='List recent migration runs and exit'
)
@click.option(
    '--show-run',
    type=str,
    metavar='RUN_ID',
    help='Show details of a specific migration run and exit'
)
@click.option(
    '--cleanup-runs',
    type=int,
    metavar='N',
    help='Keep only N most recent runs, delete older ones'
)
@click.option(
    '--skip-validation',
    is_flag=True,
    help='Skip post-migration validation (useful when target has additional data from live writes)'
)
def main(config, dry_run, tables, log_level, list_runs, show_run, cleanup_runs, skip_validation):
    """
    InfluxDB3 Migration Tool

    Migrate data between InfluxDB3 instances with incremental sync support.

    Examples:

      # Dry run to validate configuration
      python migrate.py --config config.yaml --dry-run

      # Run full migration
      python migrate.py --config config.yaml

      # Migrate specific tables only
      python migrate.py --config config.yaml --tables temperature_values,power_values

      # List recent migration runs
      python migrate.py --config config.yaml --list-runs

      # Show details of a specific run
      python migrate.py --config config.yaml --show-run <run-id>
    """
    try:
        # Load configuration
        loader = ConfigLoader()
        migration_config = loader.load(config)

        # Override dry-run if specified
        if dry_run:
            migration_config.migration.dry_run = True

        # Override tables if specified
        if tables:
            table_list = [t.strip() for t in tables.split(',')]
            migration_config.tables.include = table_list
            click.echo(f"Overriding tables from command line: {table_list}")

        # Override log level if specified
        if log_level:
            migration_config.logging.level = log_level.upper()

        # Override validation if specified
        if skip_validation:
            migration_config.validation.post_migration = False
            migration_config.validation.sample_verification = 0
            click.echo("Post-migration validation disabled via --skip-validation")

        # Handle utility commands
        state_mgr = StateManager(migration_config.state.checkpoint_db)

        if list_runs:
            _list_runs(state_mgr)
            return

        if show_run:
            _show_run(state_mgr, show_run)
            return

        if cleanup_runs is not None:
            _cleanup_runs(state_mgr, cleanup_runs)
            return

        # Run migration
        migrator = InfluxDB3Migrator(migration_config)
        success = migrator.run()

        # Exit with appropriate code
        sys.exit(0 if success else 1)

    except FileNotFoundError as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)
    except ValueError as e:
        click.echo(f"Configuration error: {e}", err=True)
        sys.exit(1)
    except KeyboardInterrupt:
        click.echo("\n\nMigration interrupted by user", err=True)
        sys.exit(130)
    except Exception as e:
        click.echo(f"Unexpected error: {e}", err=True)
        import traceback
        traceback.print_exc()
        sys.exit(1)


def _list_runs(state_mgr: StateManager) -> None:
    """List recent migration runs."""
    runs = state_mgr.list_runs(limit=20)

    if not runs:
        click.echo("No migration runs found.")
        return

    click.echo("\nRecent Migration Runs:")
    click.echo("=" * 80)

    for run in runs:
        status_symbol = {
            'completed': '✓',
            'failed': '✗',
            'running': '▶',
            'cancelled': '⊘'
        }.get(run['status'], '?')

        dry_run_marker = " [DRY RUN]" if run.get('dry_run') else ""

        click.echo(f"\n{status_symbol} Run ID: {run['id']}")
        click.echo(f"  Started:  {run['started_at']}")
        if run['completed_at']:
            click.echo(f"  Completed: {run['completed_at']}")
        click.echo(f"  Status: {run['status']}{dry_run_marker}")

    click.echo("\n" + "=" * 80)
    click.echo("Use --show-run <run-id> to see details of a specific run")


def _show_run(state_mgr: StateManager, run_id: str) -> None:
    """Show details of a specific migration run."""
    try:
        summary = state_mgr.get_run_summary(run_id)

        click.echo(f"\nMigration Run: {run_id}")
        click.echo("=" * 80)

        run = summary['run']
        click.echo(f"Status: {run['status']}")
        click.echo(f"Started: {run['started_at']}")
        if run['completed_at']:
            click.echo(f"Completed: {run['completed_at']}")
        if run.get('dry_run'):
            click.echo("Mode: DRY RUN")

        # Show checkpoints
        if summary['checkpoints']:
            click.echo("\nTable Checkpoints:")
            click.echo("-" * 80)
            for cp in summary['checkpoints']:
                click.echo(
                    f"  {cp['table_name']:25s} | "
                    f"{cp['rows_migrated']:>10,} rows | "
                    f"Last: {cp['last_timestamp']}"
                )

        # Show validations
        if summary['validations']:
            click.echo("\nValidation Results:")
            click.echo("-" * 80)
            for val in summary['validations']:
                result_symbol = {'pass': '✓', 'fail': '✗', 'warning': '⚠'}.get(val['result'], '?')
                table_info = f" [{val['table_name']}]" if val['table_name'] else ""
                click.echo(f"  {result_symbol} {val['validation_type']}{table_info}: {val['result']}")
                if val['details']:
                    click.echo(f"     Details: {val['details']}")

        click.echo("=" * 80 + "\n")

    except ValueError as e:
        click.echo(f"Error: {e}", err=True)
        sys.exit(1)


def _cleanup_runs(state_mgr: StateManager, keep_n: int) -> None:
    """Clean up old migration runs."""
    click.echo(f"Cleaning up migration runs, keeping {keep_n} most recent...")

    deleted = state_mgr.cleanup_old_runs(keep_last_n=keep_n)

    if deleted > 0:
        click.echo(f"✓ Deleted {deleted} old migration run(s)")
    else:
        click.echo("No old runs to delete")


if __name__ == '__main__':
    main()
