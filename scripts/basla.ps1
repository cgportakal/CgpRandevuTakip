param(
    [switch]$Yes,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Info([string]$message) {
    Write-Host $message
}

function Fail([string]$message) {
    Write-Host ''
    Write-Host "HATA: $message" -ForegroundColor Red
    exit 1
}

Info 'CGP - Randevu Takip · başlangıç'
Info "Klasör: $root"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail 'Git kurulu değil: https://git-scm.com/downloads'
}

$dirty = git status --porcelain
if ($LASTEXITCODE -ne 0) {
    Fail 'git status başarısız.'
}

if ($dirty) {
    Info ''
    Info 'Çalışma ağacı temiz değil. Önce commit veya stash yapın:'
    $dirty | ForEach-Object { Info "  $_" }
    Fail 'Veri kaybını önlemek için işlem durduruldu.'
}

if (-not $Check) {
    Info ''
    Info '> git pull --ff-only'
    git pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        Fail 'Pull başarısız; çakışmaları çözüp tekrar deneyin.'
    }
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (-not $Yes) {
            $answer = Read-Host 'Node.js 22 kurulsun mu? (e/H)'
            if ($answer -notmatch '^[eEyY]') {
                Fail 'Node kurulmadan devam edilemez: https://nodejs.org'
            }
        }

        Info '> winget install OpenJS.NodeJS.LTS'
        winget install --id OpenJS.NodeJS.LTS -e --accept-package-agreements --accept-source-agreements
        Info 'Kurulumdan sonra terminali yenileyip tekrar çalıştırın.'
        exit 0
    }

    Fail 'Node.js yok. https://nodejs.org (22 LTS) kurun veya winget kullanın.'
}

Info ''
Info "Node: $(node --version)"

if ($Check) {
    node scripts/bootstrap.mjs --check
    if ($LASTEXITCODE -ne 0) {
        Fail 'Eksik araç var. Kurulum: node scripts/bootstrap.mjs --install'
    }
}
else {
    node scripts/bootstrap.mjs
    if ($LASTEXITCODE -ne 0) {
        Fail 'Bootstrap başarısız. Ayrıntı: docs/35_Gelistirme_Ortami.md'
    }
}

Info ''
Info '== Devir durumu (coordination/CURRENT.md) =='
if (Test-Path coordination/CURRENT.md) {
    Get-Content coordination/CURRENT.md -TotalCount 28 | ForEach-Object { Info $_ }
}

Info ''
Info '== Aktif kilit =='
node coordination/lock.mjs status

Info ''
Info 'Sıradaki adım:'
Info '  node coordination/lock.mjs acquire --machine <makine> --task "<iş>" --branch agent/<makine>/<konu>'
Info '  git switch -c agent/<makine>/<konu>'
Info ''
Info 'Kurallar: AGENTS.md · Protokol: coordination/PROTOCOL.md · Ortam: docs/35_Gelistirme_Ortami.md'
