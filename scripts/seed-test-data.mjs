#!/usr/bin/env node
/**
 * CGP - Randevu Takip · UAT test verisi üreteci
 *
 * API'ye (tek doğruluk kaynağı) geçmiş / bugün / gelecek günler için karışık
 * randevular yazar. Masaüstü (okuma modu), web ve mobil aynı kayıtları görür.
 *
 * Kullanım:
 *   node scripts/seed-test-data.mjs                       # 1000 tekil + 8 seri
 *   node scripts/seed-test-data.mjs --count 1500          # daha fazla
 *   node scripts/seed-test-data.mjs --base-url http://127.0.0.1:5199
 *   node scripts/seed-test-data.mjs --admin-user admin --admin-pass "CgpDemo!2026"
 *   node scripts/seed-test-data.mjs --force               # durum dosyasını yok say
 *
 * Notlar:
 * - Kayıtlar deterministik `clientRequestId` taşır (`demo-2026-11-22-0001` ...).
 *   Script tekrar çalıştırılırsa aynı kayıtlar ikinci kez oluşturulmaz.
 * - Çakışma testi için bazı kayıtlar bilerek aynı host + saatte oluşturulur
 *   (`ignoreConflicts: true`).
 * - Geçmiş kayıtların bir kısmı tamamlanır/gelmedi/iptal/ertelendi olarak
 *   kapatılır; bir kısmı "geçmişte planlı" bırakılır (düzenleme/taşıma testi).
 * - Bugün için önümüzdeki dakikalara hatırlatıcı denemeleri eklenir.
 */
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// ---------------------------------------------------------------- argümanlar

const args = process.argv.slice(2);
const arg = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};
const flag = (name) => args.includes(name);

const BASE_URL = arg('--base-url', 'http://127.0.0.1:5199').replace(/\/+$/, '');
const TOTAL = Number(arg('--count', '1000'));
const ADMIN_USER = arg('--admin-user', 'admin');
const ADMIN_PASS = arg('--admin-pass', 'CgpDemo!2026');
const ADMIN_NAME = arg('--admin-name', 'Demo Yönetici');
const ADMIN_EMAIL = arg('--admin-email', 'demo@cgp.local');
const FORCE = flag('--force');
const NO_SERIES = flag('--no-series');

if (!Number.isFinite(TOTAL) || TOTAL < 1) {
  console.error('--count pozitif bir sayı olmalı.');
  process.exit(1);
}

const SCRIPT_DIR = path.dirname(fileURLToPath(import.meta.url));
const STATE_PATH = path.join(SCRIPT_DIR, '.seed-demo-state.json');
const SEED_VERSION = 'demo-2026-11-22';

// ---------------------------------------------------------------- yardımcılar

