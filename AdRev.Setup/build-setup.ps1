$ErrorActionPreference = "Stop"

Write-Host "Starting AdRev Installer Build..." -ForegroundColor Cyan

# 1. Check WiX
if (-not (Get-Command "wix" -ErrorAction SilentlyContinue)) {
    Write-Host "WiX Toolset not found." -ForegroundColor Yellow
    exit 1
}

# 1.1 Read Version from Project
$projectFile = "..\AdRev.Desktop\AdRev.Desktop.csproj"
if (Test-Path $projectFile) {
    [xml]$xml = Get-Content $projectFile
    $version = $xml.Project.PropertyGroup.Version
    if (-not $version) { $version = "1.0.0" }
}
else {
    $version = "1.0.0"
}
Write-Host "Target Version: $version" -ForegroundColor Magenta


# 2. Obfuscation setup (Security)
if (-not (Get-Command "obfuscar.console" -ErrorAction SilentlyContinue)) {
    Write-Host "Installing Obfuscar..." -ForegroundColor Yellow
    dotnet tool install --global Obfuscar.GlobalTool
}

# 3. Clean and Build Release
Write-Host "Building AdRev Projects (Release)..." -ForegroundColor Cyan
dotnet build "..\AdRev.Desktop\AdRev.Desktop.csproj" -c Release -r win-x64
dotnet build "..\AdRev.CLI\AdRev.CLI.csproj" -c Release -r win-x64
dotnet build "..\AdRev.LicenseGenerator\AdRev.LicenseGenerator.csproj" -c Release -r win-x64

# 4. Obfuscate
Write-Host "Waiting for file handles to release..." -ForegroundColor Gray
Start-Sleep -Seconds 2

Write-Host "Obfuscating Assemblies..." -ForegroundColor Cyan
Push-Location "..\AdRev.Desktop"
obfuscar.console obfuscar.xml
if ($LASTEXITCODE -ne 0) { Write-Host "Obfuscation failed" -ForegroundColor Red; exit 1 }
Pop-Location

# 5. Publish
Write-Host "Patching Release build with Obfuscated assemblies..." -ForegroundColor Cyan
$targetBin = "..\AdRev.Desktop\bin\Release\net9.0-windows\win-x64"
Copy-Item "..\AdRev.Desktop\Obfuscated\*.dll" $targetBin -Force

Write-Host "Publishing AdRev.Desktop..." -ForegroundColor Cyan
$projectPath = "..\AdRev.Desktop\AdRev.Desktop.csproj"
$publishDir = "..\AdRev.Desktop\bin\Release\net9.0-windows\win-x64\publish"

dotnet publish $projectPath -c Release -r win-x64 --no-build --self-contained true -o $publishDir

Write-Host "Publishing AdRev.CLI..." -ForegroundColor Cyan
dotnet publish "..\AdRev.CLI\AdRev.CLI.csproj" -c Release -r win-x64 --self-contained true -o "..\AdRev.CLI\bin\Release\net9.0-windows\publish"

Write-Host "Publishing AdRev.LicenseGenerator..." -ForegroundColor Cyan
dotnet publish "..\AdRev.LicenseGenerator\AdRev.LicenseGenerator.csproj" -c Release -r win-x64 --self-contained true -o "..\AdRev.LicenseGenerator\bin\Release\net9.0-windows\publish"

# 5.1 Harvest Files
Write-Host "Harvesting Files..." -ForegroundColor Cyan
.\harvest.ps1 -SourceDir $publishDir -OutputFile "GeneratedFiles.wxs" -ComponentGroupId "PublishedComponents" -DirectoryId "INSTALLFOLDER"

# 6. Build MSI
Write-Host "Building MSI (Version $version)..." -ForegroundColor Cyan
& wix build Product.wxs GeneratedFiles.wxs -d Version=$version -arch x64 `
    "Localization\fr-FR.wxl" `
    -o "AdRev$version.msi"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Success: AdRev$version.msi created." -ForegroundColor Green
    
    # 7. Organize Outputs
    Write-Host "Organizing releases..." -ForegroundColor Cyan
    $releaseDir = "..\Releases"
    if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }
    
    Copy-Item "AdRev$version.msi" -Destination $releaseDir -Force
    # Copy published exes
    Copy-Item "$publishDir\AdRev.Desktop.exe" -Destination $releaseDir -Force
    Copy-Item "..\AdRev.CLI\bin\Release\net9.0-windows\publish\AdRev.CLI.exe" -Destination $releaseDir -Force

    # Build Bundle
    Write-Host "Building Bundle..." -ForegroundColor Cyan
    
    $balExt = "C:\Users\HP\.wix\extensions\WixToolset.Bal.wixext\6.0.2\wixext6\WixToolset.BootstrapperApplications.wixext.dll"
    if (-not (Test-Path $balExt)) {
        # Fallback to name if explicit path fails
        $balExt = "WixToolset.Bal.wixext"
    }

    & wix build Bundle.wxs -d Version=$version -d MsiSource="AdRev$version.msi" -ext $balExt `
        "Localization\fr-FR.wxl" `
        -o "AdRevSetup.exe"
    
    if ($LASTEXITCODE -eq 0) {
        Copy-Item "AdRevSetup.exe" -Destination $releaseDir -Force
        Write-Host "Success: AdRevSetup.exe created." -ForegroundColor Green
    }
    else {
        Write-Host "Bundle Build failed." -ForegroundColor Red
    }
    
    # Copy CLI Tools
    $toolsDir = "$releaseDir\Tools"
    if (-not (Test-Path $toolsDir)) { New-Item -ItemType Directory -Path $toolsDir | Out-Null }
    $cliPublishDir = "..\AdRev.CLI\bin\Release\net9.0-windows\publish"
    
    if (Test-Path "$cliPublishDir\AdRev.CLI.exe") {
        Copy-Item "$cliPublishDir\AdRev.CLI.exe" -Destination $toolsDir -Force
    }
    if (Test-Path "$cliPublishDir\AdRev.CLI.runtimeconfig.json") {
        Copy-Item "$cliPublishDir\AdRev.CLI.runtimeconfig.json" -Destination $toolsDir -Force
    }
    Copy-Item "$cliPublishDir\*.dll" -Destination $toolsDir -Force -ErrorAction SilentlyContinue

    # Copy License Generator
    $genDir = "$releaseDir\LicenseGenerator"
    if (-not (Test-Path $genDir)) { New-Item -ItemType Directory -Path $genDir | Out-Null }
    $genPublishDir = "..\AdRev.LicenseGenerator\bin\Release\net9.0-windows\publish"
    if (Test-Path $genPublishDir) {
        Copy-Item "$genPublishDir\*" -Destination $genDir -Recurse -Force
    }

    Write-Host "🎉 Build Complete!" -ForegroundColor Green
    Write-Host "Files available in: $(Resolve-Path $releaseDir)" -ForegroundColor White
    Write-Host "  - Installer: AdRevSetup.exe (Bundle)" -ForegroundColor Yellow
    Write-Host "  - Setup: AdRev$version.msi" -ForegroundColor Gray
    Write-Host "  - Portable: AdRev.Desktop.exe" -ForegroundColor Gray
    Write-Host "  - Tools: Tools\AdRev.CLI.exe" -ForegroundColor Gray
    Write-Host "  - Generator: LicenseGenerator\AdRev.LicenseGenerator.exe" -ForegroundColor Gray
}
else {
    Write-Host "MSI Build failed." -ForegroundColor Red
}
