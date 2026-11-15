import * as SecureStore from 'expo-secure-store';
import type { User } from '../api/types';

const SESSION_KEY = 'cgp.session.v1';

export type StoredSession = {
  accessToken: string;
  refreshToken: string;
  user: User;
};

export async function saveSession(session: StoredSession): Promise<void> {
  await SecureStore.setItemAsync(SESSION_KEY, JSON.stringify(session));
}

export async function loadSession(): Promise<StoredSession | null> {
  try {
    const raw = await SecureStore.getItemAsync(SESSION_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}

export async function clearSession(): Promise<void> {
  await SecureStore.deleteItemAsync(SESSION_KEY);
}
