#Requires -Version 5.1
<#
.SYNOPSIS
    One-time setup: creates a self-signed Authenticode certificate for Glass.Message
    development builds, registers it in CurrentUser stores (no admin required),
    and saves the thumbprint for use by Sign-Output.ps1.

.USAGE
    Run once from PowerShell:
        .\tools\Setup-DevCert.ps1
#>

$subjectName   = "CN=Glass.Message Dev, O=Gehan Fernando"
$thumbprintFile = Join-Path $PSScriptRoot "cert-thumbprint.txt"

# ── Check if a valid cert already exists ─────────────────────────────────
$existing = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $subjectName -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($existing) {
    Write-Host "Certificate already exists." -ForegroundColor Green
    Write-Host "  Thumbprint : $($existing.Thumbprint)"
    Write-Host "  Expires    : $($existing.NotAfter.ToString('yyyy-MM-dd'))"
    $existing.Thumbprint | Set-Content $thumbprintFile -Encoding ASCII
} else {
    Write-Host "Creating self-signed Authenticode certificate..." -ForegroundColor Cyan

    $cert = New-SelfSignedCertificate `
        -Type              CodeSigning `
        -Subject           $subjectName `
        -KeyAlgorithm      RSA `
        -KeyLength         2048 `
        -HashAlgorithm     SHA256 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter          (Get-Date).AddYears(5) `
        -TextExtension     @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

    $existing = $cert
    Write-Host "  Thumbprint : $($cert.Thumbprint)"
    Write-Host "  Expires    : $($cert.NotAfter.ToString('yyyy-MM-dd'))"
    $cert.Thumbprint | Set-Content $thumbprintFile -Encoding ASCII
}

# ── Register in CurrentUser\TrustedPublisher (no admin, no prompt) ────────
# This allows Authenticode to report the signature as Valid (not UnknownError)
$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
    [System.Security.Cryptography.X509Certificates.StoreName]::TrustedPublisher,
    [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
try {
    $pubStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $alreadyThere = $pubStore.Certificates | Where-Object { $_.Thumbprint -eq $existing.Thumbprint }
    if (-not $alreadyThere) {
        $pubStore.Add($existing)
        Write-Host "  Added to CurrentUser\TrustedPublisher" -ForegroundColor Green
    } else {
        Write-Host "  Already in CurrentUser\TrustedPublisher" -ForegroundColor Green
    }
} finally { $pubStore.Close() }

Write-Host "`nThumbprint saved: $thumbprintFile" -ForegroundColor Cyan
Write-Host "Done. Every build will now auto-sign the output." -ForegroundColor Green
