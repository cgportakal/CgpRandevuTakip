#!/usr/bin/env node
/**
 * CGP - Randevu Takip — Görüşme odası / ünvan / çoklu misafir UAT zenginleştirme
 *
 * Mevcut seed kayıtlarını (scripts/seed-test-data.mjs) yeni modele taşır:
 * - Ünvanları oluşturur ve görüşmecilere atar.
 * - Her görüşmeye oda, katılımcı görüşmeci ve ek misafir ekler.
 * - Geçmiş kayıtlarda misafir başına sonuç üretir.
 * - Bilinçli oda çakışmaları oluşturur.
 *
 * Kullanım:
 *   node scripts/seed-meeting-rooms.mjs
 *   node scripts/seed-meeting-rooms.mjs --base-url http://127.0.0.1:5199
 *   node scripts/seed-meeting-rooms.mjs --force
 */

import { writeFile, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);
const arg = (name, fallback) => {
  const index = args.indexOf(`--${name}`);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};
const flag = (name) => args.includes(`--${name}`);

const BASE_URL = arg('base-url', 'http://127.0.0.1:5199').replace(/\/+$/, '');
const ADMIN_USER = arg('admin-user', 'admin');
const ADMIN_PASS = arg('admin-pass', 'CgpDemo!2026');
const ADMIN_NAME = arg('admin-name', 'Demo Yönetici');
const ADMIN_EMAIL = arg('admin-email', 'demo@cgp.local');
const FORCE = flag('force');
const EXTRA_CONFLICTS = Number(arg('extra-conflicts', '12'));

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const STATE_PATH = path.join(SCRIPT_DIR, '.seed-rooms-state.json');
const SEED_VERSION = 'rooms-2026-11-22';

const pad = (n, w = 2) => String(n).padStart(w, '0');
const localDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const localStamp = (d) =>
  `${localDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

let accessToken = '';

class HttpError extends Error {
  constructor(status, body) {
    super(`HTTP ${status}`);
    this.status = status;
    this.body = body;
  }
}

async function api(pathname, { method = 'GET', body, auth = true, allow = [] } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`;

  const response = await fetch(`${BASE_URL}${pathname}`, {
    method,
    headers,
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
    throw new HttpError(response.status, data);
  }

  return { status: response.status, data };
}

async function ensureAuth() {
  const health = await api('/v1/health', { auth: false, allow: [200, 500] });
  if (health.status !== 200) throw new Error('API sağlık kontrolü başarısız.');

  try {
    const login = await api('/v1/auth/login', {
      method: 'POST',
      auth: false,
      body: { username: ADMIN_USER, password: ADMIN_PASS, deviceName: 'seed-rooms' },
      allow: [200, 400, 401, 404],
    });

    if (login.status === 200 && login.data?.accessToken) {
      accessToken = login.data.accessToken;
      return login.data.user;
    }
  } catch {
    // Kurulum denenir.
  }

  const setup = await api('/v1/auth/setup', {
    method: 'POST',
    auth: false,
    body: {
      username: ADMIN_USER,
      displayName: ADMIN_NAME,
      email: ADMIN_EMAIL,
      password: ADMIN_PASS,
      confirmPassword: ADMIN_PASS,
      deviceName: 'seed-rooms',
    },
    allow: [200, 400, 409],
  });

  if (setup.status === 200 && setup.data?.accessToken) {
    accessToken = setup.data.accessToken;
    return setup.data.user;
  }

  const login = await api('/v1/auth/login', {
    method: 'POST',
    auth: false,
    body: { username: ADMIN_USER, password: ADMIN_PASS, deviceName: 'seed-rooms' },
  });
  accessToken = login.data.accessToken;
  return login.data.user;
}

const TITLES = [
  'BEK Okulları Kurucusu',
  'Genel Müdür',
  'Finans ve Teknoloji Müdürü',
  'Kurumsal İletişim',
  'Müdür Yardımcısı',
  'Rehber Öğretmen',
];

