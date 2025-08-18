#!/bin/bash

# Nuke Build Script for KafkaConfluentCloud Solution
# This script provides easy access to common build operations

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [target] [options]"
    echo ""
    echo "Available targets:"
    echo "  clean      - Clean build artifacts"
    echo "  restore    - Restore NuGet packages"
    echo "  format     - Format code using dotnet format"
    echo "  build      - Build the solution"
    echo "  test       - Run tests"
    echo "  publish    - Publish applications"
    echo "  all        - Run complete build pipeline (clean, restore, format, build, test, publish)"
    echo ""
    echo "Options:"
    echo "  --configuration|-c [Debug|Release]  - Build configuration (default: Debug)"
    echo "  --help|-h                          - Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 all                    # Run complete build pipeline"
    echo "  $0 build -c Release       # Build in Release configuration"
    echo "  $0 test                   # Run tests only"
}

# Default values
TARGET="all"
CONFIGURATION="Debug"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        clean|restore|format|build|test|publish|all)
            TARGET="$1"
            shift
            ;;
        --configuration|-c)
            CONFIGURATION="$2"
            shift 2
            ;;
        --help|-h)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate configuration
if [[ "$CONFIGURATION" != "Debug" && "$CONFIGURATION" != "Release" ]]; then
    print_error "Invalid configuration: $CONFIGURATION. Must be Debug or Release."
    exit 1
fi

print_info "Starting Nuke build..."
print_info "Target: $TARGET"
print_info "Configuration: $CONFIGURATION"

# Change to the directory containing this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET CLI is not installed or not in PATH"
    exit 1
fi

# Map targets to Nuke targets
case $TARGET in
    "all")
        NUKE_TARGET="BuildAll"
        ;;
    "clean")
        NUKE_TARGET="Clean"
        ;;
    "restore")
        NUKE_TARGET="Restore"
        ;;
    "format")
        NUKE_TARGET="Format"
        ;;
    "build")
        NUKE_TARGET="Compile"
        ;;
    "test")
        NUKE_TARGET="Test"
        ;;
    "publish")
        NUKE_TARGET="Publish"
        ;;
esac

# Execute the build
print_info "Executing: dotnet run --project build/Build.csproj -- $NUKE_TARGET --configuration $CONFIGURATION"

if dotnet run --project build/Build.csproj -- "$NUKE_TARGET" --configuration "$CONFIGURATION"; then
    print_success "Build completed successfully!"
else
    print_error "Build failed!"
    exit 1
fi