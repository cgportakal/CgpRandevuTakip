#!/usr/bin/env node
/**
 * CGP - Randevu Takip — UAT doğrulama betiği
 *
 * 1.000+ kayıt üzerinde oda/ünvan/çoklu görüşmeci/çoklu misafir verisini,
 * çakışma kurallarını, rapor toplamlarını ve temel performansı doğrular.
 *
 * Kullanım:
 *   node scripts/verify-uat.mjs
 *   node scripts/verify-uat.mjs --base-url http://127.0.0.1:5199 --samples 25
 */

import { writeFile, mkdir } from 'node:fs/promises';
import path from 'node:path';

const args = process.argv.slice(2);
const arg = (name, fallback) => {
  const index = args.indexOf(`--${name}`);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};

const BASE_URL = arg('base-url', 'http://127.0.0.1:5199').replace(/\/+$/, '');
const ADMIN_USER = arg('admin-user', 'admin');
const ADMIN_PASS = arg('admin-pass', 'CgpDemo!2026');
const SAMPLES = Number(arg('samples', '25'));

const pad = (n, w = 2) => String(n).padStart(w, '0');
const localDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const localStamp = (d) =>
  `${localDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

let accessToken = '';
const failures = [];
const notes = [];

function check(condition, message) {
  if (condition) {
    notes.push(`OK  ${message}`);
    return true;
  }

  failures.push(message);
  notes.push(`FAIL ${message}`);
  return false;
}

async function api(pathname, { method = 'GET', body, allow = [] } = {}) {
  const response = await fetch(`${BASE_URL}${pathname}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const text = await response.text();
  let data = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = text;
    }
  }

  if (!response.ok && !allow.includes(response.status)) {
    throw new Error(`HTTP ${response.status} ${pathname}: ${JSON.stringify(data)}`);
  }

  return { status: response.status, data };
}

function percentile(values, ratio) {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.min(sorted.length - 1, Math.floor(sorted.length * ratio));
  return sorted[index];
}

async function login() {
  const health = await api('/v1/health');
  check(health.status === 200, 'API sağlık kontrolü');

  const login = await api('/v1/auth/login', {
    method: 'POST',
    body: { username: ADMIN_USER, password: ADMIN_PASS, deviceName: 'verify-uat' },
  });

  accessToken = login.data.accessToken;
}

