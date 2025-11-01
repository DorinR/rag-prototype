#!/bin/bash

# Sync embeddings from local PostgreSQL to production database
# Usage: ./sync-embeddings.sh
# 
# Configuration:
# - Local DB: Reads from rag-experiment/appsettings.Development.json (DefaultConnection)
# - Production DB: Either:
#   1. Set PROD_DB_URL environment variable: 
#      export PROD_DB_URL="postgresql://user:pass@host:port/database"
#   2. Or update ProductionConnection in appsettings.Development.json

# Configuration - Read connection strings from appsettings
echo "📖 Reading configuration from appsettings.Development.json..."

# Check if the file exists
if [ ! -f "rag-experiment/appsettings.Development.json" ]; then
    echo "❌ Error: appsettings.Development.json not found!"
    echo "   Please ensure you're running this script from the project root directory."
    exit 1
fi

# Try to read local connection string
LOCAL_CONNECTION_STRING=$(jq -r '.ConnectionStrings.DefaultConnection // empty' rag-experiment/appsettings.Development.json)

if [ -z "$LOCAL_CONNECTION_STRING" ]; then
    echo "❌ Error: Could not find ConnectionStrings.DefaultConnection in appsettings.Development.json"
    echo "   Please add your database connection string to the file."
    exit 1
fi

# Convert Entity Framework connection string format to PostgreSQL URI format if needed
if [[ "$LOCAL_CONNECTION_STRING" == *"Host="* ]]; then
    echo "🔄 Converting EF connection string to PostgreSQL URI format..."
    
    # Extract values using sed/grep
    HOST=$(echo "$LOCAL_CONNECTION_STRING" | sed -n 's/.*Host=\([^;]*\).*/\1/p')
    DATABASE=$(echo "$LOCAL_CONNECTION_STRING" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
    USERNAME=$(echo "$LOCAL_CONNECTION_STRING" | sed -n 's/.*Username=\([^;]*\).*/\1/p')
    PASSWORD=$(echo "$LOCAL_CONNECTION_STRING" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
    PORT=${PORT:-5432}
    
    LOCAL_CONNECTION_STRING="postgresql://$USERNAME:$PASSWORD@$HOST:$PORT/$DATABASE"
    echo "✅ Converted to: postgresql://$USERNAME:***@$HOST:$PORT/$DATABASE"
fi

# Production connection string - try environment variable first, then appsettings
PROD_CONNECTION_STRING=""

if [ -n "$PROD_DB_URL" ]; then
    PROD_CONNECTION_STRING=$PROD_DB_URL
    echo "✅ Using production connection string from PROD_DB_URL environment variable"
else
    # Try to read from appsettings
    PROD_CONNECTION_STRING=$(jq -r '.ConnectionStrings.ProductionConnection // empty' rag-experiment/appsettings.Development.json)
    
    if [ -n "$PROD_CONNECTION_STRING" ] && [ "$PROD_CONNECTION_STRING" != "postgresql://username:password@your-production-host.com:5432/your_database" ]; then
        echo "✅ Using production connection string from appsettings.Development.json"
    else
        echo "❌ Error: No production database URL found!"
        echo "   Please either:"
        echo "   1. Set PROD_DB_URL environment variable:"
        echo "      export PROD_DB_URL=\"postgresql://user:pass@host:port/database\""
        echo "   2. Or update ProductionConnection in appsettings.Development.json"
        exit 1
    fi
fi

set -e  # Exit on any error

echo "🚀 Starting embedding sync from local to production..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}📊 Dumping documents and embeddings from local database...${NC}"
pg_dump $LOCAL_CONNECTION_STRING \
  --table='public."Documents"' \
  --table='public."Embeddings"' \
  --data-only \
  --column-inserts \
  --file=embeddings_dump.sql

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✅ Local dump completed successfully${NC}"
else
  echo -e "${RED}❌ Local dump failed${NC}"
  exit 1
fi

echo -e "${YELLOW}🗑️  Clearing existing data from production tables...${NC}"
psql $PROD_CONNECTION_STRING -c "TRUNCATE TABLE \"MessageSources\", \"Messages\", \"Conversations\", \"Embeddings\", \"Documents\" RESTART IDENTITY CASCADE;"

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✅ Tables cleared successfully${NC}"
else
  echo -e "${RED}❌ Failed to clear tables${NC}"
  exit 1
fi

echo -e "${YELLOW}🌐 Restoring data to production database...${NC}"
psql $PROD_CONNECTION_STRING -f embeddings_dump.sql

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✅ Production restore completed successfully${NC}"
  echo -e "${GREEN}🎉 Embedding sync complete!${NC}"
  
  # Clean up dump file
  rm embeddings_dump.sql
  echo "🧹 Cleaned up temporary dump file"
else
  echo -e "${RED}❌ Production restore failed${NC}"
  echo "💾 Dump file saved as embeddings_dump.sql for manual inspection"
  exit 1
fi
