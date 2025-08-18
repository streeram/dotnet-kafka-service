# Build System

This project uses [Nuke](https://nuke.build/) as the build automation system. Nuke provides a powerful, cross-platform build system for .NET projects.

## Quick Start

### Using the build.sh script (Recommended)

The `build.sh` script provides an easy way to execute common build operations:

```bash
# Run the complete build pipeline (clean, restore, format, build, test, publish)
./build.sh all

# Run specific targets
./build.sh clean
./build.sh restore
./build.sh format
./build.sh build
./build.sh test
./build.sh publish

# Build with specific configuration
./build.sh build --configuration Release
./build.sh all -c Release
```

### Using Nuke directly

You can also run Nuke directly:

```bash
# Run the complete build pipeline
dotnet run --project build/Build.csproj -- BuildAll

# Run specific targets
dotnet run --project build/Build.csproj -- Clean
dotnet run --project build/Build.csproj -- Restore
dotnet run --project build/Build.csproj -- Format
dotnet run --project build/Build.csproj -- Compile
dotnet run --project build/Build.csproj -- Test
dotnet run --project build/Build.csproj -- Publish

# With configuration
dotnet run --project build/Build.csproj -- Compile --configuration Release
```

## Available Targets

- **Clean**: Removes build artifacts (bin/obj folders) and cleans the artifacts directory
- **Restore**: Restores NuGet packages for the solution
- **Format**: Formats code using `dotnet format`
- **Compile**: Builds the solution
- **Test**: Runs all tests in the solution
- **Publish**: Publishes API and Service projects to the artifacts directory
- **BuildAll**: Executes the complete pipeline (Clean → Restore → Format → Compile → Test → Publish)

## Build Configuration

The build system supports both Debug and Release configurations:
- **Debug**: Default for local development
- **Release**: Default for CI/CD environments

## Artifacts

Published applications are output to the `artifacts/` directory, with each project in its own subdirectory.

## Prerequisites

- .NET 9.0 SDK
- Git (for GitRepository information)

## IDE Integration

Nuke provides excellent IDE integration:
- **Visual Studio**: Install the Nuke extension
- **JetBrains Rider**: Built-in support
- **VS Code**: Install the Nuke extension

For more information, visit the [official Nuke documentation](https://nuke.build/).