async function main() {
  await login();

  const titles = (await api('/v1/titles?activeOnly=false')).data ?? [];
  const rooms = (await api('/v1/rooms?activeOnly=false')).data ?? [];
  const allHosts = (await api('/v1/hosts')).data ?? [];
  const hosts = allHosts.filter((host) => host.role === 'Host');

  check(titles.length >= 4, `Yönetilen ünvan sayısı >= 4 (${titles.length})`);
  check(
    rooms.filter((room) => room.kind === 'Shared' && room.isActive).length >= 3,
    'En az 3 aktif ortak oda',
  );
  check(
    rooms.filter((room) => room.kind === 'HostRoom' && room.isActive).length >= hosts.length,
    'Her görüşmeci için en az bir oda',
  );
  check(
    hosts.some((host) => Boolean(host.titleId)),
    'En az bir görüşmeciye ünvan atanmış',
  );

  const from = new Date();
  from.setDate(from.getDate() - 95);
  const to = new Date();
  to.setDate(to.getDate() + 95);

  const dayStart = performance.now();
  const all = (await api(
    `/v1/appointments/search?from=${localDate(from)}&to=${localDate(to)}`,
  )).data ?? [];
  const searchMs = performance.now() - dayStart;

  check(all.length >= 1000, `Görünür randevu sayısı >= 1000 (${all.length})`);
  check(
    all.some((item) => item.guestCount > 1),
    'Çoklu misafirli randevu var',
  );
  check(
    all.some((item) => item.participantCount > 1),
    'Çoklu görüşmecili randevu var',
  );
  check(
    all.some((item) => Boolean(item.roomId)),
    'Odası atanmış randevu var',
  );

  const samples = [];
  const step = Math.max(1, Math.floor(all.length / SAMPLES));
  for (let i = 0; i < all.length && samples.length < SAMPLES; i += step) {
    samples.push(all[i]);
  }

  let detailFailures = 0;
  for (const sample of samples) {
    const detail = (await api(`/v1/appointments/${sample.id}`)).data;
    if (!detail) {
      detailFailures++;
      continue;
    }

    const primaryGuests = (detail.guests ?? []).filter((guest) => guest.isPrimary).length;
    const primaryHosts = (detail.participants ?? []).filter((host) => host.isPrimary).length;

    if (primaryGuests !== 1 || primaryHosts !== 1) detailFailures++;
    if (detail.method === 'FaceToFace' && !detail.roomId) detailFailures++;
  }

  check(detailFailures === 0, `Örnek detaylarda birincil misafir/görüşmeci tutarlı (${samples.length} örnek)`);

  const dayKey = localDate(new Date());
  const dayStartMs = performance.now();
  const todayRows = (await api(`/v1/appointments/day?date=${dayKey}`)).data ?? [];
  const dayMs = performance.now() - dayStartMs;

  const reportStart = performance.now();
  const summary = (await api(
    `/v1/reports/summary?from=${localDate(from)}&to=${localDate(to)}`,
  )).data;
  const reportMs = performance.now() - reportStart;

  check(Boolean(summary), 'Rapor özeti alındı');
  check(summary?.total === all.length, `Rapor toplamı arama toplamına eşit (${summary?.total}/${all.length})`);
  check(Array.isArray(summary?.rooms) && summary.rooms.length > 0, 'Rapor oda dağılımı içeriyor');
  check((summary?.guestTotal ?? 0) >= summary?.total, 'Rapor misafir toplamı randevu toplamından büyük/eşit');

  // Aynı oda + saat: çakışma dönmeli.
  const sharedRoom = rooms.find((room) => room.kind === 'Shared' && room.isActive);
  const categoryId = all[0]?.categoryId;

  if (sharedRoom && categoryId) {
    const conflictDate = new Date();
    conflictDate.setDate(conflictDate.getDate() + 20);
    conflictDate.setHours(11, 0, 0, 0);

    const first = await api('/v1/appointments', {
      method: 'POST',
      body: {
        firstName: 'UAT',
        lastName: 'Oda Bir',
        phone: '0555 900 00 01',
        startsAtLocal: localStamp(conflictDate),
        durationMinutes: 30,
        hostUserId: hosts[0].id,
        categoryId,
        method: 'FaceToFace',
        description: 'UAT oda çakışma kontrolü',
        studentFirstName: null,
        studentLastName: null,
        reminderOffsetsMinutes: [30],
        ignoreConflicts: false,
        clientRequestId: `verify-room-${Date.now()}-1`,
        roomId: sharedRoom.id,
        participantHostUserIds: [],
        guests: [
          {
            firstName: 'UAT',
            lastName: 'Oda Bir',
            phone: '0555 900 00 01',
            isPrimary: true,
          },
        ],
      },
      allow: [400, 409],
    });

    const second = await api('/v1/appointments', {
      method: 'POST',
      body: {
        firstName: 'UAT',
        lastName: 'Oda İki',
        phone: '0555 900 00 02',
        startsAtLocal: localStamp(new Date(conflictDate.getTime() + 10 * 60000)),
        durationMinutes: 30,
        hostUserId: hosts[1 % hosts.length].id,
        categoryId,
        method: 'FaceToFace',
        description: 'UAT oda çakışma kontrolü',
        studentFirstName: null,
        studentLastName: null,
        reminderOffsetsMinutes: [30],
        ignoreConflicts: false,
        clientRequestId: `verify-room-${Date.now()}-2`,
        roomId: sharedRoom.id,
        participantHostUserIds: [],
        guests: [
          {
            firstName: 'UAT',
            lastName: 'Oda İki',
            phone: '0555 900 00 02',
            isPrimary: true,
          },
        ],
      },
      allow: [400, 409],
    });

    check(first.status === 200, 'Oda çakışma testi: ilk randevu oluştu');
    check(
      second.status === 400 || second.status === 409,
      'Oda çakışma testi: ikinci randevu çakışma döndürdü',
    );
    check(
      Array.isArray(second.data?.conflicts) && second.data.conflicts.some((item) => item.kind === 'Room'),
      'Çakışma yanıtı Room türü içeriyor',
    );

    if (first.status === 200 && first.data?.appointmentId) {
      await api(`/v1/appointments/${first.data.appointmentId}`, {
        method: 'DELETE',
        allow: [400, 404, 409],
      });
    }
  } else {
    check(false, 'Oda çakışma testi için ortak oda/kategori bulunamadı');
  }

  const timings = {
    searchMs: Math.round(searchMs),
    dayMs: Math.round(dayMs),
    reportMs: Math.round(reportMs),
  };

  check(timings.dayMs < 1500, `Günlük liste süresi < 1500 ms (${timings.dayMs} ms)`);
  check(timings.searchMs < 3000, `Arama süresi < 3000 ms (${timings.searchMs} ms)`);
  check(timings.reportMs < 5000, `Rapor süresi < 5000 ms (${timings.reportMs} ms)`);

  const result = {
    generatedAt: new Date().toISOString(),
    baseUrl: BASE_URL,
    counts: {
      titles: titles.length,
      hosts: hosts.length,
      rooms: rooms.length,
      sharedRooms: rooms.filter((room) => room.kind === 'Shared').length,
      appointments: all.length,
      today: todayRows.length,
      samples: samples.length,
    },
    timings,
    failures,
    notes,
  };

  const outDir = path.join(process.env.TEMP ?? '.', 'opencode');
  await mkdir(outDir, { recursive: true });
  const outPath = path.join(outDir, 'cgp-uat-report.json');
  await writeFile(outPath, JSON.stringify(result, null, 2));

  console.log(`\nUAT doğrulaması: ${failures.length === 0 ? 'BAŞARILI' : 'BAŞARISIZ'}`);
  for (const note of notes) console.log(`  ${note}`);
  console.log(`\nRapor: ${outPath}`);

  if (failures.length > 0) process.exit(1);
}

main().catch((error) => {
  console.error('\nHata:', error.message);
  process.exit(1);
});
