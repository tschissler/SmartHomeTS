import json
import os
from typing import Any, Dict, List, Optional

import anyio

try:
	# InfluxDB 3 Python client
	from influxdb_client_3 import InfluxDBClient3
	from influxdb_client_3 import flight_client_options as _flight_client_options
except Exception as e:  # pragma: no cover - helpful message if deps missing
	InfluxDBClient3 = None  # type: ignore
	_flight_client_options = None  # type: ignore
	_import_error = e
else:
	_import_error = None

from mcp.server.fastmcp import FastMCP


# ------------ Configuration ------------
SERVER_NAME = "influxdb3-mcp"


def _get_env(name: str, default: Optional[str] = None) -> Optional[str]:
	v = os.getenv(name)
	return v if v not in (None, "") else default


def _bool_env(name: str, default: bool = False) -> bool:
	v = os.getenv(name)
	if v is None:
		return default
	return v.strip().lower() in ("1", "true", "yes", "y", "on")


def _get_env_any(names: List[str], default: Optional[str] = None) -> Optional[str]:
	"""Return the first non-empty environment variable from names."""
	for n in names:
		val = _get_env(n)
		if val not in (None, ""):
			return val
	return default


class InfluxConnection:
	"""Lazy InfluxDB3 connection holder with simple helpers."""

	def __init__(self) -> None:
		self._client: Optional[Any] = None
		self._last_error: Optional[str] = None
		# Override connection parameters (takes precedence over env vars)
		self._override_host: Optional[str] = None
		self._override_database: Optional[str] = None
		self._override_token: Optional[str] = None

	def set_connection_params(
		self, 
		host: Optional[str] = None, 
		database: Optional[str] = None, 
		token: Optional[str] = None
	) -> None:
		"""Set connection parameters programmatically. These override environment variables."""
		if host is not None:
			self._override_host = host
		if database is not None:
			self._override_database = database
		if token is not None:
			self._override_token = token
		# Reset client to force reconnection with new params
		self._client = None
		self._last_error = None

	def _build_client(self) -> Any:
		if InfluxDBClient3 is None:  # Dependencies not installed
			raise RuntimeError(
				f"influxdb3-python client is not installed correctly: {_import_error}"
			)

		# Use override params if set, otherwise fall back to env vars
		host = self._override_host or _get_env_any(["SMARTHOME__INFLUXDB3_HOST", "INFLUXDB3_HOST"])    
		database = self._override_database or _get_env_any(["SMARTHOME__INFLUXDB3_DATABASE", "INFLUXDB3_DATABASE"]) 
		token = self._override_token or _get_env_any(["SMARTHOME__INFLUXDB3_TOKEN", "INFLUXDB3_TOKEN"]) 
		enable_gzip = _bool_env("INFLUXDB3_ENABLE_GZIP", True)

		if not host or not database:
			raise RuntimeError(
				"Missing required environment variables INFLUXDB3_HOST and/or INFLUXDB3_DATABASE"
			)

		flight_opts = None
		if _bool_env("INFLUXDB3_USE_CERTIFI", False):
			try:
				import certifi

				with open(certifi.where(), "r", encoding="utf-8") as fh:
					cert = fh.read()
				if _flight_client_options is not None:
					flight_opts = _flight_client_options(tls_root_certs=cert)
			except Exception as e:  # pragma: no cover
				# If certifi setup fails, proceed without custom certs
				self._last_error = f"certifi setup failed: {e}"

		client = InfluxDBClient3(
			host=host,
			token=token or "",
			database=database,
			enable_gzip=enable_gzip,
			flight_client_options=flight_opts,
		)
		return client

	def client(self) -> Any:
		if self._client is None:
			self._client = self._build_client()
		return self._client

	def status(self) -> Dict[str, Any]:
		host = self._override_host or _get_env_any(["SMARTHOME__INFLUXDB3_HOST", "INFLUXDB3_HOST"])
		database = self._override_database or _get_env_any(["SMARTHOME__INFLUXDB3_DATABASE", "INFLUXDB3_DATABASE"])
		token = self._override_token or _get_env_any(["SMARTHOME__INFLUXDB3_TOKEN", "INFLUXDB3_TOKEN"])
		return {
			"host": host,
			"database": database,
			"has_token": bool(token),
			"enable_gzip": _bool_env("INFLUXDB3_ENABLE_GZIP", True),
			"using_certifi": _bool_env("INFLUXDB3_USE_CERTIFI", False),
			"last_error": self._last_error,
			"deps_loaded": _import_error is None,
			"using_overrides": bool(self._override_host or self._override_database or self._override_token),
		}


conn = InfluxConnection()


# ------------ MCP Server ------------
mcp = FastMCP(SERVER_NAME)


def _json_content(data: Any) -> str:
	return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


