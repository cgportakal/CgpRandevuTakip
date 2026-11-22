import type { AppointmentListItem } from '../api/types';
import { getDatabase } from './database';

/** Sunucudan gelen günlük listeyi yerel tabloya yazar (tam senkron). */
export async function upsertDay(dateKey: string, items: AppointmentListItem[]): Promise<void> {
  const db = await getDatabase();
  const now = new Date().toISOString();

  await db.withTransactionAsync(async () => {
    await db.runAsync(
      `DELETE FROM appointments WHERE substr(starts_at_local, 1, 10) = ?`,
      dateKey,
    );

    for (const item of items) {
      await db.runAsync(
        `INSERT INTO appointments (
           id, starts_at_local, duration_minutes, person_full_name, phone_e164,
           host_user_id, host_name, room_id, room_name, guest_count, host_count,
           category_id, category_name, category_color_hex,
           method, status, payload, synced_at
         ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
         ON CONFLICT(id) DO UPDATE SET
           starts_at_local = excluded.starts_at_local,
           duration_minutes = excluded.duration_minutes,
           person_full_name = excluded.person_full_name,
           phone_e164 = excluded.phone_e164,
           host_user_id = excluded.host_user_id,
           host_name = excluded.host_name,
           room_id = excluded.room_id,
           room_name = excluded.room_name,
           guest_count = excluded.guest_count,
           host_count = excluded.host_count,
           category_id = excluded.category_id,
           category_name = excluded.category_name,
           category_color_hex = excluded.category_color_hex,
           method = excluded.method,
           status = excluded.status,
           payload = excluded.payload,
           synced_at = excluded.synced_at`,
        item.id,
        item.startsAtLocal,
        item.durationMinutes,
        item.personFullName,
        item.phoneE164,
        item.hostUserId,
        item.hostName,
        item.roomId,
        item.roomName,
        item.guestCount,
        item.participantCount,
        item.categoryId,
        item.categoryName,
        item.categoryColorHex,
        item.method,
        item.status,
        JSON.stringify(item),
        now,
      );
    }
  });
}

/** Yerel önbellekten gün listesini okur. */
export async function getDay(dateKey: string): Promise<AppointmentListItem[]> {
  const db = await getDatabase();
  const rows = await db.getAllAsync<{ payload: string }>(
    `SELECT payload FROM appointments
     WHERE substr(starts_at_local, 1, 10) = ?
     ORDER BY starts_at_local`,
    dateKey,
  );

  return rows
    .map((row) => {
      try {
        return JSON.parse(row.payload) as AppointmentListItem;
      } catch {
        return null;
      }
    })
    .filter((item): item is AppointmentListItem => item !== null);
}

export async function countCachedAppointments(): Promise<number> {
  const db = await getDatabase();
  const row = await db.getFirstAsync<{ count: number }>(
    'SELECT COUNT(*) AS count FROM appointments',
  );
  return row?.count ?? 0;
}

export async function clearAppointments(): Promise<void> {
  const db = await getDatabase();
  await db.runAsync('DELETE FROM appointments');
}
