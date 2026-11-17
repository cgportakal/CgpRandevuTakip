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
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let reconnectAttempts = 0;

export function subscribeRealtime(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function emit(event: RealtimeEvent): void {
  for (const listener of listeners) listener(event);
}

/**
 * Sunucu kısa süre kapalı kaldığında SignalR'ın varsayılan denemeleri tükeniyor;
 * bağlantı bir daha kurulmuyordu. Bağlantı kapanınca üstel geri çekilmeyle
 * (en fazla 30 sn) süresiz yeniden denenir.
 */
function scheduleReconnect(hub: HubConnection): void {
  if (connection !== hub || reconnectTimer !== null) return;

  const delay = Math.min(30000, 2000 * 2 ** Math.min(reconnectAttempts, 4));
  reconnectAttempts += 1;

  reconnectTimer = setTimeout(async () => {
    reconnectTimer = null;
    if (connection !== hub || !api.getAccessToken()) return;

    try {
      await hub.start();
      reconnectAttempts = 0;
    } catch {
      scheduleReconnect(hub);
    }
  }, delay);
}

export async function startRealtime(): Promise<void> {
  if (!api.getAccessToken()) return;

  if (connection) {
    const state = connection.state;
    if (
      state === HubConnectionState.Connected ||
      state === HubConnectionState.Connecting ||
      state === HubConnectionState.Reconnecting
    ) {
      return;
    }

    try {
      await connection.start();
      reconnectAttempts = 0;
    } catch {
      scheduleReconnect(connection);
    }
    return;
  }

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
  hub.onreconnected(() => {
    reconnectAttempts = 0;
  });
  hub.onclose(() => scheduleReconnect(hub));

  connection = hub;

  try {
    await hub.start();
    reconnectAttempts = 0;
  } catch {
    // Sunucuya ulaşılamazsa uygulama çevrimdışı akışla devam eder.
    scheduleReconnect(hub);
  }
}

export async function stopRealtime(): Promise<void> {
  const hub = connection;
  connection = null;
  reconnectAttempts = 0;

  if (reconnectTimer !== null) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }

  if (!hub) return;

  try {
    await hub.stop();
  } catch {
    // Bağlantı zaten kapalıysa yoksayılır.
  }
}