const pad = (n, w = 2) => String(n).padStart(w, '0');
const localDate = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const localStamp = (d) =>
  `${localDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const rng = mulberry32(20261122);
const pick = (list) => list[Math.floor(rng() * list.length)];
const rint = (min, max) => min + Math.floor(rng() * (max - min + 1));

class HttpError extends Error {
  constructor(status, body) {
    super(`HTTP ${status}: ${typeof body === 'string' ? body : JSON.stringify(body)}`);
    this.status = status;
    this.body = body;
  }
}

let accessToken = '';
let adminUser = null;

async function api(pathname, { method = 'GET', body, auth = true, allow = [] } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`;

  const response = await fetch(`${BASE_URL}${pathname}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(30_000),
  });

  const text = await response.text();
  let parsed = null;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = text;
    }
  }

  if (!response.ok && !allow.includes(response.status)) {
    throw new HttpError(response.status, parsed);
  }

  return { status: response.status, data: parsed };
}

// ---------------------------------------------------------------- veri havuzu

const FIRST_NAMES = [
  'Ayşe', 'Mehmet', 'Zeynep', 'Mustafa', 'Elif', 'Ahmet', 'Fatma', 'Hasan',
  'Emine', 'Hüseyin', 'Hatice', 'İbrahim', 'Merve', 'Murat', 'Özlem', 'Ömer',
  'Sevgi', 'Serkan', 'Derya', 'Kemal', 'Sibel', 'Yusuf', 'Gül', 'Ali',
  'Pınar', 'Burak', 'Esra', 'Cem', 'Nesrin', 'Okan', 'Sema', 'Levent',
  'Gamze', 'Tolga', 'Burcu', 'Onur', 'Aylin', 'Emre', 'Dilek', 'Barış',
  'Nurgül', 'Kaan', 'Tuğba', 'Uğur', 'Büşra', 'Cihan', 'Ebru', 'Selim',
  'Aslı', 'Volkan', 'Ceren', 'Oğuz', 'Melis', 'Sinan', 'Yasemin', 'Berk',
  'İpek', 'Ege', 'Nilüfer', 'Furkan', 'Selin', 'Arda', 'Deniz', 'Mert',
  'Su', 'Ekin', 'Nazlı', 'Kerem', 'Sıla', 'Koray', 'İrem', 'Tunç',
  'Duygu', 'Eren', 'Gökçe', 'Alper', 'Hande', 'Bora', 'Ezgi', 'Cengiz',
  'Şule', 'Ozan', 'Damla', 'Efe', 'Hilal', 'Umut', 'Beyza', 'Serdar',
  'Zehra', 'Mesut', 'Cansu', 'Hakan', 'İlayda', 'Turgut', 'Begüm', 'Erdem',
  'Aleyna', 'Yalçın', 'Sude', 'Bilal', 'Nisa', 'Fatih', 'Azra', 'Adem',
];

const LAST_NAMES = [
  'Yılmaz', 'Kaya', 'Demir', 'Şahin', 'Çelik', 'Yıldız', 'Yıldırım', 'Öztürk',
  'Aydın', 'Özdemir', 'Arslan', 'Doğan', 'Kılıç', 'Aslan', 'Çetin', 'Kara',
  'Koç', 'Kurt', 'Özkan', 'Şimşek', 'Polat', 'Özcan', 'Korkmaz', 'Çakır',
  'Erdoğan', 'Yavuz', 'Güneş', 'Bulut', 'Aksoy', 'Tekin', 'Taş', 'Güler',
  'Turan', 'Kaplan', 'Bozkurt', 'Uysal', 'Coşkun', 'Ünal', 'Sarı', 'Acar',
  'Keskin', 'Balcı', 'Ateş', 'Akgün', 'Sezer', 'Sönmez', 'Erdem', 'Gündüz',
  'Bayram', 'Yalçın', 'Şen', 'Altın', 'Mercan', 'Ilgaz', 'Savaş', 'Duman',
  'Kılıçarslan', 'Toprak', 'Gürbüz', 'Avcı', 'Karaca', 'Ceylan', 'Pekcan',
  'Sert', 'Varol', 'Aydemir', 'Köse', 'Tuncer', 'Arı', 'Yorgancı', 'Dönmez',
  'Kazan', 'Uçar', 'Balaban', 'Dağ', 'Gür', 'Alp', 'Tunç', 'Ergin',
];

const HOST_NAMES = [
  'Demet Yalçın', 'Kerem Öztürk', 'Nazlı Güneş', 'Serkan Aksoy',
  'Pınar Doğan', 'Emre Kurt', 'Gizem Polat', 'Onur Tekin',
];

const TOPICS = [
  'Matematik dersindeki gelişim ve haftalık çalışma planı görüşülecek.',
  'Devamsızlık ve okula uyum süreci değerlendirilecek.',
  'Sınav kaygısı ve motivasyon üzerine rehberlik yapılacak.',
  'Yazılı sınav sonuçları ve eksik kazanımlar konuşulacak.',
  'Akran ilişkileri ve sınıf içi davranışlar ele alınacak.',
  'Üniversite tercih süreci ve hedef bölümler değerlendirilecek.',
  'Burs başvurusu ve evrak eksikleri görüşülecek.',
  'Ders seçimi ve haftalık program planlanacak.',
  'Teknoloji kullanımı ve ekran süresi üzerine öneriler paylaşılacak.',
  'Okul-aile iş birliği ve dönem hedefleri gözden geçirilecek.',
  'Rehberlik servisi yönlendirmesi ve destek planı netleştirilecek.',
  'Beslenme ve uyku düzeni hakkında bilgi alışverişi yapılacak.',
  'Proje ödevi ve grup çalışması değerlendirilecek.',
  'Yeni dönem için ders dışı etkinlik planı konuşulacak.',
  'Spor ve kulüp faaliyetlerine katılım değerlendirilecek.',
  'Servis ve ulaşım düzeni ile ilgili talepler görüşülecek.',
];

const OUTCOME_NOTES = [
  'Görüşme planlandığı gibi tamamlandı; haftalık çalışma takvimi paylaşıldı.',
  'Veli ile birlikte kısa vadeli hedefler belirlendi.',
  'Öğrenciye destek planı çıkarıldı, iki hafta sonra tekrar değerlendirilecek.',
  'Rehberlik servisine yönlendirme yapıldı.',
  'Katılım sağlanmadı, telefonla bilgilendirme denendi.',
  'Evrak eksikleri tamamlanmak üzere not alındı.',
];

function buildPersons(count) {
  const seen = new Set();
  const persons = [];
  while (persons.length < count) {
    const firstName = pick(FIRST_NAMES);
    const lastName = pick(LAST_NAMES);
    const key = `${firstName} ${lastName}`;
    if (seen.has(key)) continue;
    seen.add(key);
    persons.push({
      firstName,
      lastName,
      phone: `0532${String(1_000_000 + persons.length * 7919).slice(-7)}`,
    });
  }
  return persons;
}

const STUDENT_FIRST = [
  'Ada', 'Alp', 'Aras', 'Arya', 'Asya', 'Atlas', 'Azra', 'Bade', 'Barkın', 'Batu',
  'Beren', 'Bora', 'Can', 'Cemre', 'Çınar', 'Defne', 'Deniz', 'Derin', 'Doruk', 'Duru',
  'Ece', 'Ege', 'Elif', 'Emin', 'Emir', 'Eray', 'Esin', 'Eymen', 'Gökçe', 'İdil',
  'İpek', 'Kaan', 'Lara', 'Maya', 'Mert', 'Mira', 'Nehir', 'Nisa', 'Ozan', 'Öykü',
  'Poyraz', 'Rüzgar', 'Sarp', 'Sena', 'Sıla', 'Tuna', 'Uras', 'Umay', 'Yağız', 'Zeynep',
];

function studentFor(person, index) {
  if (rng() < 0.18) return { studentFirstName: null, studentLastName: null };
  return {
    studentFirstName: STUDENT_FIRST[(index * 7 + 3) % STUDENT_FIRST.length],
    studentLastName: person.lastName,
  };
}

// ---------------------------------------------------------------- kimlik

async function ensureAuth() {
  const health = await api('/v1/health', { auth: false });
  console.log(`API hazır: ${health.data?.status ?? 'ok'} · v${health.data?.version ?? '?'} · ${BASE_URL}`);

  const status = await api('/v1/auth/status', { auth: false });
  if (status.data?.isFirstRun) {
    console.log(`İlk kurulum yapılıyor: ${ADMIN_USER}`);
    const setup = await api('/v1/auth/setup', {
      method: 'POST',
      auth: false,
      body: {
        username: ADMIN_USER,
        displayName: ADMIN_NAME,
        email: ADMIN_EMAIL,
        password: ADMIN_PASS,
        confirmPassword: ADMIN_PASS,
        deviceName: 'seed-script',
      },
    });
    accessToken = setup.data.accessToken;
    adminUser = setup.data.user;
    console.log(`Yönetici oluşturuldu: ${ADMIN_USER} / ${ADMIN_PASS}`);
    return;
  }

  try {
    const login = await api('/v1/auth/login', {
      method: 'POST',
      auth: false,
      body: { username: ADMIN_USER, password: ADMIN_PASS, deviceName: 'seed-script' },
    });
    accessToken = login.data.accessToken;
    adminUser = login.data.user;
  } catch (error) {
    if (error instanceof HttpError && error.status === 401) {
      throw new Error(
        `Giriş başarısız (${ADMIN_USER}). Mevcut yönetici hesabıyla çalıştırın:\n` +
        `  node scripts/seed-test-data.mjs --admin-user <kullanıcı> --admin-pass "<şifre>"`);
    }
    throw error;
  }
  console.log(`Giriş yapıldı: ${adminUser.displayName} (${adminUser.role})`);
}

// ---------------------------------------------------------------- katalog

async function ensureHosts() {
  const existing = (await api('/v1/hosts')).data;
  const byName = new Map(existing.map((h) => [h.displayName, h]));
  const created = [];
  for (const displayName of HOST_NAMES) {
    if (byName.has(displayName)) continue;
    await api('/v1/admin/content/hosts', {
      method: 'POST',
      body: { displayName, loginEnabled: false, username: null, password: null, confirmPassword: null },
    });
    created.push(displayName);
  }

  const hosts = (await api('/v1/hosts')).data;
  const selected = HOST_NAMES
    .map((name) => hosts.find((h) => h.displayName === name))
    .filter(Boolean);
  if (selected.length === 0) throw new Error('Görüşmeci (host) bulunamadı.');
  if (created.length) console.log(`Görüşmeci eklendi: ${created.join(', ')}`);
  console.log(`Görüşmeci sayısı: ${selected.length}`);
  return selected;
}

async function loadCategories() {
  const categories = (await api('/v1/categories?activeOnly=true')).data;
  if (!categories?.length) throw new Error('Aktif kategori bulunamadı.');
  console.log(`Kategori sayısı: ${categories.length}`);
  return categories;
}

// ---------------------------------------------------------------- plan

const METHOD_WEIGHTS = ['FaceToFace', 'FaceToFace', 'FaceToFace', 'Phone', 'Phone', 'WhatsApp', 'Online'];
const DURATION_WEIGHTS = [30, 30, 30, 30, 15, 45, 60];
const REMINDER_PRESETS = [
  [], [30], [30], [30], [60], [60], [1440], [30, 60], [1440, 60], [1440, 60, 30],
];

const PAST_STATUS_TABLE = [
  ['Completed', 0.58], ['NoShow', 0.14], ['Cancelled', 0.09],
  ['Postponed', 0.07], ['Arrived', 0.05], ['Scheduled', 0.07],
];

function weighted(table) {
  const roll = rng();
  let acc = 0;
  for (const [value, weight] of table) {
    acc += weight;
    if (roll <= acc) return value;
  }
  return table[table.length - 1][0];
}

function buildDayPlan(now) {
  const counts = new Map();
  const add = (offset, n) => counts.set(offset, (counts.get(offset) ?? 0) + n);

  // Ağır günler: liste/performans ve çakışma testi için.
  add(0, 100);
  add(1, 46);
  add(-1, 40);
  add(-2, 32);
  add(2, 30);
  add(3, 26);
  add(7, 40);
  add(14, 34);
  add(-7, 30);
  add(-14, 26);
  add(-30, 24);
  add(30, 30);
  add(45, 22);
  add(60, 25);
  add(-60, 22);

  let assigned = [...counts.values()].reduce((a, b) => a + b, 0);
  let attempts = 0;
  while (assigned < TOTAL && attempts < TOTAL * 200) {
    attempts++;
    const roll = rng();
    let offset;
    if (roll < 0.55) offset = rint(3, 90);
    else if (roll < 0.85) offset = rint(-90, -3);
    else offset = rint(-2, 2);

    if (offset === 0) continue;
    if ((counts.get(offset) ?? 0) >= 70) continue;
    // Pazar günleri çoğunlukla boş kalsın (boş gün görünümü testi).
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate() + offset);
    if (date.getDay() === 0 && rng() < 0.75) continue;

    add(offset, 1);
    assigned++;
  }

  // Bugünün saatleri geçmişte kaldıysa fazla kaydı yakın günlere taşı.
  const minutesNow = now.getHours() * 60 + now.getMinutes();
  if (minutesNow > 15 * 60) {
    const move = Math.min(counts.get(0) - 20, 55);
    if (move > 0) {
      counts.set(0, counts.get(0) - move);
      add(1, move);
    }
  }

  return counts;
}

function statusFor(offset, startsAt, now) {
  if (offset < 0) return weighted(PAST_STATUS_TABLE);
  if (offset === 0) {
    if (startsAt < now) {
      if (now - startsAt < 120 * 60_000) return weighted([['Completed', 0.5], ['Arrived', 0.2], ['Scheduled', 0.3]]);
      return weighted(PAST_STATUS_TABLE);
    }
    return weighted([['Scheduled', 0.9], ['Postponed', 0.05], ['Cancelled', 0.05]]);
  }
  return weighted([['Scheduled', 0.93], ['Postponed', 0.04], ['Cancelled', 0.03]]);
}

function outcomeFor(status) {
  switch (status) {
    case 'Completed':
      return { attendance: 'Arrived', isClosed: true };
    case 'NoShow':
      return { attendance: 'NoShow', isClosed: true };
    case 'Cancelled':
      return { attendance: 'Cancelled', isClosed: true };
    case 'Postponed':
      return { attendance: 'Postponed', isClosed: false };
    case 'Arrived':
      return { attendance: 'Arrived', isClosed: false };
    default:
      return null;
  }
}

function makeSlot(offset, now, hosts, categories, index, persons, registry) {
  const base = new Date(now.getFullYear(), now.getMonth(), now.getDate() + offset);
  const person = persons[index % persons.length];

  let date = null;
  let host = null;
  let duration = 30;
  for (let attempt = 0; attempt < 30 && !date; attempt++) {
    host = pick(hosts);
    duration = pick(DURATION_WEIGHTS);
    const candidate = new Date(base);
    candidate.setHours(rint(8, 19), pick([0, 0, 15, 30, 30, 45]), 0, 0);
    if (candidate.getHours() >= 19 && duration > 45) continue;
    if (tryAllocate(registry, candidate, host.id, duration)) date = candidate;
  }

  let ignoreConflicts = false;
  if (!date) {
    // Aşırı yoğun günde boş aralık bulunamazsa çakışmaya izin ver.
    host = pick(hosts);
    duration = 30;
    date = new Date(base);
    date.setHours(rint(8, 19), pick([0, 30]), 0, 0);
    ignoreConflicts = true;
  }

  const category = pick(categories);
  const student = studentFor(person, index);
  const topic = pick(TOPICS);
  const status = statusFor(offset, date, now);

  return {
    index,
    clientRequestId: `${SEED_VERSION}-${String(index + 1).padStart(4, '0')}`,
    firstName: person.firstName,
    lastName: person.lastName,
    phone: person.phone,
    startsAtLocal: date,
    durationMinutes: duration,
    hostUserId: host.id,
    categoryId: category.id,
    method: pick(METHOD_WEIGHTS),
    description: `${topic} · Test kaydı #${index + 1}`,
    ...student,
    reminderOffsetsMinutes: pick(REMINDER_PRESETS),
    status,
    ignoreConflicts,
    delete: false,
    source: 'single',
  };
}

