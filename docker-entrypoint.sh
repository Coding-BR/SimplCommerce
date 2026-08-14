#!/bin/bash
set -e

DB_HOST=${DB_HOST:-simpldb}
DB_USER=${DB_USER:-postgres}
export PGPASSWORD=${PGPASSWORD:-postgres}

if psql -h "$DB_HOST" --username "$DB_USER" -lqt | cut -d \| -f 1 | grep -qw simplcommerce; then
    echo "simplcommerce database existed"
else
    echo "create new database simplcommerce"
	psql -h "$DB_HOST" --username "$DB_USER" -c "CREATE DATABASE simplcommerce WITH ENCODING 'UTF8'"
	psql -h "$DB_HOST" --username "$DB_USER" -d simplcommerce -a -f /app/dbscript.sql
fi

cd /app && dotnet SimplCommerce.WebHost.dll
