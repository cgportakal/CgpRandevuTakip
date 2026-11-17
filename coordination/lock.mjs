#!/usr/bin/env node
/**
 * coordination/lock.mjs — iki bilgisayar arasında yumuşak çalışma kilidi.
 *
 * Kullanım:
 *   node coordination/lock.mjs status
 *   node coordination/lock.mjs acquire --machine macbook --task "Icerik konsolu" --branch agent/macbook/content
 *   node coordination/lock.mjs release --machine macbook
 *
 * Kilit dosyaları coordination/locks/<makine>.json olarak commit edilir.
 * 8 saatten eski kilitler bayat sayılır; --force ile devralınabilir.
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const baseDir = path.dirname(fileURLToPath(import.meta.url));
const locksDir = path.join(baseDir, 'locks');
const STALE_MS = 8 * 60 * 60 * 1000;

function parse(argv) {
  const result = { _: [] };

  for (let i = 0; i < argv.length; i += 1) {
    const value = argv[i];

    if (!value.startsWith('--')) {
      result._.push(value);
      continue;
    }

    const key = value.slice(2);
    const next = argv[i + 1];

    if (!next || next.startsWith('--')) {
      result[key] = true;
    } else {
      result[key] = next;
      i += 1;
    }
  }

  return result;
}

function safeName(value) {
  return String(value)
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'machine';
}

function readLocks() {
  fs.mkdirSync(locksDir, { recursive: true });

  return fs
    .readdirSync(locksDir)
    .filter((file) => file.endsWith('.json'))
    .map((file) => {
      const fullPath = path.join(locksDir, file);

      try {
        const data = JSON.parse(fs.readFileSync(fullPath, 'utf8'));
        const started = Date.parse(data.startedAt ?? '');
        const ageMs = Number.isNaN(started) ? Number.POSITIVE_INFINITY : Date.now() - started;
        return { file, fullPath, data, ageMs, stale: ageMs > STALE_MS };
      } catch {
        return { file, fullPath, data: {}, ageMs: Number.POSITIVE_INFINITY, stale: true };
      }
    });
}

function formatAge(ageMs) {
  if (!Number.isFinite(ageMs)) return 'bilinmiyor';
  const minutes = Math.max(0, Math.round(ageMs / 60000));
  if (minutes < 60) return `${minutes} dk`;
  return `${(minutes / 60).toFixed(1)} sa`;
}

function status() {
  const locks = readLocks();

  if (locks.length === 0) {
    console.log('Aktif kilit yok.');
    return;
  }

  for (const lock of locks) {
    const { data } = lock;
    console.log(
      `${lock.stale ? 'BAYAT' : 'AKTIF'} | ${data.machine ?? lock.file} | ${formatAge(lock.ageMs)} | ` +
        `${data.task ?? '-'} | dal=${data.branch ?? '-'} | baslangic=${data.startedAt ?? '-'}`,
    );
  }
}

function acquire(args) {
  const machine = args.machine;

  if (!machine || machine === true) {
    console.error('Hata: --machine zorunlu.');
    process.exit(2);
  }

  const name = safeName(machine);
  const otherActive = readLocks().filter(
    (lock) => lock.data.machine !== machine && !lock.stale,
  );

  if (otherActive.length > 0 && args.force !== true) {
    console.error('Başka bir makinenin aktif kilidi var:');
    for (const lock of otherActive) {
      console.error(
        `  ${lock.data.machine} | ${formatAge(lock.ageMs)} | ${lock.data.task ?? '-'} | dal=${lock.data.branch ?? '-'}`,
      );
    }
    console.error('Devam etmek için önce `git pull` yapın veya --force kullanın (nedenini CURRENT.md\'ye yazın).');
    process.exit(1);
  }

  const payload = {
    machine: String(machine),
    agent: process.env.AGENT_NAME ?? 'opencode',
    task: typeof args.task === 'string' ? args.task : 'belirtilmedi',
    branch: typeof args.branch === 'string' ? args.branch : 'master',
    files: typeof args.files === 'string' ? args.files.split(',').map((x) => x.trim()).filter(Boolean) : [],
    startedAt: new Date().toISOString(),
    host: os.hostname(),
  };

  const target = path.join(locksDir, `${name}.json`);
  fs.writeFileSync(target, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');

  console.log(`Kilit alındı: ${target}`);
  console.log(`Görev: ${payload.task} | Dal: ${payload.branch}`);
}

function release(args) {
  const machine = args.machine;

  if (!machine || machine === true) {
    console.error('Hata: --machine zorunlu.');
    process.exit(2);
  }

  const target = path.join(locksDir, `${safeName(machine)}.json`);

  if (!fs.existsSync(target)) {
    console.log('Kilit bulunamadı (zaten bırakılmış olabilir).');
    return;
  }

  fs.rmSync(target);
  console.log(`Kilit bırakıldı: ${target}`);
}

const args = parse(process.argv.slice(2));
const command = args._[0] ?? 'status';

switch (command) {
  case 'status':
    status();
    break;
  case 'acquire':
    acquire(args);
    break;
  case 'release':
    release(args);
    break;
  default:
    console.error(`Bilinmeyen komut: ${command}`);
    console.error('Kullanım: status | acquire --machine <ad> [--task ...] [--branch ...] [--force] | release --machine <ad>');
    process.exit(2);
}