function slotKey(date, hostId) {
  return `${localDate(date)}|${hostId}`;
}

/** Aynı host + gün için çakışmayan aralık ayırır. */
function tryAllocate(registry, date, hostId, durationMinutes) {
  const key = slotKey(date, hostId);
  const list = registry.get(key) ?? [];
  const start = date.getHours() * 60 + date.getMinutes();
  const end = start + durationMinutes;
  if (end > 20 * 60 + 45) return false;
  if (list.some(([s, e]) => start < e && end > s)) return false;
  list.push([start, end]);
  registry.set(key, list);
  return true;
}

function registerSlot(registry, date, hostId, durationMinutes) {
  const key = slotKey(date, hostId);
  const list = registry.get(key) ?? [];
  const start = date.getHours() * 60 + date.getMinutes();
  list.push([start, start + durationMinutes]);
  registry.set(key, list);
}

function addConflictClusters(specs, now) {
  const byDay = new Map();
  for (const spec of specs) {
    if (spec.status !== 'Scheduled') continue;
    if (spec.startsAtLocal < now) continue;
    const key = localDate(spec.startsAtLocal);
    if (!byDay.has(key)) byDay.set(key, []);
    byDay.get(key).push(spec);
  }

  let conflicts = 0;
  for (const daySpecs of byDay.values()) {
    if (daySpecs.length < 8) continue;
    const clusters = daySpecs.length >= 30 ? 4 : daySpecs.length >= 18 ? 2 : 1;

    for (let c = 0; c < clusters; c++) {
      const base = daySpecs[rint(0, Math.min(daySpecs.length - 1, 24))];
      const person = specs[(base.index * 37 + 11) % specs.length];
      const clone = {
        ...base,
        clientRequestId: `${SEED_VERSION}-conflict-${String(conflicts + 1).padStart(3, '0')}`,
        firstName: person.firstName,
        lastName: person.lastName,
        phone: person.phone,
        description: `${base.description} · Bilinçli çakışma testi`,
        ignoreConflicts: true,
        status: 'Scheduled',
        reminderOffsetsMinutes: [],
        delete: false,
        source: 'conflict',
      };
      specs.push(clone);
      conflicts++;

      // Aynı kişi + aynı host + aynı saat (aynı kişi çakışması uyarısı).
      if (c === 0 && daySpecs.length >= 20) {
        specs.push({
          ...clone,
          clientRequestId: `${SEED_VERSION}-sameperson-${String(conflicts).padStart(3, '0')}`,
          description: `${clone.description} · Aynı kişi çakışması`,
        });
      }
    }
  }
  return conflicts;
}

