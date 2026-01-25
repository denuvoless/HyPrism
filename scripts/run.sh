#!/bin/bash

# HyPrism Launcher Script
# This script builds and runs the HyPrism launcher

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Building HyPrism Launcher..."

# Build frontend
echo "Building frontend..."
cd frontend
npm run build
cd ..

# Build and run C# backend
echo "Building backend..."
dotnet build

echo "Starting HyPrism..."
dotnet run
