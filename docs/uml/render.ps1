$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$jarPath = Join-Path $env:USERPROFILE "tools\plantuml\plantuml.jar"

if (-not (Test-Path $jarPath)) {
    Write-Host "plantuml.jar not found: $jarPath" -ForegroundColor Red
    Write-Host "Download it and place here, or edit render.ps1 with your path."
    exit 1
}

$pumlFiles = Get-ChildItem -Path $scriptDir -Filter "*.puml" -File
if ($pumlFiles.Count -eq 0) {
    Write-Host "No .puml files found in $scriptDir" -ForegroundColor Yellow
    exit 0
}

Push-Location $scriptDir
try {
    Write-Host "Generating PNG..."
    & java -jar $jarPath *.puml

    Write-Host "Generating SVG..."
    & java -jar $jarPath -tsvg *.puml

    Write-Host "Done. PNG and SVG files are generated in $scriptDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
