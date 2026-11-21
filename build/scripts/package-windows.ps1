param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("framework", "aot", "aot-static")]
    [string]$Mode,

    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if (-not (Test-Path -LiteralPath $Source)) {
    throw "publish directory does not exist: $Source"
}

if ($Mode -eq "aot-static") {
    $exe = Join-Path $Source "SourceFork.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "missing SourceFork.exe in $Source"
    }
    if (Test-Path -LiteralPath (Join-Path $Source "SourceFork.dll")) {
        throw "aot-static output still contains managed assemblies."
    }

    $files = @(Get-ChildItem -LiteralPath $Source -Recurse -File)
    if ($files.Count -ne 1) {
        $files | ForEach-Object { Write-Host $_.Name }
        throw "aot-static output must contain a single executable."
    }
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("sourcefork-package-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

try {
    Copy-Item -Path (Join-Path $Source "*") -Destination $tmp -Recurse -Force
    $license = Join-Path $root "LICENSE"
    if (Test-Path -LiteralPath $license) {
        Copy-Item -LiteralPath $license -Destination (Join-Path $tmp "LICENSE") -Force
    }

    Get-ChildItem -LiteralPath $tmp -File -Filter "*.pdb" | Remove-Item -Force
    $parent = Split-Path -Parent $Output
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Compress-Archive -Path (Join-Path $tmp "*") -DestinationPath $Output -CompressionLevel Optimal -Force
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