const EXTRA_GUEST_NAMES = [
  ['Ayşe', 'Yılmaz'],
  ['Mehmet', 'Kaya'],
  ['Zeynep', 'Demir'],
  ['Ali', 'Çelik'],
  ['Elif', 'Şahin'],
  ['Burak', 'Aydın'],
  ['Selin', 'Arslan'],
  ['Kerem', 'Doğan'],
];

async function ensureTitles() {
  const existing = (await api('/v1/titles?activeOnly=false')).data ?? [];
  const byName = new Map(existing.map((title) => [title.name, title]));

  for (let i = 0; i < TITLES.length; i++) {
    if (byName.has(TITLES[i])) continue;
    await api('/v1/admin/content/titles', {
      method: 'POST',
      body: { name: TITLES[i], sortOrder: i, isActive: true },
      allow: [400, 409],
    });
  }

  return (await api('/v1/titles?activeOnly=false')).data ?? [];
}

async function assignTitles(titles) {
  const hosts = (await api('/v1/hosts')).data ?? [];
  const hostUsers = hosts.filter((host) => host.role === 'Host');
  let assigned = 0;

  for (let i = 0; i < hostUsers.length; i++) {
    const host = hostUsers[i];
    const title = titles[i % titles.length];
    if (!title || host.titleId === title.id) continue;

    await api(`/v1/admin/content/hosts/${host.id}`, {
      method: 'PUT',
      body: {
        displayName: host.displayName,
        isActive: host.isActive,
        titleId: title.id,
        clearTitle: false,
      },
      allow: [400],
    });
    assigned++;
  }

  return { hosts: hostUsers, assigned };
}

async function loadRooms() {
  const rooms = (await api('/v1/rooms?activeOnly=false')).data ?? [];
  const hostRooms = rooms.filter((room) => room.kind === 'HostRoom');
  const sharedRooms = rooms.filter((room) => room.kind === 'Shared' && room.isActive);
  return { rooms, hostRooms, sharedRooms };
}

async function loadAppointments() {
  const from = new Date();
  from.setDate(from.getDate() - 95);
  const to = new Date();
  to.setDate(to.getDate() + 95);

  const rows = (await api(
    `/v1/appointments/search?from=${localDate(from)}&to=${localDate(to)}`,
  )).data ?? [];

  return rows.filter((row) => !row.isDeleted);
}

function pick(list, seed) {
  if (list.length === 0) return null;
  return list[Math.abs(seed) % list.length];
}

function extraGuestsFor(index) {
  const count = index % 5 === 0 ? 2 : index % 3 === 0 ? 1 : 0;
  const guests = [];

  for (let i = 0; i < count; i++) {
    const [firstName, lastName] = EXTRA_GUEST_NAMES[(index + i) % EXTRA_GUEST_NAMES.length];
    guests.push({
      firstName,
      lastName,
      phone: `0533${String(1000000 + index * 97 + i).slice(-7)}`,
      isPrimary: false,
    });
  }

  return guests;
}

function participantsFor(hosts, hostId, index) {
  const others = hosts.filter((host) => host.id !== hostId);
  const count = index % 4 === 0 ? 2 : index % 2 === 0 ? 1 : 0;
  return others.slice(index % Math.max(1, others.length), index % Math.max(1, others.length) + count).map((host) => host.id);
}

function roomFor(detail, hostRooms, sharedRooms, index) {
  if (detail.method !== 'FaceToFace' && index % 5 !== 0) return null;

  if (index % 7 === 0) {
    return pick(sharedRooms, index)?.id ?? null;
  }

  return pick(hostRooms.filter((room) => room.hostUserId === detail.hostUserId), index)?.id
    ?? pick(sharedRooms, index)?.id
    ?? null;
}

