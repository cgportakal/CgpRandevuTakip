import { Platform } from 'react-native';

/**
 * Sunucu adresi. Fiziksel cihazda .env içinde EXPO_PUBLIC_API_URL verin:
 *   EXPO_PUBLIC_API_URL=http://192.168.1.20:5199
 * Android emülatörü ana makineye 10.0.2.2 üzerinden erişir.
 */
export const API_URL =
  process.env.EXPO_PUBLIC_API_URL ??
  (Platform.OS === 'android' ? 'http://10.0.2.2:5199' : 'http://127.0.0.1:5199');

export const APP_VERSION = '1.0.0';