function addReminderShowcase(specs, now, hosts, categories, persons, startIndex, registry) {
  const created = [];
  const add = (minutesAhead, offsets, label) => {
    const at = new Date(now.getTime() + minutesAhead * 60_000);
    if (at.getHours() < 8 || at.getHours() > 20) return;
    const person = persons[(startIndex + created.length + 400) % persons.length];
    const student = studentFor(person, startIndex + created.length);
    const host = pick(hosts);
    registerSlot(registry, at, host.id, 30);
    created.push({
      index: startIndex + created.length,
      clientRequestId: `${SEED_VERSION}-reminder-${created.length + 1}`,
      firstName: person.firstName,
      lastName: person.lastName,
      phone: person.phone,
      startsAtLocal: at,
      durationMinutes: 30,
      hostUserId: host.id,
      categoryId: pick(categories).id,
      method: pick(METHOD_WEIGHTS),
      description: `Hatırlatıcı testi: ${label} · Test kaydı`,
      studentFirstName: student.studentFirstName,
      studentLastName: person.lastName,
      reminderOffsetsMinutes: offsets,
      status: 'Scheduled',
      ignoreConflicts: true,
      delete: false,
      source: 'reminder',
    });
  };

  add(36, [30], '30 dk önce bildirimi');
  add(41, [30], '30 dk önce bildirimi (2)');
  add(66, [60], '60 dk önce bildirimi');
  add(71, [60], '60 dk önce bildirimi (2)');

  // Yarın için 1 gün (1440 dk) önce hatırlatması: bugün ilerleyen saatte tetiklenir.
  const tomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, 11, 15, 0, 0);
  if (tomorrow > now) {
    const person = persons[(startIndex + 500) % persons.length];
    const student = studentFor(person, startIndex + 1);
    const host = pick(hosts);
    registerSlot(registry, tomorrow, host.id, 30);
    created.push({
      index: startIndex + created.length,
      clientRequestId: `${SEED_VERSION}-reminder-1440`,
      firstName: person.firstName,
      lastName: person.lastName,
      phone: person.phone,
      startsAtLocal: tomorrow,
      durationMinutes: 30,
      hostUserId: host.id,
      categoryId: pick(categories).id,
      method: 'FaceToFace',
      description: 'Günlük hatırlatıcı testi: yarından 24 saat önce bildirim · Test kaydı',
      studentFirstName: student.studentFirstName,
      studentLastName: person.lastName,
      reminderOffsetsMinutes: [1440],
      status: 'Scheduled',
      ignoreConflicts: true,
      delete: false,
      source: 'reminder',
    });
  }

  specs.push(...created);
  return created.length;
}

