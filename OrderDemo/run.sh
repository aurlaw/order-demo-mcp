#!/usr/bin/env bash
set -euo pipefail

API_DIR="$(dirname "$0")/OrderDemo.Api"
MCP_DIR="$(dirname "$0")/OrderDemo.Mcp"

cleanup() {
    echo ""
    echo "Stopping servers..."
    kill "$API_PID" "$MCP_PID" 2>/dev/null || true
    wait "$API_PID" "$MCP_PID" 2>/dev/null || true
}
trap cleanup SIGINT SIGTERM

echo "Starting OrderDemo.Api on http://localhost:5000..."
dotnet run --project "$API_DIR" &
API_PID=$!

echo "Waiting for API to be ready..."
until curl -sf http://localhost:5000/scalar > /dev/null 2>&1; do
    sleep 1
done

echo "Starting OrderDemo.Mcp on http://localhost:5010..."
dotnet run --project "$MCP_DIR" &
MCP_PID=$!

echo ""
echo "  API:  http://localhost:5000/scalar"
echo "  MCP:  http://localhost:5010"
echo ""
echo "Press Ctrl+C to stop both servers."

wait
