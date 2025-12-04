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

	def _build_client(self) -> Any:
		if InfluxDBClient3 is None:  # Dependencies not installed
			raise RuntimeError(
				f"influxdb3-python client is not installed correctly: {_import_error}"
			)

		host = _get_env_any(["SMARTHOME__INFLUXDB3_HOST", "INFLUXDB3_HOST"])    
		database = _get_env_any(["SMARTHOME__INFLUXDB3_DATABASE", "INFLUXDB3_DATABASE"]) 
		token = _get_env_any(["SMARTHOME__INFLUXDB3_TOKEN", "INFLUXDB3_TOKEN"]) 
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
		return {
			"host": _get_env_any(["SMARTHOME__INFLUXDB3_HOST", "INFLUXDB3_HOST"]),
			"database": _get_env_any(["SMARTHOME__INFLUXDB3_DATABASE", "INFLUXDB3_DATABASE"]),
			"has_token": bool(_get_env_any(["SMARTHOME__INFLUXDB3_TOKEN", "INFLUXDB3_TOKEN"])),
			"enable_gzip": _bool_env("INFLUXDB3_ENABLE_GZIP", True),
			"using_certifi": _bool_env("INFLUXDB3_USE_CERTIFI", False),
			"last_error": self._last_error,
			"deps_loaded": _import_error is None,
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


if __name__ == "__main__":
	# FastMCP handles stdio transport and lifecycle
	mcp.run()

