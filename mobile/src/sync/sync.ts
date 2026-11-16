import { api } from '../api/client';
import type {
  AppointmentInput,
  AppointmentListItem,
  AppointmentOutcomeInput,
} from '../api/types';
import { getDay, upsertDay } from '../db/appointments';
import { setSyncState } from '../db/database';
import { listPending, markDone, markFailed, pendingCount } from '../db/outbox';

export type PullResult = {
  items: AppointmentListItem[];
  error: string | null;
  pending: number;
};

/** Günü yerelden okur; bağlantı varsa sunucudan tazeler. */
export async function pullDay(dateKey: string, online: boolean): Promise<PullResult> {
  let items = await getDay(dateKey);
  let error: string | null = null;

  if (online) {
    try {
      items = await api.getDay(dateKey);
      await upsertDay(dateKey, items);
      await setSyncState('last_day', dateKey);
      await setSyncState('last_sync_at', new Date().toISOString());
    } catch (err) {
      error = err instanceof Error ? err.message : 'Sunucuya ulaşılamadı.';
    }
  }

  return { items, error, pending: await pendingCount() };
}

export type FlushResult = {
  sent: number;
  failed: number;
  lastError: string | null;
};

/** Bekleyen yazma işlemlerini sırayla gönderir. */
export async function flushOutbox(): Promise<FlushResult> {
  const pending = await listPending();
  let sent = 0;
  let failed = 0;
  let lastError: string | null = null;

  for (const item of pending) {
    try {
      const payload = JSON.parse(item.payload) as unknown;

      switch (item.operation) {
        case 'appointment.create': {
          const result = await api.createAppointment(payload as AppointmentInput);
          if (!result.success) throw new Error(result.error ?? 'Görüşme oluşturulamadı.');
          break;
        }

        case 'appointment.update': {
          const body = payload as { id: string; input: AppointmentInput };
          await api.updateAppointment(body.id, body.input);
          break;
        }

        case 'appointment.outcome': {
          const body = payload as { id: string; input: AppointmentOutcomeInput };
          await api.saveOutcome(body.id, body.input);
          break;
        }

        case 'series.cancel': {
          const body = payload as { seriesId: string };
          const result = await api.cancelSeries(body.seriesId);
          if (!result.success) throw new Error(result.error ?? 'Seri iptal edilemedi.');
          break;
        }

        default:
          throw new Error(`Bilinmeyen işlem: ${item.operation}`);
      }

      await markDone(item.id);
      sent++;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'İşlem gönderilemedi.';
      lastError = message;
      failed++;
      await markFailed(item.id, message, item.attempts);
      // Ağ hatasında kuyruğun geri kalanını zorlamamak için dur.
      if (message.includes('ulaşılamadı') || message.includes('zaman aşımı')) break;
    }
  }

  return { sent, failed, lastError };
}
