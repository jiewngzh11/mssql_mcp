@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-MCP-Local.ps1" -Transport Stdio
