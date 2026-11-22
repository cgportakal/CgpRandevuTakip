import * as SQLite from 'expo-sqlite';

/**
 * Mobil yerel veritabanı. Şema sürümü `meta` tablosunda tutulur; yeni sürümlerde
 * `SCHEMA_VERSION` artırılıp migration adımları eklenir.
 */
export const SCHEMA_VERSION = 3;

let databasePromise: Promise<SQLite.SQLiteDatabase> | null = null;

export async function getDatabase(): Promise<SQLite.SQLiteDatabase> {
  if (!databasePromise) {
    databasePromise = (async () => {
      const db = await SQLite.openDatabaseAsync('cgp.db');
      await migrate(db);
      return db;
    })();
  }

  return databasePromise;
}

async function migrate(db: SQLite.SQLiteDatabase): Promise<void> {
  await db.execAsync(`
    PRAGMA journal_mode = WAL;

    CREATE TABLE IF NOT EXISTS meta (
      key TEXT PRIMARY KEY NOT NULL,
      value TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS cache (
      key TEXT PRIMARY KEY NOT NULL,
      payload TEXT NOT NULL,
      updated_at TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS appointments (
      id TEXT PRIMARY KEY NOT NULL,
      starts_at_local TEXT NOT NULL,
      duration_minutes INTEGER NOT NULL,
      person_full_name TEXT NOT NULL,
      phone_e164 TEXT NOT NULL,
      host_user_id TEXT NOT NULL,
      host_name TEXT NOT NULL,
      room_id TEXT,
      room_name TEXT,
      guest_count INTEGER NOT NULL DEFAULT 1,
      host_count INTEGER NOT NULL DEFAULT 1,
      category_id TEXT NOT NULL,
      category_name TEXT NOT NULL,
      category_color_hex TEXT NOT NULL,
      method TEXT NOT NULL,
      status TEXT NOT NULL,
      payload TEXT NOT NULL,
      synced_at TEXT NOT NULL
    );

    CREATE INDEX IF NOT EXISTS ix_appointments_starts ON appointments(starts_at_local);
    CREATE INDEX IF NOT EXISTS ix_appointments_host ON appointments(host_user_id, starts_at_local);
    CREATE INDEX IF NOT EXISTS ix_appointments_room ON appointments(room_id, starts_at_local);

    CREATE TABLE IF NOT EXISTS outbox (
      id TEXT PRIMARY KEY NOT NULL,
      operation TEXT NOT NULL,
      entity_id TEXT,
      payload TEXT NOT NULL,
      created_at TEXT NOT NULL,
      attempts INTEGER NOT NULL DEFAULT 0,
      last_error TEXT,
      state TEXT NOT NULL DEFAULT 'pending'
    );

    CREATE INDEX IF NOT EXISTS ix_outbox_state ON outbox(state, created_at);

    CREATE TABLE IF NOT EXISTS sync_state (
      key TEXT PRIMARY KEY NOT NULL,
      value TEXT NOT NULL,
      updated_at TEXT NOT NULL
    );
  `);

  // v2 -> v3: yeni sütunlar mevcut veritabanlarına eklenir.
  await ensureColumn(db, 'appointments', 'room_id', 'TEXT');
  await ensureColumn(db, 'appointments', 'room_name', 'TEXT');
  await ensureColumn(db, 'appointments', 'guest_count', 'INTEGER NOT NULL DEFAULT 1');
  await ensureColumn(db, 'appointments', 'host_count', 'INTEGER NOT NULL DEFAULT 1');

  await db.runAsync(
    `INSERT INTO meta (key, value) VALUES ('schema_version', ?)
     ON CONFLICT(key) DO UPDATE SET value = excluded.value`,
    String(SCHEMA_VERSION),
  );
}

async function ensureColumn(
  db: SQLite.SQLiteDatabase,
  table: string,
  column: string,
  definition: string,
): Promise<void> {
  const columns = await db.getAllAsync<{ name: string }>(`PRAGMA table_info(${table})`);
  if (columns.some((item) => item.name === column))
    return;

  await db.execAsync(`ALTER TABLE ${table} ADD COLUMN ${column} ${definition}`);
}

export async function getMeta(key: string): Promise<string | null> {
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ value: string }>(
    'SELECT value FROM meta WHERE key = ?',
    key,
  );
  return row?.value ?? null;
}

export async function setMeta(key: string, value: string): Promise<void> {
  const db = await getDatabase();
  await db.runAsync(
    `INSERT INTO meta (key, value) VALUES (?, ?)
     ON CONFLICT(key) DO UPDATE SET value = excluded.value`,
    key,
    value,
  );
}

export async function setSyncState(key: string, value: string): Promise<void> {
  const db = await getDatabase();
  await db.runAsync(
    `INSERT INTO sync_state (key, value, updated_at) VALUES (?, ?, ?)
     ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at`,
    key,
    value,
    new Date().toISOString(),
  );
}

export async function getSyncState(key: string): Promise<string | null> {
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ value: string }>(
    'SELECT value FROM sync_state WHERE key = ?',
    key,
  );
  return row?.value ?? null;
}