function buildSpecs(now, hosts, categories, persons, registry) {
  const dayPlan = buildDayPlan(now);
  const specs = [];
  let index = 0;

  for (const [offset, count] of [...dayPlan.entries()].sort((a, b) => a[0] - b[0])) {
    for (let i = 0; i < count; i++) {
      specs.push(makeSlot(offset, now, hosts, categories, index++, persons, registry));
    }
  }

  const conflicts = addConflictClusters(specs, now);
  const reminders = addReminderShowcase(specs, now, hosts, categories, persons, index, registry);

  // Test için silinenler (son 30 gün içinde oluşturulmuş olur).
  const deletable = specs.filter((s) => s.source === 'single' && s.status === 'Scheduled');
  for (let i = 0; i < 18 && deletable.length; i++) {
    deletable[(i * 41 + 7) % deletable.length].delete = true;
  }

  specs.sort((a, b) => a.startsAtLocal - b.startsAtLocal);
  return { specs, persons, conflicts, reminders };
}

// ---------------------------------------------------------------- yazma

async function createSeries(now, hosts, categories, persons, registry) {
  if (NO_SERIES) return [];
  const created = [];
  const frequencies = ['Weekly', 'Biweekly', 'Monthly'];
  const times = [['09:15', 30], ['10:30', 45], ['11:00', 30], ['13:15', 30], ['14:00', 45], ['15:30', 30], ['16:15', 60], ['17:00', 30]];
  const offsets = [1, 2, 3, 5, 8, 10, 12, 15];

  for (let i = 0; i < Math.min(hosts.length, 8); i++) {
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() + offsets[i]);
    const [hh, mm] = times[i][0].split(':').map(Number);
    start.setHours(hh, mm, 0, 0);
    const person = persons[(i * 13 + 2) % persons.length];

    const body = {
      input: {
        firstName: person.firstName,
        lastName: person.lastName,
        phone: person.phone,
        startsAtLocal: localStamp(start),
        durationMinutes: times[i][1],
        hostUserId: hosts[i].id,
        categoryId: categories[i % categories.length].id,
        method: i % 2 === 0 ? 'FaceToFace' : 'Online',
        description: `Haftalık takip serisi (${frequencies[i % 3]}) · Test kaydı · Seri ${i + 1}`,
        studentFirstName: STUDENT_FIRST[(i * 5) % STUDENT_FIRST.length],
        studentLastName: person.lastName,
        reminderOffsetsMinutes: [1440, 60],
        ignoreConflicts: false,
        clientRequestId: `${SEED_VERSION}-series-${String(i + 1).padStart(2, '0')}`,
      },
      frequency: frequencies[i % 3],
      count: 5 + (i % 4),
    };

    const result = await api('/v1/appointments/series', { method: 'POST', body, allow: [400, 409] });
    if (result.status === 200 && result.data?.success) {
      created.push({ seriesId: result.data.seriesId, created: result.data.createdCount });
      for (const slot of result.data.slots ?? []) {
        if (slot.willCreate || slot.state === 'Create') {
          registerSlot(registry, new Date(slot.startsAtLocal), hosts[i].id, times[i][1]);
        }
      }
      console.log(`Seri ${i + 1}: ${result.data.createdCount} kayıt (${frequencies[i % 3]})`);
    } else {
      console.log(`Seri ${i + 1} atlandı: ${result.data?.error ?? 'bilinmeyen hata'}`);
    }
  }
  return created;
}