def _as_table(obj: Any) -> Any:
	"""Normalize query result to an Arrow Table-like object when possible.

	influxdb3-python may return a Reader with .read_all() or directly a pyarrow.Table
	depending on version and mode. This helper makes it consistent.
	"""
	if hasattr(obj, "read_all") and callable(getattr(obj, "read_all")):
		try:
			return obj.read_all()
		except Exception:
			return obj
	return obj


def _is_pandas_df(obj: Any) -> bool:
	try:
		import pandas as pd  # type: ignore

		return isinstance(obj, pd.DataFrame)
	except Exception:
		return False


def _table_to_rows(table_obj: Any, max_rows: int = 0) -> List[Dict[str, Any]]:
	"""Convert a pyarrow.Table or pandas.DataFrame into a list of dict rows."""
	# pandas path
	if _is_pandas_df(table_obj):
		df = table_obj
		if max_rows > 0:
			df = df.head(max_rows)
		return df.to_dict(orient="records")

	# arrow table path (has .to_pandas or .schema)
	try:
		if hasattr(table_obj, "to_pandas"):
			df = table_obj.to_pandas()
			if max_rows > 0:
				df = df.head(max_rows)
			return df.to_dict(orient="records")
	except Exception:
		pass

	# Manual extraction from arrow table without pandas
	try:
		cols = [c.name for c in table_obj.schema]
		total = getattr(table_obj, "num_rows", 0)
		limit = min(max_rows if max_rows > 0 else total, total)
		rows: List[Dict[str, Any]] = []
		for i in range(limit):
			rows.append({cols[j]: table_obj.column(j)[i].as_py() for j in range(len(cols))})
		return rows
	except Exception:
		# Fallback: return empty on unexpected format
		return []


@mcp.tool()
def set_connection(host: str, database: str, token: str = "") -> str:
	"""Set connection parameters for InfluxDB3. These override environment variables.
	
	Parameters:
	- host: InfluxDB3 host URL (e.g., "http://localhost:8086")
	- database: Database/bucket name
	- token: Authentication token (optional, can be empty string)
	
	Returns: Connection status
	"""
	try:
		conn.set_connection_params(host=host, database=database, token=token)
		# Test the connection
		client = conn.client()
		try:
			_ = client.query(query="SELECT 1", language="sql")
			ok = True
			msg = "Connection established successfully"
		except Exception as e:
			ok = False
			msg = f"Connection test failed: {e}"
		return _json_content({"ok": ok, "message": msg, "config": conn.status()})
	except Exception as e:
		return _json_content({"ok": False, "message": f"Failed to set connection: {e}", "config": conn.status()})


@mcp.tool()
def health() -> str:
	"""Return server health and InfluxDB configuration status."""
	try:
		# Try a trivial query to ensure connectivity if possible
		client = conn.client()
		# Use a light-weight query against information_schema if available
		try:
			_ = client.query(query="SELECT 1", language="sql")
			ok = True
			msg = "ok"
		except Exception as e:  # pragma: no cover
			ok = False
			msg = f"influx query failed: {e}"
	except Exception as e:
		ok = False
		msg = f"init failed: {e}"
	return _json_content({"ok": ok, "message": msg, "config": conn.status()})


@mcp.tool()
def list_tables(include_system: bool = False) -> str:
	"""List available tables/measurements in the configured database (best effort)."""
	client = conn.client()

	# Prefer information_schema, filter out system schemas by default
	if include_system:
		queries = [
			"SELECT table_name FROM information_schema.tables ORDER BY table_name",
			"SHOW TABLES",
		]
	else:
		queries = [
			"SELECT table_name FROM information_schema.tables WHERE table_schema NOT IN ('information_schema','system') ORDER BY table_name",
			"SHOW TABLES",
		]
	last_err: Optional[str] = None
	for q in queries:
		try:
			res = client.query(query=q, language="sql")
			table = _as_table(res)
			# Try pandas-first for simplicity
			try:
				if _is_pandas_df(table):
					df = table
				else:
					df = table.to_pandas()  # type: ignore[attr-defined]
				names = [str(x) for x in df.iloc[:, 0].tolist()]
			except Exception:
				# Arrow Table without pandas
				col0 = table.column(0)  # type: ignore[attr-defined]
				names = [str(col0[i].as_py()) for i in range(len(col0))]
			return _json_content({"tables": names, "source": q})
		except Exception as e:
			last_err = str(e)
			continue
	raise RuntimeError(f"Failed to list tables: {last_err}")


