#!/bin/bash

# ==============================================================================
# Launch Script for Mafia Rumours Service (Linux/macOS)
# ------------------------------------------------------------------------------
# This script builds and runs the .NET application. The application itself
# will load configuration from the .env file in the root directory.
# ==============================================================================

# Exit immediately if any command fails
set -e

# Define the relative path to your main project directory
PROJECT_DIR="MafiaRumoursService"
DLL_NAME="MafiaRumoursService.dll"
# This path assumes a Debug build. Change 'Debug' to 'Release' for production builds.
DLL_PATH="bin/Debug/net9.0/$DLL_NAME" 

# Check if the project directory exists
if [ ! -d "$PROJECT_DIR" ]; then
  echo "Error: Project directory '$PROJECT_DIR' not found."
  echo "Please run this script from the root of your solution."
  exit 1
fi

echo "Building Mafia Rumours Service..."
# Navigate into the project directory and build the application
(cd "$PROJECT_DIR" && dotnet build)

echo "Build complete."
echo "Running Mafia Rumours Service..."

# Navigate into the project directory and run the built DLL
# The application will automatically find and load the .env file.
# All arguments passed to this script are forwarded to the application.
(cd "$PROJECT_DIR" && dotnet "$DLL_PATH" "$@")

echo "Application has been shut down."