async function applyOutcome(id, status) {
  const outcome = outcomeFor(status);
  if (!outcome) return false;

  let detail;
  try {
    detail = (await api(`/v1/appointments/${id}`)).data;
  } catch {
    return false;
  }
  if (!detail) return false;
  if (detail.status === 'Completed' || detail.status === 'Cancelled') return false;
  if (detail.status === status) return false;

  const payload = {
    attendance: outcome.attendance,
    summary: pick(OUTCOME_NOTES),
    willRepeat: rng() < 0.3,
    postNotes: rng() < 0.5 ? `Takip: ${pick(OUTCOME_NOTES)}` : null,
    isClosed: outcome.isClosed,
  };

  const result = await api(`/v1/appointments/${id}/outcome`, {
    method: 'PUT',
    body: payload,
    allow: [400, 409],
  });
  return result.status === 204;
}

async function writeSpecs(specs) {
  const ids = new Map();
  const failures = [];
  let created = 0;
  let outcomes = 0;
  let deleted = 0;
  let processed = 0;

  for (const spec of specs) {
    const body = {
      firstName: spec.firstName,
      lastName: spec.lastName,
      phone: spec.phone,
      startsAtLocal: localStamp(spec.startsAtLocal),
      durationMinutes: spec.durationMinutes,
      hostUserId: spec.hostUserId,
      categoryId: spec.categoryId,
      method: spec.method,
      description: spec.description,
      studentFirstName: spec.studentFirstName,
      studentLastName: spec.studentLastName,
      reminderOffsetsMinutes: spec.reminderOffsetsMinutes,
      ignoreConflicts: spec.ignoreConflicts,
      clientRequestId: spec.clientRequestId,
      expectedUpdatedAtUtc: null,
    };

    let result;
    try {
      result = await api('/v1/appointments', { method: 'POST', body, allow: [400, 409] });
    } catch (error) {
      failures.push({ id: spec.clientRequestId, error: error.message });
      continue;
    }

    if (result.status !== 200 || !result.data?.success || !result.data.appointmentId) {
      failures.push({
        id: spec.clientRequestId,
        error: result.data?.error ?? `HTTP ${result.status}`,
      });
      continue;
    }

    const id = result.data.appointmentId;
    ids.set(spec.clientRequestId, id);
    created++;

    if (spec.status !== 'Scheduled') {
      if (await applyOutcome(id, spec.status)) outcomes++;
    }

    if (spec.delete) {
      const del = await api(`/v1/appointments/${id}`, { method: 'DELETE', allow: [400, 404, 409] });
      if (del.status === 204) deleted++;
    }

    processed++;
    if (processed % 50 === 0) {
      console.log(`  ${processed}/${specs.length} kayıt işlendi…`);
    }
  }

  return { ids, failures, created, outcomes, deleted };
}

