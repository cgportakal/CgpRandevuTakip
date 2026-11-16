'use client';

import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { MANIFEST_CACHE_KEY, readCache, writeCache } from '@/lib/cache';
import type { AuthTokens, ContentManifest } from '@/lib/types';
import { LoginView } from '@/components/LoginView';
import { TodayView } from '@/components/TodayView';
import { ReportsView } from '@/components/ReportsView';

type View = 'today' | 'reports';

export default function Page() {
  const [booting, setBooting] = useState(true);
  const [session, setSession] = useState<AuthTokens | null>(null);
  const [manifest, setManifest] = useState<ContentManifest | null>(null);
  const [view, setView] = useState<View>('today');
  const [online, setOnline] = useState(true);

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
    </div>
  );
}
