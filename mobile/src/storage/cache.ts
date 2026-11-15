import * as SQLite from 'expo-sqlite';

/**
 * Çevrimdışı okuma önbelleği. Sunucudan gelen gün listesi ve içerik manifesti
 * burada tutulur; ağ yokken son bilinen veri gösterilir.
 * Yazma kuyruğu (outbox) Faz 2'de aynı veritabanına eklenecek.
 */
let databasePromise: Promise<SQLite.SQLiteDatabase> | null = null;

async function getDatabase(): Promise<SQLite.SQLiteDatabase> {
  if (!databasePromise) {
    databasePromise = (async () => {
      const db = await SQLite.openDatabaseAsync('cgp.db');
      await db.execAsync(`
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS cache (
          key TEXT PRIMARY KEY NOT NULL,
          payload TEXT NOT NULL,
          updated_at TEXT NOT NULL
        );
      `);
      return db;
    })();
  }

  return databasePromise;
}

export type CacheEntry<T> = {
  value: T;
  updatedAt: string;
};

export async function writeCache<T>(key: string, value: T): Promise<void> {
  const db = await getDatabase();
  await db.runAsync(
    `INSERT INTO cache (key, payload, updated_at) VALUES (?, ?, ?)
     ON CONFLICT(key) DO UPDATE SET payload = excluded.payload, updated_at = excluded.updated_at`,
    key,
    JSON.stringify(value),
    new Date().toISOString(),
  );
}

export async function readCache<T>(key: string): Promise<CacheEntry<T> | null> {
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ payload: string; updated_at: string }>(
    'SELECT payload, updated_at FROM cache WHERE key = ?',
    key,
  );

  if (!row) return null;

  try {
    return { value: JSON.parse(row.payload) as T, updatedAt: row.updated_at };
  } catch {
    return null;
  }
}

export const dayCacheKey = (dateKey: string) => `day:${dateKey}`;
export const MANIFEST_CACHE_KEY = 'content:manifest';
