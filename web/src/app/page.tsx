'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { MANIFEST_CACHE_KEY, readCache, writeCache } from '@/lib/cache';
import {
  realtimeEvents,
  setRealtimeToken,
  startRealtime,
  stopRealtime,
  type RealtimeStatus,
  type ReminderEvent,
} from '@/lib/realtime';
import type { AuthTokens, ContentManifest } from '@/lib/types';
import { LoginView } from '@/components/LoginView';
import { TodayView } from '@/components/TodayView';
import { ReportsView } from '@/components/ReportsView';

type View = 'today' | 'reports';

type Toast = { id: string; title: string; body: string };

export default function Page() {
  const [booting, setBooting] = useState(true);
  const [session, setSession] = useState<AuthTokens | null>(null);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [view, setView] = useState<View>('today');
  const [online, setOnline] = useState(true);
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>('disconnected');
  const [toasts, setToasts] = useState<Toast[]>([]);

  useEffect(() => {
    const tokens = api.loadSession();
    if (tokens) setSession(tokens);

    const cached = readCache<ContentManifest>(MANIFEST_CACHE_KEY);
    if (cached) setManifest(cached.value);

    setOnline(typeof navigator === 'undefined' ? true : navigator.onLine);
    const update = () => setOnline(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    setBooting(false);

    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, []);

  useEffect(() => {
    if (!session) return;

    void (async () => {
      try {
        const content = await api.getManifest();
        setManifest(content);
        writeCache(MANIFEST_CACHE_KEY, content);
      } catch {
        // Önbellekteki içerik kullanılmaya devam eder.
      }
    })();
  }, [session]);

  useEffect(() => {
    if (!session) {
      setRealtimeStatus('disconnected');
      void stopRealtime();
      return;
    }

    setRealtimeToken(session.accessToken);
    void startRealtime(setRealtimeStatus);

    const onReminder = (event: Event) => {
      const payload = (event as CustomEvent<ReminderEvent>).detail;
      const toast: Toast = {
        id: crypto.randomUUID(),
        title: payload.title,
        body: payload.body,
      };

      setToasts((current) => [...current.slice(-2), toast]);
      window.setTimeout(() => {
        setToasts((current) => current.filter((item) => item.id !== toast.id));
      }, 10000);
    };

    const onContent = () => {
      void (async () => {
        try {
          const content = await api.getManifest();
          setManifest(content);
          writeCache(MANIFEST_CACHE_KEY, content);
        } catch {
          // Mevcut içerik kullanılmaya devam eder.
        }
      })();
    };

    realtimeEvents.addEventListener('reminderDue', onReminder);
    realtimeEvents.addEventListener('contentChanged', onContent);

    return () => {
      realtimeEvents.removeEventListener('reminderDue', onReminder);
      realtimeEvents.removeEventListener('contentChanged', onContent);
      void stopRealtime();
    };
  }, [session]);

  useEffect(() => {
    const handler = (event: Event) => {
      setRealtimeToken((event as CustomEvent<string | null>).detail);
    };

    window.addEventListener('cgp:tokens', handler);
    return () => window.removeEventListener('cgp:tokens', handler);
  }, []);

  const onLogin = (tokens: AuthTokens) => {
    setSession(tokens);
  };

  const onLogout = async () => {
    await api.logout();
    setSession(null);
  };

  if (booting) {
    return <div className="splash">Yükleniyor…</div>;
  }

  if (!session) {
    return <LoginView onLogin={onLogin} />;
  }

  return (
    <div className="app">
      <aside className="sidebar">
        <div>
          <div className="brand">CGP</div>
          <div className="brand-title">Randevu Takip</div>
          <div className="institution">{manifest?.institution.name ?? 'Kurum yükleniyor…'}</div>
        </div>

        <nav className="nav">
          <button
            className={`nav-item ${view === 'today' ? 'active' : ''}`}
            onClick={() => setView('today')}
          >
            Günlük Liste
          </button>
          <button
            className={`nav-item ${view === 'reports' ? 'active' : ''}`}
            onClick={() => setView('reports')}
          >
            Raporlar
          </button>
        </nav>

        <div className="sidebar-footer">
          <span>{session.user.displayName}</span>
          <span>{session.user.role === 'Admin' ? 'Yönetici' : session.user.role === 'Secretary' ? 'Sekreter' : 'Görüşmeci'}</span>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <h1>{view === 'today' ? 'Günlük Liste' : 'Raporlar'}</h1>
          <div className="spacer" />
          <div className="user-chip">
            <span className={`online-dot ${online ? '' : 'offline'}`} />
            {online ? 'Çevrimiçi' : 'Çevrimdışı'}
            <span
              className={`badge ${
                realtimeStatus === 'connected' ? 'live' : realtimeStatus === 'connecting' ? 'warn' : ''
              }`}
            >
              {realtimeStatus === 'connected'
                ? 'Canlı'
                : realtimeStatus === 'connecting'
                  ? 'Bağlanıyor'
                  : 'Canlı değil'}
            </span>
          </div>
          <button className="btn btn-sm" onClick={() => void onLogout()}>
            Çıkış
          </button>
        </header>

        <div className="content">
          {view === 'today' ? (
            <TodayView manifest={manifest} online={online} />
          ) : (
            <ReportsView manifest={manifest} online={online} />
          )}
        </div>
      </main>

      <div className="toast-stack">
        {toasts.map((toast) => (
          <button
            key={toast.id}
            className="toast"
            onClick={() => setToasts((current) => current.filter((item) => item.id !== toast.id))}
          >
            <strong>{toast.title}</strong>
            <span>{toast.body}</span>
          </button>
        ))}
      </div>
    </div>
  );
}
