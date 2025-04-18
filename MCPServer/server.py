# MCPServer main entry point
from fastapi import FastAPI
from fastapi.responses import JSONResponse
import os
from influxdb_client import InfluxDBClient
from influxdb_client.client.exceptions import InfluxDBError
from requests.exceptions import ReadTimeout
from datetime import datetime

app = FastAPI()

# Read InfluxDB connection info from environment variables
INFLUXDB_URL = os.getenv("INFLUXDB_URL")
INFLUXDB_TOKEN = os.getenv("INFLUXDB_TOKEN")
INFLUXDB_ORG = os.getenv("INFLUXDB_ORG")

if not all([INFLUXDB_URL, INFLUXDB_TOKEN, INFLUXDB_ORG]):
    raise RuntimeError("Missing one or more InfluxDB environment variables: INFLUXDB_URL, INFLUXDB_TOKEN, INFLUXDB_ORG")

# Create InfluxDB client
influx_client = InfluxDBClient(
    url=INFLUXDB_URL,
    token=INFLUXDB_TOKEN,
    org=INFLUXDB_ORG
)

@app.get("/health")
def health_check():
    return JSONResponse(content={"status": "ok"})

@app.get("/schema")
def get_schema():
    """Return buckets, measurements, fields, and tags."""
    try:
        buckets_api = influx_client.buckets_api()
        buckets = buckets_api.find_buckets().buckets
        result = []
        for bucket in buckets:
            bucket_name = bucket.name
            query_api = influx_client.query_api()
            # Get measurements for this bucket
            query = f'import "influxdata/influxdb/schema"\nschema.measurements(bucket: "{bucket_name}")'
            measurements = [r["_value"] for r in query_api.query(org=INFLUXDB_ORG, query=query)[0].records]
            bucket_info = {"bucket": bucket_name, "measurements": []}
            for m in measurements:
                # Get fields using predicate
                query_fields = f'import "influxdata/influxdb/schema"\nschema.fieldKeys(bucket: "{bucket_name}", predicate: (r) => r._measurement == "{m}")'
                fields = [r["_value"] for r in query_api.query(org=INFLUXDB_ORG, query=query_fields)[0].records]
                # Get tags using predicate
                query_tags = f'import "influxdata/influxdb/schema"\nschema.tagKeys(bucket: "{bucket_name}", predicate: (r) => r._measurement == "{m}")'
                tags = [r["_value"] for r in query_api.query(org=INFLUXDB_ORG, query=query_tags)[0].records]
                bucket_info["measurements"].append({
                    "measurement": m,
                    "fields": fields,
                    "tags": tags
                })
            result.append(bucket_info)
        return result
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/buckets")
def get_buckets():
    """Return a list of all available bucket names."""
    try:
        buckets_api = influx_client.buckets_api()
        buckets = buckets_api.find_buckets().buckets
        bucket_names = [bucket.name for bucket in buckets]
        return bucket_names
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/sample")
def get_sample(bucket: str, measurement: str, limit: int = 5):
    """Return recent sample data for a measurement."""
    try:
        query_api = influx_client.query_api()
        query = f'from(bucket: "{bucket}")\n  |> range(start: -1d)\n  |> filter(fn: (r) => r._measurement == "{measurement}")\n  |> limit(n: {limit})'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        samples = []
        for table in tables:
            for record in table.records:
                samples.append(record.values)
        return samples
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/measurements")
def get_measurements(bucket: str):
    """Return all measurements in a specific bucket."""
    try:
        query_api = influx_client.query_api()
        query = f'import "influxdata/influxdb/schema"\nschema.measurements(bucket: "{bucket}")'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        measurements = [r["_value"] for r in tables[0].records] if tables else []
        return measurements
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/fields")
def get_fields(bucket: str, measurement: str):
    """Return all field keys for a given measurement in a bucket."""
    try:
        query_api = influx_client.query_api()
        query = f'import "influxdata/influxdb/schema"\nschema.fieldKeys(bucket: "{bucket}", predicate: (r) => r._measurement == "{measurement}")'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        fields = [r["_value"] for r in tables[0].records] if tables else []
        return fields
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/tags")
def get_tags(bucket: str, measurement: str):
    """Return all tag keys for a given measurement in a bucket."""
    try:
        query_api = influx_client.query_api()
        query = f'import "influxdata/influxdb/schema"\nschema.tagKeys(bucket: "{bucket}", predicate: (r) => r._measurement == "{measurement}")'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        tags = [r["_value"] for r in tables[0].records] if tables else []
        return tags
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/tag-values")
def get_tag_values(bucket: str, measurement: str, tag: str):
    """Return all possible values for a specific tag key in a measurement."""
    try:
        query_api = influx_client.query_api()
        query = f'import "influxdata/influxdb/schema"\nschema.tagValues(bucket: "{bucket}", predicate: (r) => r._measurement == "{measurement}", tag: "{tag}")'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        values = [r["_value"] for r in tables[0].records] if tables else []
        return values
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/field-types")
def get_field_types(bucket: str, measurement: str):
    """Return the data types of each field in a measurement (if available)."""
    try:
        query_api = influx_client.query_api()
        query = f'import "influxdata/influxdb/schema"\nschema.fieldKeys(bucket: "{bucket}", predicate: (r) => r._measurement == "{measurement}")'
        tables = query_api.query(org=INFLUXDB_ORG, query=query)
        field_types = []
        if tables:
            for r in tables[0].records:
                field_types.append({"field": r["_value"], "type": r.get("type", "unknown")})
        return field_types
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})

@app.get("/time-range")
def get_time_range(bucket: str, measurement: str, days: int = 365):
    """
    Return the earliest and latest timestamps for a measurement within the last `days` days.
    """
    try:
        query_api = influx_client.query_api()
        # Query for min _time
        query_min = (
            f'from(bucket: "{bucket}")\n'
            f'  |> range(start: -{days}d)\n'
            f'  |> filter(fn: (r) => r._measurement == "{measurement}")\n'
            '  |> keep(columns: ["_time"])\n'
            '  |> min(column: "_time")\n'
        )
        min_tables = query_api.query(org=INFLUXDB_ORG, query=query_min)
        min_time = None
        if min_tables and min_tables[0].records:
            min_time = str(min_tables[0].records[0]['_time'])

        # Query for max _time
        query_max = (
            f'from(bucket: "{bucket}")\n'
            f'  |> range(start: -{days}d)\n'
            f'  |> filter(fn: (r) => r._measurement == "{measurement}")\n'
            '  |> keep(columns: ["_time"])\n'
            '  |> max(column: "_time")\n'
        )
        max_tables = query_api.query(org=INFLUXDB_ORG, query=query_max)
        max_time = None
        if max_tables and max_tables[0].records:
            max_time = str(max_tables[0].records[0]['_time'])

        return {"min_time": min_time, "max_time": max_time, "window_days": days}
    except ReadTimeout:
        return JSONResponse(status_code=504, content={"error": "Query timed out. Try a smaller bucket, measurement, or shorter time window."})
    except InfluxDBError as e:
        return JSONResponse(status_code=500, content={"error": str(e)})
    except Exception as e:
        import traceback
        return JSONResponse(status_code=500, content={"error": str(e), "traceback": traceback.format_exc()})

@app.get("/version")
def get_influxdb_version():
    """Return the version of the connected InfluxDB instance."""
    try:
        health = influx_client.health()
        version = health.version if hasattr(health, 'version') else None
        return {"version": version}
    except Exception as e:
        return JSONResponse(status_code=500, content={"error": str(e)})