@mcp.tool()
def list_tables_with_schema(include_system: bool = False) -> str:
	"""List tables with their schemas from information_schema."""
	client = conn.client()
	if include_system:
		q = "SELECT table_schema, table_name FROM information_schema.tables ORDER BY table_schema, table_name"
	else:
		q = (
			"SELECT table_schema, table_name FROM information_schema.tables "
			"WHERE table_schema NOT IN ('information_schema','system') "
			"ORDER BY table_schema, table_name"
		)
	res = client.query(query=q, language="sql")
	table_obj = _as_table(res)
	rows = _table_to_rows(table_obj)
	return _json_content({"rows": rows})


@mcp.tool()
def get_columns(table: str) -> str:
	"""Get column names and data types for a given table/measurement."""
	client = conn.client()
	q = (
		"SELECT table_name, column_name, data_type FROM information_schema.columns "
		f"WHERE table_name = '{table}' ORDER BY ordinal_position"
	)
	reader = client.query(query=q, language="sql")
	table_obj = _as_table(reader)
	try:
		if _is_pandas_df(table_obj):
			df = table_obj
		else:
			df = table_obj.to_pandas()
		rows = df.to_dict(orient="records")
	except Exception:
		# Arrow table -> list of dicts
		rows = _table_to_rows(table_obj)
	return _json_content({"table": table, "columns": rows})


@mcp.tool()
def run_sql(query: str, language: str = "sql", max_rows: int = 100) -> str:
	"""Run an InfluxDB query (SQL or InfluxQL). Returns up to max_rows rows as JSON.

	Parameters:
	- query: SQL or InfluxQL query string
	- language: "sql" (default) or "influxql"
	- max_rows: Maximum number of rows to return (default 100)
	"""
	client = conn.client()
	reader = client.query(query=query, language=language)
	table_obj = _as_table(reader)

	rows = _table_to_rows(table_obj, max_rows=max_rows)
	return _json_content({"rows": rows, "row_count": len(rows)})


@mcp.tool()
def validate_sql(query: str, language: str = "sql") -> str:
	"""Validate a query without returning full results.

	For SQL, attempts EXPLAIN. If EXPLAIN isn't supported, runs the query with LIMIT 0.
	For InfluxQL, tries LIMIT 1 to minimally validate.
	Returns the plan (if available) or success/error details.
	"""
	client = conn.client()
	if language.lower() == "sql":
		try:
			explain_q = f"EXPLAIN {query}"
			reader = client.query(query=explain_q, language="sql")
			table_obj = _as_table(reader)
			try:
				if _is_pandas_df(table_obj):
					df = table_obj
				else:
					df = table_obj.to_pandas()
				plan = "\n".join(df.iloc[:, 0].astype(str).tolist())
			except Exception:
				# Arrow table; attempt to read first column as plan lines
				col0 = table_obj.column(0)  # type: ignore[attr-defined]
				plan = "\n".join(str(col0[i].as_py()) for i in range(len(col0)))
			return _json_content({"ok": True, "method": "EXPLAIN", "plan": plan})
		except Exception as e:
			# Fallback minimal exec
			try:
				limited = f"SELECT * FROM ({query}) LIMIT 0"
				client.query(query=limited, language="sql")
				return _json_content({"ok": True, "method": "LIMIT 0"})
			except Exception as e2:
				return _json_content({"ok": False, "error": str(e2), "fallback": str(e)})
	else:  # influxql
		try:
			# Try adding LIMIT 1 if not present
			q = query
			if " limit " not in query.lower():
				q = f"{query} LIMIT 1"
			client.query(query=q, language="influxql")
			return _json_content({"ok": True, "method": "LIMIT 1"})
		except Exception as e:
			return _json_content({"ok": False, "error": str(e)})


def _sql_literal(value: str) -> str:
	"""Basic SQL literal escaping for single quotes."""
	return value.replace("'", "''")


@mcp.tool()
def distinct_measurements(
	table: str = "temperature_values",
	location: Optional[str] = None,
	sub_category: Optional[str] = None,
	days: int = 1,
	limit: int = 1000,
) -> str:
	"""Return distinct measurement names filtered by optional location/sub_category within the last N days.

	Parameters:
	- table: Source table/measurement (default 'temperature_values')
	- location: Optional location tag filter
	- sub_category: Optional sub_category tag filter
	- days: Time window size (defaults to last 1 day)
	- limit: Max number of measurement names to return
	"""
	client = conn.client()

	# Build WHERE conditions
	conditions: List[str] = []
	# Time bound to reduce file usage; use 'days' window from now
	conditions.append(f"time >= now() - INTERVAL '{int(days)} days'")
	if location:
		conditions.append(f"location = '{_sql_literal(location)}'")
	if sub_category:
		conditions.append(f"sub_category = '{_sql_literal(sub_category)}'")

	where = ""
	if conditions:
		where = " WHERE " + " AND ".join(conditions)

	q = (
		f"SELECT DISTINCT measurement FROM {table}{where} "
		f"ORDER BY measurement LIMIT {int(limit)}"
	)

	res = client.query(query=q, language="sql")
	table_obj = _as_table(res)
	try:
		if _is_pandas_df(table_obj):
			df = table_obj
		else:
			df = table_obj.to_pandas()
		names = [str(x) for x in df.iloc[:, 0].tolist()]
	except Exception:
		# Arrow path
		col0 = table_obj.column(0)  # type: ignore[attr-defined]
		names = [str(col0[i].as_py()) for i in range(len(col0))]

	return _json_content({
		"measurements": names,
		"table": table,
		"filters": {"location": location, "sub_category": sub_category, "days": days},
	})