function buildUpdate(detail, hosts, hostRooms, sharedRooms, index) {
  const guests = [
    {
      personId: detail.guests?.[0]?.personId ?? null,
      firstName: detail.personFirstName,
      lastName: detail.personLastName,
      phone: detail.phoneE164,
      isPrimary: true,
    },
    ...extraGuestsFor(index),
  ];

  return {
    firstName: detail.personFirstName,
    lastName: detail.personLastName,
    phone: detail.phoneE164,
    startsAtLocal: detail.startsAtLocal.slice(0, 19),
    durationMinutes: detail.durationMinutes,
    hostUserId: detail.hostUserId,
    categoryId: detail.categoryId,
    method: detail.method,
    description: detail.description,
    studentFirstName: detail.studentFirstName,
    studentLastName: detail.studentLastName,
    reminderOffsetsMinutes: detail.reminderOffsetsMinutes ?? [],
    ignoreConflicts: true,
    expectedUpdatedAtUtc: detail.updatedAtUtc,
    roomId: roomFor(detail, hostRooms, sharedRooms, index),
    participantHostUserIds: participantsFor(hosts, detail.hostUserId, index),
    guests,
  };
}

function outcomeAttendance(status, index, isPrimary) {
  if (status === 'NoShow') return 'NoShow';
  if (status === 'Cancelled') return 'Cancelled';
  if (status === 'Postponed') return 'Postponed';
  if (status === 'Completed' || status === 'Arrived') {
    if (!isPrimary && index % 4 === 0) return 'NoShow';
    return 'Arrived';
  }
  return null;
}

async function applyGuestOutcomes(id, status, index) {
  const attendance = outcomeAttendance(status, index, true);
  if (!attendance) return false;

  const detail = (await api(`/v1/appointments/${id}`)).data;
  if (!detail?.guests?.length) return false;

  const guestOutcomes = detail.guests.map((guest, guestIndex) => ({
    guestId: guest.guestId,
    attendance: outcomeAttendance(status, index + guestIndex, guest.isPrimary) ?? 'Arrived',
    summary: guestIndex === 0 ? 'UAT misafir değerlendirmesi.' : 'Ek misafir değerlendirmesi.',
    willRepeat: guestIndex === 0 && index % 9 === 0,
    postNotes: guestIndex === 0 ? 'Takip notu.' : null,
    isClosed: status === 'Completed' || status === 'NoShow' || status === 'Cancelled',
  }));

  const result = await api(`/v1/appointments/${id}/outcome`, {
    method: 'PUT',
    body: {
      attendance,
      summary: 'UAT toplu değerlendirmesi.',
      willRepeat: index % 9 === 0,
      postNotes: 'UAT takip notu.',
      isClosed: status === 'Completed' || status === 'NoShow' || status === 'Cancelled',
      guestOutcomes,
    },
    allow: [400, 409],
  });

  return result.status === 204;
}

async function createRoomConflicts(rooms, hosts, categoryId, count) {
  if (count <= 0) return 0;

  const shared = rooms.sharedRooms[0];
  if (!shared) return 0;

  const now = new Date();
  let created = 0;

  for (let i = 0; i < count; i++) {
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 4);
    date.setHours(15, 0, 0, 0);

    const result = await api('/v1/appointments', {
      method: 'POST',
      body: {
        firstName: 'Oda',
        lastName: `Çakışma ${i + 1}`,
        phone: `0544${String(2000000 + i).slice(-7)}`,
        startsAtLocal: localStamp(date),
        durationMinutes: 30,
        hostUserId: hosts[i % hosts.length].id,
        categoryId,
        method: 'FaceToFace',
        description: 'UAT oda çakışması örneği.',
        studentFirstName: null,
        studentLastName: null,
        reminderOffsetsMinutes: [30],
        ignoreConflicts: true,
        clientRequestId: `${SEED_VERSION}-room-conflict-${pad(i + 1)}`,
        roomId: shared.id,
        participantHostUserIds: [],
        guests: [
          {
            firstName: 'Oda',
            lastName: `Çakışma ${i + 1}`,
            phone: `0544${String(2000000 + i).slice(-7)}`,
            isPrimary: true,
          },
        ],
      },
      allow: [400, 409],
    });

    if (result.status === 200 && result.data?.success) created++;
  }

  return created;
}

