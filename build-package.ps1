$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
$toolboxPath = "C:\Users\Kyle\AppData\Local\Playnite\Toolbox.exe"
$stageRoot = Join-Path $projectRoot "dist\stage"
$stageDir = Join-Path $stageRoot "LocalGameDownloader"
$packagesDir = Join-Path $projectRoot "dist\packages"
$releaseDir = Join-Path $projectRoot "bin\Release"

if (-not (Test-Path $msbuildPath)) {
    throw "MSBuild was not found at '$msbuildPath'."
}

if (-not (Test-Path $toolboxPath)) {
    throw "Playnite Toolbox was not found at '$toolboxPath'."
}

& $msbuildPath (Join-Path $projectRoot "LocalGameDownloader.sln") /p:Configuration=Release
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

if (Test-Path $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $releaseDir "LocalGameDownloader.dll") -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot "extension.yaml") -Destination $stageDir
Copy-Item -LiteralPath (Join-Path $projectRoot "icon.png") -Destination $stageDir

$localizationPath = Join-Path $projectRoot "Localization"
if (Test-Path $localizationPath) {
    Copy-Item -LiteralPath $localizationPath -Destination $stageDir -Recurse
}

& $toolboxPath pack $stageDir $packagesDir
if ($LASTEXITCODE -ne 0) {
    throw "Packaging with Playnite Toolbox failed."
}

$latestGeneratedPackage = Get-ChildItem -Path $packagesDir -Filter "LocalGameDownloader_*.pext" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latestGeneratedPackage) {
    throw "No generated package was found in '$packagesDir'."
}

$friendlyPackagePath = Join-Path $packagesDir "LocalGameDownloader_v1.0.0.pext"
Copy-Item -LiteralPath $latestGeneratedPackage.FullName -Destination $friendlyPackagePath -Force

Write-Host "Created package: $friendlyPackagePath"
