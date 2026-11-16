import { api } from './api';

export type PushPermission = 'unsupported' | 'default' | 'granted' | 'denied';

export type PushResult = { ok: boolean; message: string };

function urlBase64ToUint8Array(base64String: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = window.atob(base64);
  const buffer = new ArrayBuffer(raw.length);
  const output = new Uint8Array(buffer);

  for (let i = 0; i < raw.length; i += 1) output[i] = raw.charCodeAt(i);

  return output;
}

export function getPushPermission(): PushPermission {
  if (typeof window === 'undefined') return 'unsupported';
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return 'unsupported';
  if (typeof Notification === 'undefined') return 'unsupported';

  return Notification.permission as PushPermission;
}

export async function registerWebPush(): Promise<PushResult> {
  if (getPushPermission() === 'unsupported') {
    return { ok: false, message: 'Bu tarayıcı web bildirimlerini desteklemiyor.' };
  }

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    return { ok: false, message: 'Bildirim izni verilmedi.' };
  }

  try {
    const registration = await navigator.serviceWorker.register('/sw.js');
    await navigator.serviceWorker.ready;

    const { publicKey } = await api.getPushPublicKey();

    const existing = await registration.pushManager.getSubscription();
    const subscription =
      existing ??
      (await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      }));

    const json = subscription.toJSON();
    const keys = json.keys ?? {};

    if (!json.endpoint || !keys.p256dh || !keys.auth) {
      return { ok: false, message: 'Push aboneliği oluşturulamadı.' };
    }

    await api.registerDevice({
      platform: 'web',
      endpoint: json.endpoint,
      p256dh: keys.p256dh,
      auth: keys.auth,
      appVersion: 'web',
    });

    return { ok: true, message: 'Bu tarayıcıda bildirimler açıldı.' };
  } catch (error) {
    return {
      ok: false,
      message: error instanceof Error ? error.message : 'Bildirimler açılamadı.',
    };
  }
}

export async function unregisterWebPush(): Promise<void> {
  if (getPushPermission() === 'unsupported') return;

  try {
    const registration = await navigator.serviceWorker.getRegistration('/');
    const subscription = await registration?.pushManager.getSubscription();
    if (!subscription) return;

    await api.unregisterDevice(subscription.endpoint);
    await subscription.unsubscribe();
  } catch {
    // Çıkış akışını engellemez.
  }
}

export async function sendTestPush(): Promise<PushResult> {
  try {
    const result = await api.sendTestPush();
    return result.sent > 0
      ? { ok: true, message: `${result.sent} cihaza test bildirimi gönderildi.` }
      : { ok: false, message: 'Kayıtlı push cihazı bulunamadı.' };
  } catch (error) {
    return {
      ok: false,
      message: error instanceof Error ? error.message : 'Test bildirimi gönderilemedi.',
    };
  }
}