@mcp.tool()
def write_data(records: str, table: Optional[str] = None) -> str:
	"""Write data records to InfluxDB3 using line protocol format.
	
	Parameters:
	- records: Line protocol formatted data (one record per line, newline separated)
	  Example: "measurement,tag1=value1 field1=1.0,field2=2.0 1234567890000000000"
	- table: Optional table/measurement name (only used for response, not for write routing)
	
	Returns: Status of the write operation including count of records written
	"""
	try:
		client = conn.client()
		
		# Count records (non-empty lines)
		lines = [line.strip() for line in records.split('\n') if line.strip() and not line.strip().startswith('#')]
		record_count = len(lines)
		
		if record_count == 0:
			return _json_content({"ok": False, "message": "No valid records to write", "records_written": 0})
		
		# Write data using line protocol
		client.write(record=records)
		
		return _json_content({
			"ok": True, 
			"message": f"Successfully wrote {record_count} records",
			"records_written": record_count,
			"table": table
		})
	except Exception as e:
		return _json_content({
			"ok": False, 
			"message": f"Write failed: {e}",
			"records_written": 0,
			"table": table
		})


@mcp.tool()
def insert_data(
	table: str,
	data: str,
	timestamp_column: str = "time",
	tag_columns: Optional[str] = None,
	field_columns: Optional[str] = None
) -> str:
	"""Insert data into InfluxDB3 from JSON array format.
	
	Parameters:
	- table: Measurement/table name
	- data: JSON array of records, e.g., '[{"time": "2024-01-01T00:00:00Z", "temp": 20.5, "location": "kitchen"}]'
	- timestamp_column: Name of the timestamp column (default: "time")
	- tag_columns: Comma-separated list of tag column names (e.g., "location,sensor_id")
	- field_columns: Comma-separated list of field column names (e.g., "temp,humidity"). If not specified, all non-tag, non-timestamp columns are treated as fields
	
	Returns: Status of the insert operation
	"""
	try:
		import pandas as pd
		from datetime import datetime
		
		client = conn.client()
		
		# Parse JSON data
		try:
			records_list = json.loads(data)
			if not isinstance(records_list, list):
				return _json_content({"ok": False, "message": "Data must be a JSON array", "records_written": 0})
		except json.JSONDecodeError as e:
			return _json_content({"ok": False, "message": f"Invalid JSON: {e}", "records_written": 0})
		
		if len(records_list) == 0:
			return _json_content({"ok": False, "message": "No records to insert", "records_written": 0})
		
		# Parse tag and field columns
		tags = [t.strip() for t in tag_columns.split(',')] if tag_columns else []
		fields = [f.strip() for f in field_columns.split(',')] if field_columns else None
		
		# Convert to DataFrame
		df = pd.DataFrame(records_list)
		
		# Validate timestamp column exists
		if timestamp_column not in df.columns:
			return _json_content({
				"ok": False, 
				"message": f"Timestamp column '{timestamp_column}' not found in data",
				"records_written": 0
			})
		
		# If field_columns not specified, use all columns except timestamp and tags
		if fields is None:
			fields = [col for col in df.columns if col != timestamp_column and col not in tags]
		
		# Convert timestamp column to datetime if it's not already
		if not pd.api.types.is_datetime64_any_dtype(df[timestamp_column]):
			df[timestamp_column] = pd.to_datetime(df[timestamp_column])
		
		# Write using the DataFrame method with tags and fields specified
		client.write(
			record=df,
			data_frame_measurement_name=table,
			data_frame_timestamp_column=timestamp_column,
			data_frame_tag_columns=tags if tags else None
		)
		
		return _json_content({
			"ok": True,
			"message": f"Successfully inserted {len(df)} records into {table}",
			"records_written": len(df),
			"table": table,
			"tags": tags,
			"fields": fields
		})
		
	except Exception as e:
		return _json_content({
			"ok": False,
			"message": f"Insert failed: {e}",
			"records_written": 0,
			"table": table
		})


if __name__ == "__main__":
	# FastMCP handles stdio transport and lifecycle
	mcp.run()

