import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { API_URL } from './api';

export type RealtimeStatus = 'connecting' | 'connected' | 'disconnected';

export type AppointmentEvent = {
  appointmentId: string;
  change: string;
  dateKey: string;
  startsAtLocal: string;
};

export type SeriesEvent = {
  seriesId: string;
  change: string;
};

export type ContentEvent = {
  version: string;
  action: string;
  atUtc: string;
};

export type ReminderEvent = {
  reminderId: string;
  appointmentId: string;
  kind: string;
  title: string;
  body: string;
  startsAtLocal: string;
  timeLabel: string;
  silent: boolean;
};

/** Bileşenler bu hedefe abone olur; bağlantı yaşam döngüsü sayfada yönetilir. */
export const realtimeEvents = new EventTarget();

let connection: HubConnection | null = null;
let currentToken: string | null = null;

export function setRealtimeToken(token: string | null): void {
  currentToken = token;
}

export function getRealtimeConnection(): HubConnection | null {
  return connection;
}

export async function startRealtime(
  onStatus: (status: RealtimeStatus) => void,
): Promise<void> {
  if (!currentToken) return;
  if (connection && connection.state !== HubConnectionState.Disconnected) return;

  const hub = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/events`, {
      accessTokenFactory: () => currentToken ?? '',
      // JWT başlığı kullanıyoruz; tanımlama bilgisi (cookie) gönderilmez.
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .configureLogging(LogLevel.Warning)
    .build();

  hub.on('appointmentChanged', (payload: AppointmentEvent) => {
    realtimeEvents.dispatchEvent(new CustomEvent('appointmentChanged', { detail: payload }));
  });
  hub.on('seriesChanged', (payload: SeriesEvent) => {
    realtimeEvents.dispatchEvent(new CustomEvent('seriesChanged', { detail: payload }));
  });
  hub.on('contentChanged', (payload: ContentEvent) => {
    realtimeEvents.dispatchEvent(new CustomEvent('contentChanged', { detail: payload }));
  });
  hub.on('reminderDue', (payload: ReminderEvent) => {
    realtimeEvents.dispatchEvent(new CustomEvent('reminderDue', { detail: payload }));
  });

  hub.onreconnecting(() => onStatus('connecting'));
  hub.onreconnected(() => onStatus('connected'));
  hub.onclose(() => onStatus('disconnected'));

  connection = hub;
  onStatus('connecting');

  try {
    await hub.start();
    onStatus('connected');
  } catch {
    onStatus('disconnected');
  }
}

export async function stopRealtime(): Promise<void> {
  const hub = connection;
  connection = null;

  if (!hub) return;

  try {
    await hub.stop();
  } catch {
    // Bağlantı zaten kapanmışsa yoksayılır.
  }
}
