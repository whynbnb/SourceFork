param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("framework", "aot", "aot-static")]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $root "src/SourceFork.csproj"

if (Test-Path -LiteralPath $Output) {
    Get-ChildItem -LiteralPath $Output -Force | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

$arguments = @(
    "publish", $project,
    "-c", "Release",
    "-r", $Rid,
    "-o", $Output,
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

switch ($Mode) {
    "framework" {
        $arguments += "--self-contained", "false"
        $arguments += "-p:DisableAOT=true", "-p:PublishAot=false", "-p:PublishTrimmed=false", "-p:PublishSingleFile=false"
    }
    "aot" {
        $arguments += "--self-contained", "true"
        $arguments += "-p:DisableAOT=false", "-p:PublishAot=true", "-p:PublishTrimmed=true", "-p:TrimMode=link", "-p:StripSymbols=true", "-p:PublishSingleFile=false"
    }
    "aot-static" {
        $arguments += "--self-contained", "true"
        $arguments += "-p:DisableAOT=false", "-p:PublishAot=true", "-p:PublishTrimmed=true", "-p:TrimMode=link", "-p:StripSymbols=true", "-p:PublishSingleFile=false", "-p:UseStaticLinkAvalonia=true"
    }
}

Write-Host "==> SourceFork publish"
Write-Host "    mode:   $Mode"
Write-Host "    rid:    $Rid"
Write-Host "    output: $Output"

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem -LiteralPath $Output -File -Filter "*.pdb" | Remove-Item -Force

$exe = Join-Path $Output "SourceFork.exe"
if ($Mode -ne "framework" -and -not (Test-Path -LiteralPath $exe)) {
    throw "AOT publish did not produce $exe"
}
if ($Mode -ne "framework" -and (Test-Path -LiteralPath (Join-Path $Output "SourceFork.dll"))) {
    throw "AOT output still contains managed assemblies."
}

if ($Mode -eq "aot-static") {
    $files = @(Get-ChildItem -LiteralPath $Output -File)
    if ($files.Count -ne 1) {
        $files | ForEach-Object { Write-Host $_.Name }
        throw "aot-static output must contain a single executable."
    }
}

Write-Host "==> done"
