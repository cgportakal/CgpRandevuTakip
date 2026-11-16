import { getDatabase } from './database';

/**
 * Yazma kuyruğu. Çevrimdışıyken yapılan görüşme oluşturma/güncelleme, sonuç girme
 * ve seri iptali burada tutulur; bağlantı gelince sırayla gönderilir.
 */
export type OutboxOperation =
  | 'appointment.create'
  | 'appointment.update'
  | 'appointment.outcome'
  | 'series.cancel';

export type OutboxItem = {
  id: string;
  operation: OutboxOperation;
  entityId: string | null;
  payload: string;
  createdAt: string;
  attempts: number;
  lastError: string | null;
  state: 'pending' | 'failed';
};

export async function enqueue(
  operation: OutboxOperation,
  payload: unknown,
  entityId: string | null = null,
): Promise<string> {
  const db = await getDatabase();
  const id = `ob_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;

  await db.runAsync(
    `INSERT INTO outbox (id, operation, entity_id, payload, created_at, state)
     VALUES (?, ?, ?, ?, ?, 'pending')`,
    id,
    operation,
    entityId,
    JSON.stringify(payload),
    new Date().toISOString(),
  );

  return id;
}

export async function listPending(limit = 50): Promise<OutboxItem[]> {
  const db = await getDatabase();
  return db.getAllAsync<OutboxItem>(
    `SELECT id, operation, entity_id AS entityId, payload, created_at AS createdAt,
            attempts, last_error AS lastError, state
     FROM outbox
     WHERE state = 'pending'
     ORDER BY created_at
     LIMIT ?`,
    limit,
  );
}

export async function pendingCount(): Promise<number> {
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ count: number }>(
    `SELECT COUNT(*) AS count FROM outbox WHERE state = 'pending'`,
  );
  return row?.count ?? 0;
}

export async function markDone(id: string): Promise<void> {
  const db = await getDatabase();
  await db.runAsync('DELETE FROM outbox WHERE id = ?', id);
}

export async function markFailed(id: string, error: string, attempts: number): Promise<void> {
  const db = await getDatabase();
  await db.runAsync(
    `UPDATE outbox
     SET attempts = ?, last_error = ?, state = CASE WHEN ? >= 5 THEN 'failed' ELSE 'pending' END
     WHERE id = ?`,
    attempts + 1,
    error,
    attempts + 1,
    id,
  );
}

export async function retryFailed(): Promise<void> {
  const db = await getDatabase();
  await db.runAsync(`UPDATE outbox SET state = 'pending', attempts = 0 WHERE state = 'failed'`);
}
