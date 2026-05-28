#Requires -Version 5.1
param(
    [Parameter(Mandatory)]
    [string] $TargetPath,
    [string] $ThumbprintFile = ""
)

# ── Resolve thumbprint ────────────────────────────────────────────────────
if (-not $ThumbprintFile) {
    $dir = if ($PSScriptRoot) { $PSScriptRoot }
           elseif ($MyInvocation.MyCommand.Path) { Split-Path $MyInvocation.MyCommand.Path }
           else { Split-Path $MyInvocation.ScriptName }
    $ThumbprintFile = Join-Path $dir "cert-thumbprint.txt"
}

if (-not (Test-Path $ThumbprintFile)) {
    Write-Warning "Thumbprint file not found: $ThumbprintFile. Run tools\Setup-DevCert.ps1 first."
    exit 0
}
$thumbprint = (Get-Content $ThumbprintFile -Raw).Trim()
if (-not $thumbprint) {
    Write-Warning "Thumbprint file is empty. Run tools\Setup-DevCert.ps1 first."
    exit 0
}

# ── Guard: target must exist ──────────────────────────────────────────────
if (-not (Test-Path $TargetPath)) {
    Write-Warning "Sign-Output: file not found: $TargetPath"
    exit 0
}

# ── Locate signtool.exe (Windows SDK, ships with Visual Studio) ───────────
$signtool = $null

# Well-known Windows SDK locations
$kitRoot  = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$signtool = Get-ChildItem $kitRoot -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like "*x64*" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

# PATH fallback
if (-not $signtool) {
    $fromPath = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($fromPath) { $signtool = $fromPath.Source }
}

if (-not $signtool) {
    Write-Warning "signtool.exe not found. Install the Windows SDK (ships with Visual Studio)."
    exit 0
}

# ── Sign with signtool ────────────────────────────────────────────────────
$leaf = Split-Path $TargetPath -Leaf
Write-Host "  Signing: $leaf" -ForegroundColor Cyan

# Try with timestamp server first
& $signtool sign /sha1 $thumbprint /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 $TargetPath 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "  OK (timestamped)" -ForegroundColor Green
    exit 0
}

# Retry without timestamp (offline / firewall blocking timestamp server)
& $signtool sign /sha1 $thumbprint /fd SHA256 $TargetPath 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "  OK (no timestamp)" -ForegroundColor Yellow
} else {
    # Run again with output to capture the error message
    $output = & $signtool sign /sha1 $thumbprint /fd SHA256 $TargetPath 2>&1
    Write-Warning "  Signing failed: $output"
}
