#!/usr/bin/env node
/**
 * scripts/bootstrap.mjs — iki bilgisayarda geliştirme ortamını hazırlar.
 *
 *   node scripts/bootstrap.mjs            # kontrol + proje bağımlılıklarını kur
 *   node scripts/bootstrap.mjs --check    # yalnızca eksikleri raporla (CI/ajan)
 *   node scripts/bootstrap.mjs --install  # eksik sistem araçlarını winget/brew ile kur
 *   node scripts/bootstrap.mjs --force    # node_modules'ü yeniden kur
 *
 * Not: WindowsDesktop/WPF yalnızca Windows'ta derlenir. macOS'ta API, web ve mobil
 * bağımlılıkları kurulur; masaüstü derlemesi Windows makinede yapılır.
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const isWindows = process.platform === 'win32';
const isMac = process.platform === 'darwin';
const args = new Set(process.argv.slice(2));
const checkOnly = args.has('--check');
const doInstall = args.has('--install');
const force = args.has('--force');

const missing = [];

function log(message) {
  console.log(message);
}

function capture(command, commandArgs) {
  const result = spawnSync(command, commandArgs, {
    cwd: root,
    encoding: 'utf8',
    shell: isWindows && /^(npm|npx)$/.test(command),
  });

  if (result.status !== 0) return null;

  return `${result.stdout ?? ''}\n${result.stderr ?? ''}`.trim().split(/\r?\n/)[0];
}

function run(command, commandArgs, options = {}) {
  log(`\n> ${command} ${commandArgs.join(' ')}${options.cwd ? `  (${options.cwd})` : ''}`);

  const result = spawnSync(command, commandArgs, {
    cwd: options.cwd ?? root,
    stdio: 'inherit',
    shell: options.shell ?? (isWindows && /^(npm|npx)$/.test(command)),
    env: { ...process.env, ...(options.env ?? {}) },
  });

  return result.status === 0;
}

function requireTool(name, versionText, hint, required = true) {
  if (versionText) {
    log(`✓ ${name}: ${versionText}`);
    return true;
  }

  log(`✗ ${name} bulunamadı${required ? '' : ' (opsiyonel)'} — ${hint}`);
  if (required) missing.push(name);
  return false;
}

function checkTools() {
  log('== Araç kontrolü ==');

  const git = capture('git', ['--version']);
  requireTool('git', git, 'https://git-scm.com/downloads');

  const nodeVersion = process.versions.node;
  const nodeMajor = Number.parseInt(nodeVersion.split('.')[0], 10);
  if (nodeMajor >= 20) {
    log(`✓ node: v${nodeVersion}${nodeMajor !== 22 ? ' (CI ve .nvmrc: 22; uyumlu)' : ''}`);
  } else {
    requireTool('node', null, 'Node.js 22 LTS kurun: https://nodejs.org');
  }

  const npm = capture('npm', ['--version']);
  requireTool('npm', npm, 'Node.js ile birlikte gelir');

  const dotnet = capture('dotnet', ['--version']);
  const dotnetMajor = dotnet ? Number.parseInt(dotnet.split('.')[0], 10) : 0;
  if (dotnet && dotnetMajor >= 8) {
    log(`✓ dotnet: ${dotnet}`);
    if (dotnetMajor !== 8) log('  ! global.json .NET 8 ister; 8.0 SDK kurulu olmalı.');
  } else {
    requireTool('dotnet', null, '.NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0');
  }

  const gh = capture('gh', ['--version']);
  requireTool('gh', gh, 'GitHub CLI (opsiyonel): https://cli.github.com', false);

  if (isWindows) {
    const iscc = [
      'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe',
      'C:\\Program Files\\Inno Setup 6\\ISCC.exe',
      path.join(process.env.LOCALAPPDATA ?? '', 'Programs', 'Inno Setup 6', 'ISCC.exe'),
    ].find((candidate) => fs.existsSync(candidate));

    log(iscc ? `✓ Inno Setup: ${iscc}` : '! Inno Setup 6 yok (yalnızca Setup EXE üretimi için gerekli).');
  }

  if (isMac) {
    const xcode = capture('xcodebuild', ['-version']);
    log(xcode ? `✓ Xcode: ${xcode}` : '! Xcode yok; iOS derlemesi için App Store’dan kurun.');
    const pods = capture('pod', ['--version']);
    log(pods ? `✓ CocoaPods: ${pods}` : '! CocoaPods yok (iOS native derleme için).');
    const watchman = capture('watchman', ['--version']);
    log(watchman ? `✓ watchman: ${watchman}` : '! watchman yok (Metro için önerilir).');
  }
}

function installSystemTools() {
  if (!doInstall) {
    if (missing.length > 0) {
      log('\nEksik araçlar var. Kurmak için:');
      if (isWindows) {
        log('  winget install Git.Git');
        log('  winget install Microsoft.DotNet.SDK.8');
        log('  winget install OpenJS.NodeJS.LTS');
        log('  winget install JRSoftware.InnoSetup');
      } else if (isMac) {
        log('  brew install node@22 && brew link --overwrite node@22');
        log('  brew install --cask dotnet-sdk');
        log('  brew install watchman cocoapods');
        log('  npm install -g eas-cli');
      }
      log('veya: node scripts/bootstrap.mjs --install');
    }
    return;
  }

  if (isWindows) {
    const winget = capture('winget', ['--version']);
    if (!winget) {
      log('winget bulunamadı; araçları elle kurun.');
      return;
    }

    const packages = [];
    if (missing.includes('git')) packages.push('Git.Git');
    if (missing.includes('dotnet')) packages.push('Microsoft.DotNet.SDK.8');
    if (missing.includes('node') || missing.includes('npm')) packages.push('OpenJS.NodeJS.LTS');

    for (const id of packages) {
      run('winget', ['install', '--id', id, '-e', '--accept-package-agreements', '--accept-source-agreements']);
    }
  } else if (isMac) {
    const brew = capture('brew', ['--version']);
    if (!brew) {
      log('Homebrew bulunamadı; https://brew.sh adresinden kurun.');
      return;
    }

    if (missing.includes('node') || missing.includes('npm')) {
      run('brew', ['install', 'node@22']);
      run('brew', ['link', '--overwrite', 'node@22']);
    }

    if (missing.includes('dotnet')) {
      run('brew', ['install', '--cask', 'dotnet-sdk']);
    }

    run('brew', ['install', 'watchman', 'cocoapods']);
    if (capture('npm', ['--version'])) {
      run('npm', ['install', '-g', 'eas-cli']);
    }
  }
}

function ensureEnvFile(relativePath, content) {
  const target = path.join(root, relativePath);

  if (fs.existsSync(target)) {
    log(`= ${relativePath} zaten var`);
    return;
  }

  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content, 'utf8');
  log(`+ ${relativePath} oluşturuldu (gitignore'da, commit edilmez)`);
}

function installProjectDependencies() {
  if (missing.includes('dotnet')) {
    log('\n.NET SDK yok; dotnet restore atlandı.');
    return false;
  }

  const targets = isWindows
    ? ['CgpRandevuTakip.sln']
    : [
        'src/CgpRandevuTakip.Api/CgpRandevuTakip.Api.csproj',
        'tests/CgpRandevuTakip.Api.Tests/CgpRandevuTakip.Api.Tests.csproj',
      ];

  for (const target of targets) {
    if (!run('dotnet', ['restore', target])) {
      log(`Hata: dotnet restore başarısız (${target}).`);
      return false;
    }
  }

  if (!missing.includes('npm')) {
    for (const project of ['web', 'mobile']) {
      const projectDir = path.join(root, project);
      const modules = path.join(projectDir, 'node_modules');

      if (fs.existsSync(modules) && !force) {
        log(`= ${project}/node_modules mevcut (--force ile yenilenir)`);
        continue;
      }

      if (!run('npm', ['ci'], { cwd: projectDir })) {
        log(`Hata: npm ci başarısız (${project}).`);
        return false;
      }
    }
  }

  ensureEnvFile('web/.env.local', 'NEXT_PUBLIC_API_URL=http://127.0.0.1:5199\n');
  ensureEnvFile(
    'mobile/.env',
    '# Fiziksel cihazda bilgisayarın LAN IP adresini yazın.\nEXPO_PUBLIC_API_URL=http://127.0.0.1:5199\n',
  );

  return true;
}

function summary() {
  log('\n== Hazır ==');
  log('API    : dotnet run --project src/CgpRandevuTakip.Api --urls http://0.0.0.0:5199');
  log('Web    : cd web && npm run dev');
  log('Mobil  : cd mobile && npx expo start');
  if (isWindows) {
    log('Test   : dotnet test CgpRandevuTakip.sln -c Release');
    log('Setup  : .\\Kurulum-Olustur.ps1');
  } else {
    log('Test   : dotnet test tests/CgpRandevuTakip.Api.Tests -c Release');
    log('Not    : Masaüstü (WPF) yalnızca Windows’ta derlenir.');
  }
  log('Ortam  : docs/35_Gelistirme_Ortami.md');
}

log(`CGP - Randevu Takip · ortam hazırlığı (${process.platform})`);
checkTools();
installSystemTools();

if (missing.length > 0) {
  log(`\nEksik zorunlu araçlar: ${missing.join(', ')}`);

  if (checkOnly) {
    process.exit(1);
  }

  if (doInstall) {
    log('Kurulum komutları çalıştırıldı. Terminali yenileyip scripti tekrar çalıştırın.');
    process.exit(0);
  }

  process.exit(1);
}

if (checkOnly) {
  log('\n--check tamamlandı.');
  process.exit(0);
}

const ok = installProjectDependencies();
summary();

process.exit(ok ? 0 : 1);
