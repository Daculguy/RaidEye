param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\RaidEye.Agent"
$dist = Join-Path $root "dist"
$out = Join-Path $dist "RaidEye.exe"

$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Could not find csc.exe. Install .NET Framework developer tools or the .NET SDK."
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$sources = Get-ChildItem -Path $src -Filter *.cs -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName }
if (-not $sources) {
    throw "No C# source files found under $src"
}

$defines = if ($Configuration -eq "Debug") { "DEBUG;TRACE" } else { "TRACE" }
$optimize = if ($Configuration -eq "Debug") { "/optimize-" } else { "/optimize+" }

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /define:$defines `
    $optimize `
    /out:$out `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $out"

