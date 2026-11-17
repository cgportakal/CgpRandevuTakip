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
import {
  getPushPermission,
  registerWebPush,
  sendTestPush,
  unregisterWebPush,
  type PushPermission,
} from '@/lib/push';
import type { AuthTokens, ContentManifest } from '@/lib/types';
import { LoginView } from '@/components/LoginView';
import { TodayView } from '@/components/TodayView';
import { ReportsView } from '@/components/ReportsView';
import { ContentAdminView } from '@/components/ContentAdminView';

type View = 'today' | 'reports' | 'content';

type Toast = { id: string; title: string; body: string };

export default function Page() {
  const [booting, setBooting] = useState(true);
  const [session, setSession] = useState<AuthTokens | null>(null);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [view, setView] = useState<View>('today');
  const [online, setOnline] = useState(true);
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>('disconnected');
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [pushPermission, setPushPermission] = useState<PushPermission>('unsupported');

  useEffect(() => {
    const tokens = api.loadSession();
    if (tokens) setSession(tokens);

    const cached = readCache<ContentManifest>(MANIFEST_CACHE_KEY);
    if (cached) setManifest(cached.value);

    setOnline(typeof navigator === 'undefined' ? true : navigator.onLine);
    setPushPermission(getPushPermission());
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

  useEffect(() => {
    if (session && session.user.role !== 'Admin' && view === 'content') {
      setView('today');
    }
  }, [session, view]);

  const onLogin = (tokens: AuthTokens) => {
    setSession(tokens);
  };

  const onLogout = async () => {
    await unregisterWebPush();
    await api.logout();
    setSession(null);
  };

  const showToast = (title: string, body: string) => {
    const toast: Toast = { id: crypto.randomUUID(), title, body };
    setToasts((current) => [...current.slice(-2), toast]);
    window.setTimeout(() => {
      setToasts((current) => current.filter((item) => item.id !== toast.id));
    }, 9000);
  };

  const enablePush = async () => {
    const result = await registerWebPush();
    setPushPermission(getPushPermission());
    showToast('Bildirimler', result.message);
  };

  const testPush = async () => {
    const result = await sendTestPush();
    showToast('Test bildirimi', result.message);
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
          {session.user.role === 'Admin' ? (
            <button
              className={`nav-item ${view === 'content' ? 'active' : ''}`}
              onClick={() => setView('content')}
            >
              İçerik Yönetimi
            </button>
          ) : null}
        </nav>

        <div className="sidebar-footer">
          <span>{session.user.displayName}</span>
          <span>{session.user.role === 'Admin' ? 'Yönetici' : session.user.role === 'Secretary' ? 'Sekreter' : 'Görüşmeci'}</span>
        </div>
      </aside>

      <main className="main">
        <header className="topbar">
          <h1>
            {view === 'today' ? 'Günlük Liste' : view === 'reports' ? 'Raporlar' : 'İçerik Yönetimi'}
          </h1>
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

          {pushPermission === 'default' ? (
            <button className="btn btn-sm" onClick={() => void enablePush()}>
              Bildirimleri aç
            </button>
          ) : pushPermission === 'granted' ? (
            <button className="btn btn-sm" onClick={() => void testPush()}>
              Test bildirimi
            </button>
          ) : pushPermission === 'denied' ? (
            <span className="badge warn">Bildirim kapalı</span>
          ) : null}
          <button className="btn btn-sm" onClick={() => void onLogout()}>
            Çıkış
          </button>
        </header>

        <div className="content">
          {view === 'today' ? (
            <TodayView manifest={manifest} online={online} />
          ) : view === 'reports' ? (
            <ReportsView manifest={manifest} online={online} />
          ) : (
            <ContentAdminView online={online} />
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
