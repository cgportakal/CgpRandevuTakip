import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import { api } from './api/client';
import { APP_VERSION } from './config';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

/**
 * Expo push belirteci alır ve sunucuya kaydeder.
 * Uzak bildirimler Expo Go'da desteklenmez; EAS geliştirme derlemesi veya mağaza
 * derlemesi gerekir. Desteklenmeyen ortamda sessizce null döner.
 */
export async function registerForPushAsync(): Promise<string | null> {
  try {
    if (!Device.isDevice) return null;

    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('default', {
        name: 'CGP - Randevu Takip',
        importance: Notifications.AndroidImportance.MAX,
        vibrationPattern: [0, 250, 250, 250],
      });
    }

    const existing = await Notifications.getPermissionsAsync();
    let status = existing.status;

    if (status !== 'granted') {
      status = (await Notifications.requestPermissionsAsync()).status;
    }

    if (status !== 'granted') return null;

    const projectId =
      process.env.EXPO_PUBLIC_EAS_PROJECT_ID ??
      Constants.expoConfig?.extra?.eas?.projectId ??
      (Constants as unknown as { easConfig?: { projectId?: string } }).easConfig?.projectId;

    if (!projectId) {
      console.warn(
        'Push kaydı atlandı: EAS projectId yok. app.json extra.eas.projectId veya EXPO_PUBLIC_EAS_PROJECT_ID ayarlayın.',
      );
      return null;
    }

    const token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;

    await api.registerDevice({
      platform: Platform.OS === 'android' ? 'android' : 'ios',
      token,
      appVersion: APP_VERSION,
    });

    return token;
  } catch {
    return null;
  }
}

export async function unregisterPushAsync(token: string | null): Promise<void> {
  if (!token) return;

  try {
    await api.unregisterDeviceToken(token);
  } catch {
    // Çıkış akışını engellemez.
  }
}
