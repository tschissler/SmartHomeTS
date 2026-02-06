#!/bin/bash
# Load environment variables from .env file (Linux/Mac bash script)
# Usage: source load_env.sh

if [ ! -f ".env" ]; then
    echo "Error: .env file not found!"
    echo "Please copy .env.example to .env and fill in your credentials."
    return 1 2>/dev/null || exit 1
fi

echo "Loading environment variables from .env file..."

# Export variables from .env
set -a
source .env
set +a

echo ""
echo "Environment variables loaded successfully!"
echo ""
echo "You can now run the migration:"
echo "  python migrate.py --config config.yaml"
echo ""
