#!/usr/bin/env bash
# CGP - Randevu Takip · macOS / Linux başlangıç betiği
# Kullanım: bash basla.command [--yes] [--check]
set -euo pipefail

cd "$(dirname "$0")"

YES=0
CHECK=0

for arg in "$@"; do
  case "$arg" in
    --yes|-y) YES=1 ;;
    --check) CHECK=1 ;;
    --help|-h)
      echo "Kullanım: bash basla.command [--yes] [--check]"
      exit 0
      ;;
  esac
done

info() { printf '%s\n' "$*"; }
fail() { printf '\nHATA: %s\n' "$*" >&2; exit 1; }

info "CGP - Randevu Takip · başlangıç"
info "Klasör: $(pwd)"

command -v git >/dev/null 2>&1 || fail "Git kurulu değil: https://git-scm.com/downloads"

if [ -n "$(git status --porcelain)" ]; then
  info ""
  info "Çalışma ağacı temiz değil. Önce commit veya stash yapın:"
  git status --short
  fail "Veri kaybını önlemek için işlem durduruldu."
fi

if [ "$CHECK" -eq 0 ]; then
  info ""
  info "> git pull --ff-only"
  git pull --ff-only || fail "Pull başarısız; çakışmaları çözüp tekrar deneyin."
fi

if ! command -v node >/dev/null 2>&1; then
  if command -v brew >/dev/null 2>&1; then
    if [ "$YES" -eq 0 ]; then
      printf 'Node.js 22 kurulsun mu? [e/H] '
      read -r answer
      case "$answer" in
        e|E|y|Y) ;;
        *) fail "Node kurulmadan devam edilemez: https://nodejs.org" ;;
      esac
    fi

    info "> brew install node@22"
    brew install node@22
    brew link --overwrite node@22
  else
    fail "Node.js yok. https://nodejs.org (22 LTS) kurun veya Homebrew kullanın."
  fi
fi

info ""
info "Node: $(node --version)"

if [ "$CHECK" -eq 1 ]; then
  node scripts/bootstrap.mjs --check || fail "Eksik araç var. Kurulum: node scripts/bootstrap.mjs --install"
else
  node scripts/bootstrap.mjs || fail "Bootstrap başarısız. Ayrıntı: docs/35_Gelistirme_Ortami.md"
fi

info ""
info "== Devir durumu (coordination/CURRENT.md) =="
if [ -f coordination/CURRENT.md ]; then
  sed -n '1,28p' coordination/CURRENT.md
fi

info ""
info "== Aktif kilit =="
node coordination/lock.mjs status

info ""
info "Sıradaki adım:"
info "  node coordination/lock.mjs acquire --machine <makine> --task \"<iş>\" --branch agent/<makine>/<konu>"
info "  git switch -c agent/<makine>/<konu>"
info ""
info "Kurallar: AGENTS.md · Protokol: coordination/PROTOCOL.md · Ortam: docs/35_Gelistirme_Ortami.md"

if [ "$YES" -eq 0 ] && [ -t 0 ]; then
  printf '\nKapatmak için Enter...'
  read -r _
fi
