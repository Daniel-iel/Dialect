#!/bin/bash
set -e

# SQL Server initialization script
# Start SQL Server in the background
echo "Starting SQL Server..."
/opt/mssql/bin/sqlservr &
SERVER_PID=$!

# Wait for SQL Server to be ready
echo "Waiting for SQL Server to be ready..."
count=0
while [ $count -lt 60 ]; do
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "P@ssw0rd!" -C -Q "SELECT 1" > /dev/null 2>&1
    if [ $? -eq 0 ]; then
        echo "SQL Server is ready!"
        break
    fi
    count=$((count + 1))
    echo "Waiting... ($count/60)"
    sleep 1
done

# Execute initialization script
if [ -f /var/opt/mssql/init.sql ]; then
    echo "Executing initialization script..."
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "P@ssw0rd!" -C -i /var/opt/mssql/init.sql
    echo "Initialization script completed!"
else
    echo "Warning: /var/opt/mssql/init.sql not found"
fi

# Keep the process running
wait $SERVER_PID
