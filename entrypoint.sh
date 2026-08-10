#!/bin/bash
# Docker entrypoint: waits until SQL Server accepts TCP connections, then
# starts the API. Deployment-layer only - no application code is changed.
set -e

DB_HOST="${DB_HOST:-db}"
DB_PORT="${DB_PORT:-1433}"
MAX_ATTEMPTS="${DB_WAIT_ATTEMPTS:-60}" # 60 x 3s = up to 3 minutes of waiting
attempt=0

echo "[entrypoint] Waiting for SQL Server at ${DB_HOST}:${DB_PORT} ..."

until (echo > "/dev/tcp/${DB_HOST}/${DB_PORT}") 2>/dev/null; do
  attempt=$((attempt + 1))
  if [ "${attempt}" -ge "${MAX_ATTEMPTS}" ]; then
    echo "[entrypoint] ERROR: SQL Server not reachable after ${MAX_ATTEMPTS} attempts. Exiting."
    exit 1
  fi
  echo "[entrypoint] SQL Server not ready (attempt ${attempt}/${MAX_ATTEMPTS}), retrying in 3s..."
  sleep 3
done

echo "[entrypoint] SQL Server is reachable. Starting CbeMeetingAttendance.dll ..."
exec dotnet CbeMeetingAttendance.dll
