import type { AppointmentListItem } from './types';

const PREFIX = 'cgp.web.cache.';

export type CacheEntry<T> = { value: T; updatedAt: string };

export function writeCache<T>(key: string, value: T): void {
  if (typeof window === 'undefined') return;

  try {
    const entry: CacheEntry<T> = { value, updatedAt: new Date().toISOString() };
    window.localStorage.setItem(PREFIX + key, JSON.stringify(entry));
  } catch {
    // Depolama doluysa önbellek atlanır.
  }
}

export function readCache<T>(key: string): CacheEntry<T> | null {
  if (typeof window === 'undefined') return null;

  try {
    const raw = window.localStorage.getItem(PREFIX + key);
    return raw ? (JSON.parse(raw) as CacheEntry<T>) : null;
  } catch {
    return null;
  }
}

export const dayCacheKey = (dateKey: string) => `day:${dateKey}`;
export const MANIFEST_CACHE_KEY = 'manifest';

export type CachedDay = AppointmentListItem[];
