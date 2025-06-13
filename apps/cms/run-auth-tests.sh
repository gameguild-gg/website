#!/bin/bash

# Auth Module Test Runner Script
echo "=== Running Auth Module Tests ==="

cd "w:/repositories/game-guild/game-guild/apps/cms"

echo "Building the project..."
dotnet build

if [ $? -eq 0 ]; then
    echo "Build successful! Running auth module tests..."
    
    echo ""
    echo "=== Auth Service Tests ==="
    dotnet test --filter "FullyQualifiedName~AuthServiceTests" --logger "console;verbosity=normal"
    
    echo ""
    echo "=== JWT Token Service Tests ==="
    dotnet test --filter "FullyQualifiedName~JwtTokenServiceTests" --logger "console;verbosity=normal"
    
    echo ""
    echo "=== Web3 Service Tests ==="
    dotnet test --filter "FullyQualifiedName~Web3ServiceTests" --logger "console;verbosity=normal"
    
    echo ""
    echo "=== Auth Controller Tests ==="
    dotnet test --filter "FullyQualifiedName~AuthControllerTests" --logger "console;verbosity=normal"
    
    echo ""
    echo "=== Auth Integration Tests ==="
    dotnet test --filter "FullyQualifiedName~AuthIntegrationTests" --logger "console;verbosity=normal"
    
    echo ""
    echo "=== All Auth Tests ==="
    dotnet test --filter "FullyQualifiedName~cms.Tests.Modules.Auth" --logger "console;verbosity=normal"
    
else
    echo "Build failed. Please check for compilation errors."
    exit 1
fi

echo "=== Auth Module Test run completed ==="
