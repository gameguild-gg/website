#!/bin/bash

# Test runner script for the CMS BaseEntity tests
echo "=== Running BaseEntity Tests ==="

cd "w:/repositories/game-guild/game-guild/apps/cms"

echo "Building the project..."
dotnet build

if [ $? -eq 0 ]; then
    echo "Build successful! Running tests..."
    dotnet test --logger "console;verbosity=detailed"
else
    echo "Build failed. Please check for compilation errors."
    exit 1
fi

echo "=== Test run completed ==="
