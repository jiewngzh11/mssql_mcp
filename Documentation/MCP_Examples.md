# MCP Examples

Examples of configuring and runing this MCP Server in different modes.

## SQL Server MCP Configuration Example for starting local MCP Server via http

This is an example of how to start a local http based MCP server for SQL Server. Use this method if you want to run the MCP server locally on your development machine for testing or development purposes. It can also be used as a way to run the MCP server when you have multiple local applications that need to connect to the MCP server.

### Start MCP Server

First navigate to the folder where you have cloned the `https://github.com/MCPRUNNER/mssqlMCP.git` repository. Then set the required environment variables for your MCP Key and API Key. Finally, run the MCP server using the `dotnet run` command.

```powershell

cd "C:\Path\To\mssql-mcp-server"
$env:MSSQL_MCP_KEY = "abcd1234567891-UseSomethingElseForYourMCPKey="
$env:MSSQL_MCP_API_KEY = "efghij1234567891-UseSomethingElseForYourAPIKey="
$env:MSSQL_MCP_TRANSPORT = "http"

# Additional environment variables can be set here as needed
$env:MSSQL_MCP_DATA = "C:\Path\To\where\you\want\data\settings\db\stored"
# or will default to AppData\Local\mssqlMCP folder
# Logs will default to /app/Data in docker container  or inappsettings.json

dotnet run --no-launch-profile --no-build --project mssqlMCP.csproj

```

Next, you can configure your VSCode MCP configuration file to connect to your local MCP server.
Set Bearer token authorization header using the API Key you set in the environment variable above.
The `mssql-server-mcp-key` value can be user as the Authorization header if you want full administrative control inside the MCP server.  
The `mssql-server-mcp-api-key` value is used as the Authorization header if you want to access as a normal user.

```json [.vscode/mcp.json]
{
  "inputs": [
    {
      "id": "mssql-server-mcp-api-key",
      "type": "promptString",
      "description": "Enter your SQL Server MCP API Key",
      "password": true
    },
    {
      "id": "mssql-server-mcp-key",
      "type": "promptString",
      "description": "Enter your SQL Server MCP Key",
      "password": true
    }
  ],
  "servers": {
    "sql-server-mcp": {
      "url": "http://localhost:3001/mcp",
      "headers": {
        "Authorization": "Bearer ${input:mssql-server-mcp-api-key}",
        "Content-Type": "application/json"
      }
    }
  }
}
```

## SQL Server MCP Configuration Example for starting local MCP Server via stdio

This is an example of how to start a local stdio-based MCP server for SQL Server. Use this method when you want to run the MCP server as a direct process that communicates through standard input/output streams. This is typically used when integrating with applications that spawn the MCP server as a child process.

### VSCode MCP Configuration for stdio

For stdio-based MCP servers, configure your VSCode MCP configuration file to launch the server as a command. The server will communicate through stdin/stdout with the MCP client.

```json [.vscode/mcp.json]
{
  "servers": {
    "sql-server-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--no-launch-profile",
        "--no-build",
        "--project",
        "c:\\path\\to\\mssqlMCP.csproj"
      ],
      "env": {
        "MSSQL_MCP_KEY": "abcd1234567891-UseSomethingElseForYourMCPKey=",
        "MSSQL_MCP_API_KEY": "efghij1234567891-UseSomethingElseForYourAPIKey=",
        "MSSQL_MCP_TRANSPORT": "stdio"
      }
    }
  }
}
```

## SQL Server MCP Configuration Example for connecting to a remote MCP Server via http

### VSCode MCP Configuration for http remote server

Set Bearer token authorization header using the API Key you set in the environment variable above.
The `mssql-server-mcp-key` value can be user as the Authorization header if you want full administrative control inside the MCP server.  
The `mssql-server-mcp-api-key` value is used as the Authorization header if you want to access as a normal user.

```json
{
  "inputs": [
    {
      "id": "mssql-server-mcp-api-key",
      "type": "promptString",
      "description": "Enter your SQL Server MCP API Key",
      "password": true
    },
    ,
    {
      "id": "mssql-server-mcp-key",
      "type": "promptString",
      "description": "Enter your SQL Server MCP Key",
      "password": true
    }
  ],
  "servers": {
    "sql-server-mcp-remote": {
      "url": "https://your-remote-mcp-server.com/mcp",
      "headers": {
        "Authorization": "Bearer ${input:mssql-server-mcp-api-key}",
        "Content-Type": "application/json"
      }
    }
  }
}
```
