# Local AI Agent Setup

Use this setup when you want the MCP server to run on the same machine as your AI agent. It avoids Azure Container Apps costs and lets SQL Server IP restrictions follow your workstation or VPN instead of a cloud outbound IP.

## Storage

`Scripts/Start-MCP-Local.ps1` stores runtime data under:

```text
%LOCALAPPDATA%\mssqlMCP
```

The folder contains:

- `connections.db`: SQLite connection catalog.
- `apikeys.db`: SQLite API key catalog.
- `mssql_mcp_key.dpapi`: local encryption key protected by the current Windows user.
- `mssql_mcp_api_key.dpapi`: local HTTP API key protected by the current Windows user.

The DPAPI files are only usable by the Windows user that created them. Do not commit copied database files or DPAPI files to source control.

The launcher ignores inherited `MSSQL_MCP_KEY` and `MSSQL_MCP_API_KEY` values by default so old remote-server settings do not leak into local mode. Add `-UseEnvironmentSecrets` only when you intentionally want to supply those values yourself.

## First-Time Build

Local clients start an already-built DLL so startup is fast and stdio clients never write build output to stdout before MCP protocol messages.

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" build --no-restore
```

## VS Code / GitHub Copilot Agent

This repo's `.vscode/mcp.json` starts the MCP server through stdio:

```json
{
  "servers": {
    "mssqlmcp-local": {
      "type": "stdio",
      "command": "powershell",
      "args": [
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "${workspaceFolder}\\Scripts\\Start-MCP-Local.ps1",
        "-Transport",
        "Stdio"
      ]
    }
  }
}
```

## Cursor

This repo's `.cursor/mcp.json` uses the same local stdio launcher with an absolute path.

## Codex Desktop

Codex Desktop reads MCP servers from `%USERPROFILE%\.codex\config.toml`. A local stdio setup looks like this:

```toml
[mcp_servers.mssqlmcp-local]
command = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe"
args = ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "C:\\Users\\jiewn\\Documents\\mssql_mcp\\Scripts\\Start-MCP-Local.ps1", "-Transport", "Stdio"]
startup_timeout_sec = 30
tool_timeout_sec = 120
```

After changing this file, start a new Codex thread or restart Codex Desktop so the MCP tool list is loaded again.

## HTTP Clients

For clients configured through HTTP, start the local server first:

```powershell
.\Scripts\Start-MCP-Local.ps1 -Transport Http
```

Then connect the client to:

```text
http://localhost:3001/mcp
```

Use this to print the generated local API key state and paths:

```powershell
.\Scripts\Start-MCP-Local.ps1 -Transport Http -PrintConfig
```

Use this to reveal the local API key when an HTTP MCP client prompts for it:

```powershell
.\Scripts\Start-MCP-Local.ps1 -Transport Http -ShowApiKey
```

The local API key itself is stored encrypted in `%LOCALAPPDATA%\mssqlMCP\mssql_mcp_api_key.dpapi`. If you need to rotate it, delete that file while the server is stopped and restart the launcher.

## Adding Connections

Ask the agent to call:

```text
mssql_add_connection
```

with a SQL Server connection string your local machine can reach. The connection string is encrypted before being stored in the local SQLite catalog.
