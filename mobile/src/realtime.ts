import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { api } from './api/client';
import { API_URL } from './config';

export type RealtimeEvent =
  | {
      type: 'appointmentChanged';
      payload: { appointmentId: string; change: string; dateKey: string };
    }
  | { type: 'seriesChanged'; payload: { seriesId: string; change: string } }
  | {
      type: 'reminderDue';
      payload: { title: string; body: string; silent: boolean; reminderId: string };
    };

type Listener = (event: RealtimeEvent) => void;

const listeners = new Set<Listener>();
let connection: HubConnection | null = null;

export function subscribeRealtime(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function emit(event: RealtimeEvent): void {
  for (const listener of listeners) listener(event);
}

export async function startRealtime(): Promise<void> {
  if (connection && connection.state !== HubConnectionState.Disconnected) return;
  if (!api.getAccessToken()) return;

  const hub = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/events`, {
      accessTokenFactory: () => api.getAccessToken() ?? '',
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .configureLogging(LogLevel.Warning)
    .build();

  hub.on('appointmentChanged', (payload) => emit({ type: 'appointmentChanged', payload }));
  hub.on('seriesChanged', (payload) => emit({ type: 'seriesChanged', payload }));
  hub.on('reminderDue', (payload) => emit({ type: 'reminderDue', payload }));

  connection = hub;

  try {
    await hub.start();
  } catch {
    // Sunucuya ulaşılamazsa uygulama çevrimdışı akışla devam eder.
  }
}

export async function stopRealtime(): Promise<void> {
  const hub = connection;
  connection = null;

  if (!hub) return;

  try {
    await hub.stop();
  } catch {
    // Bağlantı zaten kapalıysa yoksayılır.
  }
}