async function main() {
  if (!FORCE) {
    try {
      const state = JSON.parse(await readFile(STATE_PATH, 'utf8'));
      if (state?.baseUrl === BASE_URL && state?.seedVersion === SEED_VERSION) {
        console.log(`Bu zenginleştirme ${state.seededAt} tarihinde zaten çalıştırılmış.`);
        console.log('Yeniden çalıştırmak için --force kullanın.');
        return;
      }
    } catch {
      // İlk çalıştırma.
    }
  }

  await ensureAuth();
  const titles = await ensureTitles();
  const { hosts, assigned } = await assignTitles(titles);
  const rooms = await loadRooms();
  const appointments = await loadAppointments();

  console.log(`Ünvan: ${titles.length} · görüşmeci: ${hosts.length} (atama: ${assigned})`);
  console.log(`Oda: ${rooms.rooms.length} (host: ${rooms.hostRooms.length}, ortak: ${rooms.sharedRooms.length})`);
  console.log(`İşlenecek randevu: ${appointments.length}`);

  let updated = 0;
  let skipped = 0;
  let outcomes = 0;
  let failed = 0;

  for (let index = 0; index < appointments.length; index++) {
    const row = appointments[index];

    try {
      const detail = (await api(`/v1/appointments/${row.id}`)).data;
      if (!detail) {
        skipped++;
        continue;
      }

      if ((detail.guests?.length ?? 0) > 1 && (detail.participants?.length ?? 0) > 1) {
        skipped++;
        continue;
      }

      const body = buildUpdate(detail, hosts, rooms.hostRooms, rooms.sharedRooms, index);
      const result = await api(`/v1/appointments/${row.id}`, {
        method: 'PUT',
        body,
        allow: [400, 409],
      });

      const updateOk =
        result.status === 204 ||
        (result.status === 200 && result.data?.success !== false);

      if (!updateOk) {
        failed++;
        if (failed <= 5) {
          console.log(`  ! ${row.id}: ${result.status} ${result.data?.error ?? ''}`);
        }
        continue;
      }

      updated++;

      if (row.status !== 'Scheduled' && row.status !== 'Deleted') {
        if (await applyGuestOutcomes(row.id, row.status, index)) outcomes++;
      }
    } catch (error) {
      failed++;
      if (failed <= 5) console.log(`  ! ${row.id}: ${error.message}`);
    }

    if ((index + 1) % 100 === 0) {
      console.log(`  ${index + 1}/${appointments.length} işlendi…`);
    }
  }

  const conflictCount = await createRoomConflicts(
    rooms,
    hosts,
    appointments[0]?.categoryId ?? (await api('/v1/categories?activeOnly=true')).data?.[0]?.id,
    EXTRA_CONFLICTS,
  );

  const summary = {
    baseUrl: BASE_URL,
    seedVersion: SEED_VERSION,
    seededAt: new Date().toISOString(),
    titles: titles.length,
    hosts: hosts.length,
    rooms: rooms.rooms.length,
    appointments: appointments.length,
    updated,
    skipped,
    outcomes,
    failed,
    roomConflicts: conflictCount,
  };

  await writeFile(STATE_PATH, JSON.stringify(summary, null, 2));

  console.log('\nÖzet:');
  console.log(`  Güncellenen: ${updated} · atlanan: ${skipped} · sonuç: ${outcomes} · hata: ${failed}`);
  console.log(`  Ek oda çakışması: ${conflictCount}`);
  console.log(`  Durum dosyası: ${STATE_PATH}`);
  console.log('\nDoğrulama: node scripts/verify-uat.mjs');
}

main().catch((error) => {
  console.error('\nHata:', error.message);
  if (error instanceof HttpError) console.error(error.body);
  process.exit(1);
});