async function dayCounts(fromOffset, toOffset) {
  const from = new Date();
  from.setDate(from.getDate() + fromOffset);
  const to = new Date();
  to.setDate(to.getDate() + toOffset);
  const rows = (await api(
    `/v1/appointments/search?from=${localDate(from)}&to=${localDate(to)}`)).data ?? [];

  const byDay = new Map();
  for (const row of rows) {
    const day = row.startsAtLocal.slice(0, 10);
    byDay.set(day, (byDay.get(day) ?? 0) + 1);
  }
  return { rows, byDay };
}

// ---------------------------------------------------------------- ana akış

async function main() {
  const now = new Date();

  if (!FORCE) {
    let state = null;
    try {
      state = JSON.parse(await readFile(STATE_PATH, 'utf8'));
    } catch {
      state = null;
    }

    if (state?.baseUrl === BASE_URL && state?.seedVersion === SEED_VERSION && state.count >= TOTAL) {
      console.log(`Bu veri tabanı ${state.seededAt} tarihinde zaten doldurulmuş görünüyor.`);
      console.log(`Durum dosyası: ${STATE_PATH}`);
      console.log('Yeniden oluşturmak için --force kullanın.');
      return;
    }
  }

  await ensureAuth();
  const hosts = await ensureHosts();
  const categories = await loadCategories();

  const persons = buildPersons(240);
  const registry = new Map();
  const series = await createSeries(now, hosts, categories, persons, registry);
  const { specs } = buildSpecs(now, hosts, categories, persons, registry);

  console.log(`\n${specs.length} tekil/çakışma kaydı yazılıyor (${localDate(now)})…`);
  const result = await writeSpecs(specs);

  if (result.failures.length) {
    console.log(`\nBaşarısız kayıt: ${result.failures.length}`);
    for (const failure of result.failures.slice(0, 10)) {
      console.log(`  - ${failure.id}: ${failure.error}`);
    }
  }

  console.log('\nÖzet doğrulaması (sunucudan):');
  const window = await dayCounts(-95, 95);
  const days = [...window.byDay.entries()].sort((a, b) => a[0].localeCompare(b[0]));
  const todayKey = localDate(now);
  const tomorrowKey = localDate(new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1));
  const todayCount = window.byDay.get(todayKey) ?? 0;
  const tomorrowCount = window.byDay.get(tomorrowKey) ?? 0;
  const busiest = [...days].sort((a, b) => b[1] - a[1]).slice(0, 8);

  const statuses = new Map();
  for (const row of window.rows) statuses.set(row.status, (statuses.get(row.status) ?? 0) + 1);

  console.log(`  Toplam görünür kayıt: ${window.rows.length} (${days.length} gün)`);
  console.log(`  Bugün (${todayKey}): ${todayCount} · Yarın: ${tomorrowCount}`);
  console.log(`  En yoğun günler: ${busiest.map(([day, n]) => `${day}=${n}`).join(', ')}`);
  console.log(`  Durumlar: ${[...statuses.entries()].map(([s, n]) => `${s}=${n}`).join(', ')}`);
  console.log(`  Oluşturulan: ${result.created} · sonuç yazılan: ${result.outcomes} · silinen: ${result.deleted}`);

  await writeFile(STATE_PATH, JSON.stringify({
    baseUrl: BASE_URL,
    seedVersion: SEED_VERSION,
    seededAt: new Date().toISOString(),
    count: TOTAL,
    created: result.created,
    series,
    adminUser: adminUser?.username ?? ADMIN_USER,
  }, null, 2));

  console.log(`\nDurum dosyası: ${STATE_PATH}`);
  console.log('Masaüstü için okuma modunu açın: Ayarlar → Sunucu bağlantısını yönet → Okuma modu (P2).');
}

main().catch((error) => {
  console.error('\nHata:', error.message);
  if (error instanceof HttpError) {
    console.error(error.body);
  }
  process.exit(1);
});
