# InfluxDB 3 MCP Server

This MCP server exposes tools to discover schema and run SQL/InfluxQL queries against your InfluxDB 3 (Core or Cloud) database so your AI agent can propose and validate queries automatically.

## Configure

Set the following environment variables for the server process:

- INFLUXDB3_HOST: Base URL of your InfluxDB 3 endpoint (e.g. `http://localhost:8181` for Core, or `https://<region>.aws.cloud2.influxdata.com` for Cloud/Dedicated)
- INFLUXDB3_DATABASE: Database/bucket name to query
- INFLUXDB3_TOKEN: (Optional) Auth token if required by your instance
- INFLUXDB3_ENABLE_GZIP: (Optional, default `true`) Enable gzip
- INFLUXDB3_USE_CERTIFI: (Optional, default `false`) Set to `1` on Windows if gRPC TLS needs explicit root certs

Example (PowerShell):

```powershell
$env:INFLUXDB3_HOST = "http://localhost:8181"
$env:INFLUXDB3_DATABASE = "mydb"
$env:INFLUXDB3_TOKEN = ""
```

## Install

From this folder:

```powershell
python -m pip install -r requirements.txt
```

## Run (standalone)

The server uses stdio. You can run it for quick health checks:

```powershell
python .\server.py
```

It will wait for an MCP client to connect. To stop, press Ctrl+C.

## Tools

- health(): Returns config and a basic connectivity check
- list_tables(): Lists available tables/measurements
- get_columns(table): Returns columns and data types for a specific table
- run_sql(query, language = "sql", max_rows = 100): Executes a query and returns rows as JSON
- validate_sql(query, language = "sql"): Validates a query via EXPLAIN (SQL) or LIMIT (InfluxQL)

## VS Code Agent Configuration (example)

Add this server to your MCP-capable client configuration. Example for VS Code Copilot Chat (User Settings JSON):

```json
{
  "github.copilot.chat.modelContextProtocol.tools": {
    "influxdb3": {
      "command": "python",
      "args": [
        "${workspaceFolder}/MCPServer/server.py"
      ],
      "env": {
        "INFLUXDB3_HOST": "http://localhost:8181",
        "INFLUXDB3_DATABASE": "mydb",
        "INFLUXDB3_TOKEN": ""
      }
    }
  }
}
```

Adjust the path and environment variables to your setup.
