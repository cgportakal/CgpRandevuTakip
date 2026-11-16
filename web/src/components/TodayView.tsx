'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { api, ApiError } from '@/lib/api';
import { dayCacheKey, readCache, writeCache } from '@/lib/cache';
import { shiftDateKey, todayKey } from '@/lib/dates';
import type { AppointmentDetail, AppointmentListItem, ContentManifest } from '@/lib/types';
import { AppointmentForm } from '@/components/AppointmentForm';
import { DetailView } from '@/components/DetailView';

type Props = {
  manifest: ContentManifest | null;
  online: boolean;
};

type ModalState =
  | { kind: 'none' }
  | { kind: 'new' }
  | { kind: 'detail'; id: string }
  | { kind: 'edit'; detail: AppointmentDetail };

export function TodayView({ manifest, online }: Props) {
  const [dateKey, setDateKey] = useState(todayKey());
  const [items, setItems] = useState<AppointmentListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [syncedAt, setSyncedAt] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [searching, setSearching] = useState(false);
  const [modal, setModal] = useState<ModalState>({ kind: 'none' });

  const load = useCallback(async (key: string) => {
    const cached = readCache<AppointmentListItem[]>(dayCacheKey(key));
    if (cached) {
      setItems(cached.value);
      setSyncedAt(cached.updatedAt);
    }

    if (!online) {
      setError(null);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const day = await api.getDay(key);
      setItems(day);
      setSyncedAt(new Date().toISOString());
      writeCache(dayCacheKey(key), day);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Liste yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, [online]);

  useEffect(() => {
    if (search.trim()) return;
    void load(dateKey);
  }, [dateKey, load, search]);

  useEffect(() => {
    const text = search.trim();
    if (!text) return;

    const timer = window.setTimeout(() => {
      void (async () => {
        setSearching(true);
        setError(null);

        try {
          setItems(await api.search(text));
          setSyncedAt(new Date().toISOString());
        } catch (err) {
          setError(err instanceof Error ? err.message : 'Arama başarısız.');
        } finally {
          setSearching(false);
        }
      })();
    }, 350);

    return () => window.clearTimeout(timer);
  }, [search]);

  const dateLabel = useMemo(() => {
    const [year, month, day] = dateKey.split('-').map(Number);
    const date = new Date(year, month - 1, day);
    return date.toLocaleDateString('tr-TR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      weekday: 'long',
    });
  }, [dateKey]);

  const refresh = () => {
    void load(dateKey);
  };

  return (
    <>
      <div className="card">
        <div className="toolbar">
          <button className="icon-btn" onClick={() => setDateKey(shiftDateKey(dateKey, -1))} disabled={Boolean(search.trim())}>
            ‹
          </button>
          <div>
            <div className="date-title">{dateLabel}</div>
            <div className="muted">
              {items.length} görüşme
              {syncedAt ? ` · ${new Date(syncedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}` : ''}
            </div>
          </div>
          <button className="icon-btn" onClick={() => setDateKey(shiftDateKey(dateKey, 1))} disabled={Boolean(search.trim())}>
            ›
          </button>
          <button className="btn btn-sm" onClick={() => setDateKey(todayKey())} disabled={Boolean(search.trim())}>
            Bugün
          </button>

          <div className="spacer" />

          <input
            className="input search"
            placeholder="Kişi, telefon, kategori veya görüşmeci ara"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />

          <button className="btn btn-sm" onClick={refresh} disabled={!online || loading}>
            Yenile
          </button>

          <button
            className="btn btn-primary"
            onClick={() => setModal({ kind: 'new' })}
            disabled={!manifest}
          >
            Yeni görüşme
          </button>
        </div>

        {!online ? (
          <div className="alert warn">Çevrimdışısınız; son kaydedilen liste gösteriliyor.</div>
        ) : null}

        {error ? <div className="alert error">{error}</div> : null}
      </div>

      <div className="card">
        {loading || searching ? (
          <div className="empty">Yükleniyor…</div>
        ) : items.length === 0 ? (
          <div className="empty">
            <strong>Kayıt yok</strong>
            {search.trim() ? 'Aramanızla eşleşen görüşme bulunamadı.' : 'Bu gün için görüşme yok.'}
          </div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: 90 }}>Saat</th>
                <th>Kişi</th>
                <th>Kategori</th>
                <th>Görüşmeci</th>
                <th>Yöntem</th>
                <th>Durum</th>
                <th style={{ width: 70 }} />
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <div className="time-pill" style={{ backgroundColor: item.categoryColorHex }}>
                      {item.timeLabel}
                      <small>{item.endTimeLabel}</small>
                    </div>
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{item.personFullName}</div>
                    <div className="muted">{item.phoneE164}</div>
                    {item.studentFullName ? <div className="muted">Öğrenci: {item.studentFullName}</div> : null}
                  </td>
                  <td>{item.categoryName}</td>
                  <td>{item.hostName}</td>
                  <td>{item.methodText}</td>
                  <td>
                    <span className={`badge ${statusClass(item.status)}`}>{item.statusText}</span>
                  </td>
                  <td>
                    <button className="btn btn-sm" onClick={() => setModal({ kind: 'detail', id: item.id })}>
                      Aç
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal.kind === 'new' ? (
        <AppointmentForm
          manifest={manifest}
          defaultDate={dateKey}
          onClose={() => setModal({ kind: 'none' })}
          onSaved={() => {
            setModal({ kind: 'none' });
            setSearch('');
            refresh();
          }}
        />
      ) : null}

      {modal.kind === 'edit' ? (
        <AppointmentForm
          manifest={manifest}
          defaultDate={dateKey}
          initial={modal.detail}
          onClose={() => setModal({ kind: 'detail', id: modal.detail.id })}
          onSaved={() => {
            setModal({ kind: 'none' });
            setSearch('');
            refresh();
          }}
        />
      ) : null}

      {modal.kind === 'detail' ? (
        <DetailView
          id={modal.id}
          online={online}
          onClose={() => setModal({ kind: 'none' })}
          onChanged={() => {
            setModal({ kind: 'none' });
            refresh();
          }}
          onEdit={(detail) => setModal({ kind: 'edit', detail })}
        />
      ) : null}
    </>
  );
}

function statusClass(status: AppointmentListItem['status']): string {
  switch (status) {
    case 'Completed':
      return 'live';
    case 'NoShow':
    case 'Cancelled':
      return 'danger';
    case 'Postponed':
      return 'warn';
    default:
      return '';
  }
}
