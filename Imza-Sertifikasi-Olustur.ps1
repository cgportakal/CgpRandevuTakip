param(
    [string]$Subject = "CN=CGP - Randevu Takip (Gelistirme), O=CGP, C=TR",
    [int]$Years = 3
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $Subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddMonths(1)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $cert) {
    Write-Host "[CGP] Gelistirme kod imzalama sertifikasi olusturuluyor..."
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyUsage DigitalSignature `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears($Years)
} else {
    Write-Host "[CGP] Mevcut gelistirme sertifikasi kullaniliyor."
}

$cerPath = Join-Path $dist "CGP-RandevuTakip-Dev.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
Set-Content -Path (Join-Path $root "signing.thumbprint") -Value $cert.Thumbprint -Encoding ASCII

Write-Host ""
Write-Host "[CGP] Sertifika hazir (GELISTIRME / self-signed):"
Write-Host "  Subject   : $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Bitis     : $($cert.NotAfter.ToString('dd.MM.yyyy'))"
Write-Host "  Public CER: $cerPath"
Write-Host ""
Write-Host "NOT: Bu sertifika varsayilan olarak guvenilir degildir; Windows 'Bilinmeyen yayinci'"
Write-Host "gosterebilir. Uretim icin CA'dan alinmis bir kod imzalama sertifikasi (PFX) kullanin."
