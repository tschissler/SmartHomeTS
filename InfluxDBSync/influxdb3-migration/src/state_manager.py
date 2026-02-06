"""State management with SQLite for migration checkpointing"""

import sqlite3
import hashlib
import json
import logging
from datetime import datetime
from typing import Optional, Dict, List
from pathlib import Path


logger = logging.getLogger(__name__)


class StateManager:
    """
    Manage migration state and checkpoints using SQLite.

    Provides ACID guarantees for checkpoint persistence, enabling:
    - Resumable migrations after interruption
    - Incremental sync tracking
    - Validation result logging
    """

    def __init__(self, db_path: str):
        """
        Initialize state manager.

        Args:
            db_path: Path to SQLite database file
        """
        self.db_path = db_path
        self._ensure_db_exists()
        self._init_schema()

    def _ensure_db_exists(self) -> None:
        """Ensure database directory exists."""
        db_file = Path(self.db_path)
        db_file.parent.mkdir(parents=True, exist_ok=True)

    def _init_schema(self) -> None:
        """Initialize database schema if not exists."""
        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                CREATE TABLE IF NOT EXISTS migration_runs (
                    id TEXT PRIMARY KEY,
                    config_hash TEXT NOT NULL,
                    started_at TIMESTAMP NOT NULL,
                    completed_at TIMESTAMP,
                    status TEXT NOT NULL,
                    dry_run INTEGER NOT NULL DEFAULT 0
                )
            """)

            conn.execute("""
                CREATE TABLE IF NOT EXISTS table_checkpoints (
                    run_id TEXT NOT NULL,
                    table_name TEXT NOT NULL,
                    target_database TEXT NOT NULL,
                    last_timestamp TIMESTAMP NOT NULL,
                    rows_migrated INTEGER NOT NULL DEFAULT 0,
                    updated_at TIMESTAMP NOT NULL,
                    PRIMARY KEY (run_id, table_name),
                    FOREIGN KEY (run_id) REFERENCES migration_runs(id)
                )
            """)

            conn.execute("""
                CREATE TABLE IF NOT EXISTS validation_results (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    run_id TEXT NOT NULL,
                    table_name TEXT,
                    validation_type TEXT NOT NULL,
                    result TEXT NOT NULL,
                    details TEXT,
                    created_at TIMESTAMP NOT NULL,
                    FOREIGN KEY (run_id) REFERENCES migration_runs(id)
                )
            """)

            # Create indexes for faster lookups
            conn.execute("""
                CREATE INDEX IF NOT EXISTS idx_checkpoints_table
                ON table_checkpoints(table_name)
            """)

            conn.execute("""
                CREATE INDEX IF NOT EXISTS idx_validation_run
                ON validation_results(run_id)
            """)

            # Migration: Add target_database column if it doesn't exist (for existing databases)
            cursor = conn.execute("PRAGMA table_info(table_checkpoints)")
            columns = [row[1] for row in cursor.fetchall()]
            if 'target_database' not in columns:
                logger.info("Migrating database schema: adding target_database column to table_checkpoints")
                # Add column with default value for existing rows
                conn.execute("""
                    ALTER TABLE table_checkpoints
                    ADD COLUMN target_database TEXT NOT NULL DEFAULT 'unknown'
                """)
                logger.info("Schema migration completed")

            conn.commit()
            logger.debug(f"Initialized state database: {self.db_path}")

    def create_run(self, config: Dict, dry_run: bool = False) -> str:
        """
        Create a new migration run record.

        Args:
            config: Migration configuration dict
            dry_run: Whether this is a dry run

        Returns:
            Run ID (UUID)
        """
        import uuid

        run_id = str(uuid.uuid4())
        config_hash = self._hash_config(config)

        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                INSERT INTO migration_runs (id, config_hash, started_at, status, dry_run)
                VALUES (?, ?, ?, ?, ?)
            """, (run_id, config_hash, datetime.utcnow(), 'running', int(dry_run)))
            conn.commit()

        logger.info(f"Created migration run: {run_id} (dry_run={dry_run})")
        return run_id

    def complete_run(self, run_id: str, status: str = 'completed') -> None:
        """
        Mark a migration run as completed.

        Args:
            run_id: Run ID
            status: Final status (completed, failed, cancelled)
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                UPDATE migration_runs
                SET completed_at = ?, status = ?
                WHERE id = ?
            """, (datetime.utcnow(), status, run_id))
            conn.commit()

        logger.info(f"Completed migration run: {run_id} with status={status}")

    def save_checkpoint(
        self,
        run_id: str,
        table: str,
        last_timestamp: datetime,
        rows_migrated: int,
        target_database: str
    ) -> None:
        """
        Save checkpoint for a table.

        Args:
            run_id: Run ID
            table: Table name
            last_timestamp: Latest timestamp migrated
            rows_migrated: Total rows migrated for this table
            target_database: Target database name (for checkpoint isolation)
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                INSERT OR REPLACE INTO table_checkpoints
                (run_id, table_name, target_database, last_timestamp, rows_migrated, updated_at)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (run_id, table, target_database, last_timestamp, rows_migrated, datetime.utcnow()))
            conn.commit()

        logger.debug(f"Saved checkpoint for {table} (DB: {target_database}): {last_timestamp} ({rows_migrated:,} rows)")

    def get_last_checkpoint(self, table: str, target_database: str) -> Optional[datetime]:
        """
        Get last successful migration timestamp for a table in a specific target database.

        This is used for incremental sync to determine where to start.
        Includes checkpoints from interrupted runs to support resumability.

        Args:
            table: Table name
            target_database: Target database name (for checkpoint isolation)

        Returns:
            Last migrated timestamp, or None if never migrated
        """
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.execute("""
                SELECT last_timestamp
                FROM table_checkpoints
                WHERE table_name = ?
                  AND target_database = ?
                  AND run_id IN (
                      SELECT id FROM migration_runs
                      WHERE dry_run = 0
                  )
                ORDER BY updated_at DESC
                LIMIT 1
            """, (table, target_database))
            result = cursor.fetchone()

        if result:
            timestamp = datetime.fromisoformat(result[0])
            logger.debug(f"Last checkpoint for {table} (DB: {target_database}): {timestamp}")
            return timestamp

        logger.debug(f"No checkpoint found for {table} (DB: {target_database})")
        return None

    def get_current_run_checkpoint(self, run_id: str, table: str) -> Optional[Dict]:
        """
        Get checkpoint for current run and table.

        Args:
            run_id: Run ID
            table: Table name

        Returns:
            Dict with checkpoint info, or None if not found
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.row_factory = sqlite3.Row
            cursor = conn.execute("""
                SELECT *
                FROM table_checkpoints
                WHERE run_id = ? AND table_name = ?
            """, (run_id, table))
            row = cursor.fetchone()

        if row:
            return dict(row)
        return None

    def save_validation_result(
        self,
        run_id: str,
        validation_type: str,
        result: str,
        table: Optional[str] = None,
        details: Optional[str] = None
    ) -> None:
        """
        Save validation result.

        Args:
            run_id: Run ID
            validation_type: Type of validation (pre_flight, post_migration, sample)
            result: Result (pass, fail, warning)
            table: Optional table name
            details: Optional details (JSON string or text)
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.execute("""
                INSERT INTO validation_results
                (run_id, table_name, validation_type, result, details, created_at)
                VALUES (?, ?, ?, ?, ?, ?)
            """, (run_id, table, validation_type, result, details, datetime.utcnow()))
            conn.commit()

        logger.debug(f"Saved validation result: {validation_type} = {result}")

    def get_validation_results(self, run_id: str) -> List[Dict]:
        """
        Get all validation results for a run.

        Args:
            run_id: Run ID

        Returns:
            List of validation result dicts
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.row_factory = sqlite3.Row
            cursor = conn.execute("""
                SELECT *
                FROM validation_results
                WHERE run_id = ?
                ORDER BY created_at
            """, (run_id,))
            return [dict(row) for row in cursor.fetchall()]

    def get_run_summary(self, run_id: str) -> Dict:
        """
        Get summary of a migration run.

        Args:
            run_id: Run ID

        Returns:
            Dict with run summary including checkpoints
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.row_factory = sqlite3.Row

            # Get run info
            cursor = conn.execute("""
                SELECT * FROM migration_runs WHERE id = ?
            """, (run_id,))
            run_row = cursor.fetchone()
            if not run_row:
                raise ValueError(f"Run not found: {run_id}")

            run_info = dict(run_row)

            # Get checkpoints
            cursor = conn.execute("""
                SELECT * FROM table_checkpoints WHERE run_id = ?
            """, (run_id,))
            checkpoints = [dict(row) for row in cursor.fetchall()]

            # Get validation results
            cursor = conn.execute("""
                SELECT * FROM validation_results WHERE run_id = ?
            """, (run_id,))
            validations = [dict(row) for row in cursor.fetchall()]

        return {
            'run': run_info,
            'checkpoints': checkpoints,
            'validations': validations
        }

    def list_runs(self, limit: int = 10) -> List[Dict]:
        """
        List recent migration runs.

        Args:
            limit: Maximum number of runs to return

        Returns:
            List of run dicts
        """
        with sqlite3.connect(self.db_path) as conn:
            conn.row_factory = sqlite3.Row
            cursor = conn.execute("""
                SELECT * FROM migration_runs
                ORDER BY started_at DESC
                LIMIT ?
            """, (limit,))
            return [dict(row) for row in cursor.fetchall()]

    @staticmethod
    def _hash_config(config: Dict) -> str:
        """
        Generate hash of configuration for tracking.

        Args:
            config: Configuration dict

        Returns:
            SHA256 hash string
        """
        config_str = json.dumps(config, sort_keys=True)
        return hashlib.sha256(config_str.encode()).hexdigest()[:16]

    def cleanup_old_runs(self, keep_last_n: int = 10) -> int:
        """
        Delete old migration runs, keeping only the most recent ones.

        Args:
            keep_last_n: Number of runs to keep

        Returns:
            Number of runs deleted
        """
        with sqlite3.connect(self.db_path) as conn:
            # Get IDs of runs to delete
            cursor = conn.execute("""
                SELECT id FROM migration_runs
                ORDER BY started_at DESC
                LIMIT -1 OFFSET ?
            """, (keep_last_n,))
            run_ids = [row[0] for row in cursor.fetchall()]

            if not run_ids:
                return 0

            # Delete validation results
            placeholders = ','.join(['?'] * len(run_ids))
            conn.execute(f"""
                DELETE FROM validation_results
                WHERE run_id IN ({placeholders})
            """, run_ids)

            # Delete checkpoints
            conn.execute(f"""
                DELETE FROM table_checkpoints
                WHERE run_id IN ({placeholders})
            """, run_ids)

            # Delete runs
            conn.execute(f"""
                DELETE FROM migration_runs
                WHERE id IN ({placeholders})
            """, run_ids)

            conn.commit()

        logger.info(f"Cleaned up {len(run_ids)} old migration runs")
        return len(run_ids)

    def close(self) -> None:
        """Close database connection (no-op for sqlite3, connections are per-query)."""
        pass

    def __repr__(self) -> str:
        return f"StateManager(db_path='{self.db_path}')"
