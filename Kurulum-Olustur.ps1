param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\CgpRandevuTakip.Desktop\CgpRandevuTakip.Desktop.csproj"
$dist = Join-Path $root "dist"
$publish = Join-Path $dist "CGP-RandevuTakip-$Version-win-x64"
$zip = Join-Path $dist "CGP-RandevuTakip-$Version-portable.zip"
$iss = Join-Path $PSScriptRoot "CGP-RandevuTakip.iss"

Write-Host "[CGP] Temizleniyor: $publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "[CGP] Release publish (win-x64, self-contained)..."
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız." }

Write-Host "[CGP] Taşınabilir ZIP oluşturuluyor..."
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip -CompressionLevel Optimal

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    Write-Host "[CGP] Kurulum EXE'si oluşturuluyor: $iscc"
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup derlemesi başarısız." }
} else {
    Write-Warning "Inno Setup 6 bulunamadı. Kurulum EXE'si oluşturulmadı; taşınabilir ZIP kullanılabilir."
    Write-Host "Kurulum için: https://jrsoftware.org/isdl.php adresinden Inno Setup 6 kurun ve bu betiği yeniden çalıştırın."
}

Write-Host ""
Write-Host "[CGP] Çıktılar:"
Write-Host "  Klasör : $publish"
Write-Host "  ZIP    : $zip"
if ($iscc) {
    Get-ChildItem $dist -Filter "CGP-RandevuTakip-$Version-Setup.exe" |
        ForEach-Object { Write-Host "  Setup  : $($_.FullName)" }
}
