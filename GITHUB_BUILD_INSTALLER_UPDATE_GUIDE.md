# GitHub Build, Installer, and Auto-Update Implementation Guide

This guide documents how to implement automated GitHub releases with Velopack installer and auto-update functionality in a .NET project.

## Overview

This implementation uses:
- **GitHub Actions** for automated builds and releases
- **Velopack** for creating Windows installers and managing updates
- **GitHub Releases** as the update distribution mechanism

## Prerequisites

- .NET 8.0 or higher project
- GitHub repository
- Windows application (console or GUI)

## Step 1: Install Velopack Package

Add the Velopack NuGet package to your project:

```xml
<PackageReference Include="Velopack" Version="0.0.1298" />
```

**Note:** Check for the latest version at [nuget.org/packages/Velopack](https://www.nuget.org/packages/Velopack)

## Step 2: Configure Project File

Add update configuration to your `.csproj` file:

```xml
<PropertyGroup>
  <UpdateEnabled>true</UpdateEnabled>
  <UpdateMode>Background</UpdateMode>
  <UpdateInterval>1</UpdateInterval>
  <UpdateIntervalUnits>Hours</UpdateIntervalUnits>
</PropertyGroup>
```

These settings enable background update checking with a configurable interval.

## Step 3: Add Update Logic to Your Application

In your `Program.cs` (or main entry point), add update checking code:

```csharp
using Velopack;

public class Program
{
    private const string GithubOwner = "your-username";
    private const string GithubRepo = "your-repo-name";

    public static int Main(string[] args)
    {
        // Initialize Velopack
        VelopackApp.Build().Run();
        
        try
        {
            var updateSource = $"https://github.com/{GithubOwner}/{GithubRepo}/releases/latest/download/";
            var mgr = new UpdateManager(updateSource);

            // Check for new version
            var newVersion = mgr.CheckForUpdates();
            if (newVersion != null)
            {
                Console.WriteLine($"Updating to version {newVersion.TargetFullRelease.Version}");

                // Download new version
                mgr.DownloadUpdates(newVersion);

                // Install new version and restart app
                mgr.ApplyUpdatesAndRestart(newVersion);
                Console.WriteLine("Update complete! Please restart the application.");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update check failed: {ex.Message}");
        }

        // Your application code continues here
        // ...
        
        return 0;
    }
}
```

**Important:** Replace `GithubOwner` and `GithubRepo` with your actual GitHub username and repository name.

## Step 4: Create GitHub Actions Workflow

Create `.github/workflows/release.yml` in your repository:

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest
    
    env:
      Solution_Name: YourSolution.sln
      Project_Path: YourProject/YourProject.csproj

    steps:
    - name: Extract Version
      id: extract_version
      shell: bash
      run: |
        VERSION=$(echo "${GITHUB_REF_NAME}" | cut -c2-)
        echo "version=$VERSION" >> $GITHUB_OUTPUT

    - uses: actions/checkout@v3  
      with:
        fetch-depth: 0

    - name: Setup .NET    
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies    
      run: dotnet restore ${{ env.Solution_Name }}

    - name: Build  
      run: dotnet build ${{ env.Solution_Name }} --configuration Release --no-restore

    - name: Publish  
      run: dotnet publish ${{ env.Project_Path }} --self-contained -r win-x64 -o YourProject/publish

    - name: Install Velopack CLI  
      run: dotnet tool install --global vpk
  
    - name: Create Velopack Release
      run: |
        vpk pack --packId "YourAppId" --packVersion "${{ steps.extract_version.outputs.version }}" --packDir "YourProject/publish" --mainExe "YourApp.exe"

    - name: Log Releases Directory Listing
      run: |
        echo "Contents of Releases:"
        Get-ChildItem -Recurse

    - name: Create Release and Upload Assets
      uses: softprops/action-gh-release@v2
      with:
        tag_name: ${{ github.ref }}
        name: Release ${{ steps.extract_version.outputs.version }}
        draft: false
        prerelease: false
        files: |
          Releases/*
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Replace the following:**
- `YourSolution.sln` with your solution file name
- `YourProject/YourProject.csproj` with your project path
- `YourProject/publish` with your desired publish directory
- `YourAppId` with your application ID (no spaces)
- `YourApp.exe` with your executable name

## Step 5: Create NuSpec File (Optional)

Create `YourProject.nuspec` for additional metadata:

```xml
<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>YourAppId</id>
    <version>0.0.0</version>
    <title>Your Application Name</title>
    <authors>Your Name</authors>
    <description>Your application description</description>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
  <files>
    <file src="publish\**" target="lib\net8.0" />
  </files>
</package>
```

## Step 6: Add VS Code Build Tasks (Optional)

Create or update `.vscode/tasks.json` for local builds:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build: publish",
      "type": "shell",
      "command": "dotnet publish YourProject/YourProject.csproj --self-contained -r win-x64 -o YourProject/publish",
      "group": "build"
    },
    {
      "label": "build: installer",
      "type": "shell",
      "command": "vpk pack --packId YourAppId --packVersion 1.0.0 --packDir YourProject/publish --mainExe YourApp.exe --yes --skip-updates",
      "group": "build",
      "dependsOn": ["build: publish"]
    }
  ]
}
```

## Step 7: Create and Push a Release

To trigger a build and release:

1. **Update version numbers** in your project files if needed
2. **Commit all changes**
3. **Create a version tag:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

The GitHub Action will automatically:
- Build your project
- Create a Velopack installer
- Create a GitHub release
- Upload the installer files

## Step 8: Verify the Release

After the workflow completes:
1. Go to your GitHub repository's **Releases** page
2. Verify the release was created with version tag (e.g., `v1.0.0`)
3. Check that the installer files are attached:
   - `Setup.exe` - Full installer
   - `RELEASES` - Update manifest
   - `.nupkg` files - Update packages

## How the Auto-Update Works

1. **On application startup:**
   - App checks GitHub releases for the latest version
   - Compares with currently installed version

2. **If update available:**
   - Downloads the update package in the background
   - Applies the update
   - Restarts the application automatically

3. **User experience:**
   - User sees a message about updating
   - Application downloads and applies updates automatically
   - Minimal user interaction required

## Troubleshooting

### Updates Not Working

- Verify `GithubOwner` and `GithubRepo` constants match your repository
- Check that releases are public (not private repository)
- Ensure `RELEASES` manifest file is uploaded to releases

### Build Failures

- Verify all paths in workflow match your project structure
- Ensure .NET version matches your project's target framework
- Check that `vpk` tool installs successfully

### Installer Issues

- Confirm `--mainExe` parameter points to correct executable
- Verify `--packDir` contains all necessary files
- Check that application is self-contained (includes runtime)

## Best Practices

1. **Version Management:**
   - Use semantic versioning (e.g., `v1.0.0`, `v1.1.0`)
   - Increment version for each release
   - Keep versions consistent across project files

2. **Testing:**
   - Test installer locally before pushing tags
   - Use `--skip-updates` flag during development
   - Verify update process with real releases

3. **Release Notes:**
   - Add release notes to GitHub releases
   - Document breaking changes
   - Include upgrade instructions if needed

4. **Error Handling:**
   - Wrap update code in try-catch
   - Log update failures for debugging
   - Don't block app startup on update failures

## Additional Resources

- [Velopack Documentation](https://docs.velopack.io/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Semantic Versioning](https://semver.org/)

## Summary

This implementation provides:
- ✅ Automated builds on version tags
- ✅ Windows installer generation
- ✅ Automatic update checking and installation
- ✅ GitHub Releases integration
- ✅ Minimal user interaction required

Users simply download and run the initial installer, then receive automatic updates forever!
