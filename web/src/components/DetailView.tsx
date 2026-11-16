'use client';

import { useEffect, useState } from 'react';
import { api, ApiError, type AppointmentOutcome } from '@/lib/api';
import type { AppointmentDetail, AppointmentOutcomeInput } from '@/lib/types';

type Props = {
  id: string;
  online: boolean;
  onClose: () => void;
  onChanged: () => void;
  onEdit: (detail: AppointmentDetail) => void;
};

const attendanceOptions: { value: AppointmentOutcomeInput['attendance']; label: string }[] = [
  { value: 'Arrived', label: 'Geldi' },
  { value: 'NoShow', label: 'Gelmedi' },
  { value: 'Postponed', label: 'Ertelendi' },
  { value: 'Cancelled', label: 'İptal' },
];

export function DetailView({ id, online, onClose, onChanged, onEdit }: Props) {
  const [detail, setDetail] = useState<AppointmentDetail | null>(null);
  const [outcome, setOutcome] = useState<AppointmentOutcome | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [attendance, setAttendance] = useState<AppointmentOutcomeInput['attendance']>('Arrived');
  const [summary, setSummary] = useState('');
  const [willRepeat, setWillRepeat] = useState(false);
  const [postNotes, setPostNotes] = useState('');
  const [isClosed, setIsClosed] = useState(true);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setError(null);

      try {
        const [appointment, existing] = await Promise.all([api.getDetail(id), api.getOutcome(id)]);
        setDetail(appointment);
        setOutcome(existing);

        if (existing) {
          setAttendance(existing.attendance);
          setSummary(existing.summary ?? '');
          setWillRepeat(existing.willRepeat);
          setPostNotes(existing.postNotes ?? '');
          setIsClosed(existing.isClosed);
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Detay yüklenemedi.');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const saveOutcome = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!detail) return;

    setBusy(true);
    setError(null);

    try {
      await api.saveOutcome(detail.id, {
        attendance,
        summary: summary.trim() || null,
        willRepeat,
        postNotes: postNotes.trim() || null,
        isClosed,
      });
      onChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sonuç kaydedilemedi.');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!detail) return;
    if (!window.confirm(`${detail.personFullName} görüşmesi silinsin mi? 30 gün içinde geri alınabilir.`)) return;

    setBusy(true);
    try {
      await api.deleteAppointment(detail.id);
      onChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Silinemedi.');
    } finally {
      setBusy(false);
    }
  };

  const cancelSeries = async () => {
    if (!detail?.seriesId) return;
    if (!window.confirm('Bu serinin gelecek planlanmış görüşmeleri iptal edilsin mi?')) return;

    setBusy(true);
    try {
      const result = await api.cancelSeries(detail.seriesId);
      window.alert(result.summary);
      onChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Seri iptal edilemedi.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className="modal">
        <div className="modal-header">
          <h2>Görüşme detayı</h2>
          <div className="spacer" />
          <button type="button" className="btn btn-sm btn-ghost" onClick={onClose}>
            Kapat
          </button>
        </div>

        {loading ? (
          <div className="empty">Yükleniyor…</div>
        ) : !detail ? (
          <div className="alert error">{error ?? 'Kayıt bulunamadı.'}</div>
        ) : (
          <>
            <div className="card" style={{ boxShadow: 'none' }}>
              <div className="card-header">
                <span className="badge" style={{ background: detail.categoryColorHex, color: '#fff' }}>
                  {detail.categoryName}
                </span>
                <span className={`badge ${detail.isClosed ? 'live' : ''}`}>{detail.statusText}</span>
                {detail.seriesId ? (
                  <span className="badge warn">
                    Seri {detail.seriesIndex}/{detail.seriesCount}
                  </span>
                ) : null}
                <div className="spacer" />
                <button className="btn btn-sm" onClick={() => onEdit(detail)} disabled={!online}>
                  Düzenle
                </button>
                {detail.seriesId ? (
                  <button className="btn btn-sm btn-danger" onClick={() => void cancelSeries()} disabled={!online || busy}>
                    Seriyi iptal et
                  </button>
                ) : null}
                <button className="btn btn-sm btn-danger" onClick={() => void remove()} disabled={!online || busy}>
                  Sil
                </button>
              </div>

              <dl className="detail-grid">
                <div>
                  <dt>Kişi</dt>
                  <dd>{detail.personFullName}</dd>
                </div>
                <div>
                  <dt>Telefon</dt>
                  <dd>{detail.phoneE164}</dd>
                </div>
                <div>
                  <dt>Tarih</dt>
                  <dd>{detail.dateLabel}</dd>
                </div>
                <div>
                  <dt>Saat</dt>
                  <dd>{detail.timeRangeLabel}</dd>
                </div>
                <div>
                  <dt>Görüşmeci</dt>
                  <dd>{detail.hostName}</dd>
                </div>
                <div>
                  <dt>Yöntem</dt>
                  <dd>{detail.method === 'FaceToFace' ? 'Yüz yüze' : detail.method === 'Phone' ? 'Telefon' : detail.method === 'WhatsApp' ? 'WhatsApp' : 'İnternet'}</dd>
                </div>
                <div>
                  <dt>Öğrenci</dt>
                  <dd>{detail.studentFullName || '—'}</dd>
                </div>
                <div>
                  <dt>Hatırlatıcılar</dt>
                  <dd>{detail.remindersText || '—'}</dd>
                </div>
                <div style={{ gridColumn: '1 / -1' }}>
                  <dt>Açıklama</dt>
                  <dd>{detail.description || '—'}</dd>
                </div>
              </dl>
            </div>

            <form className="card" style={{ boxShadow: 'none' }} onSubmit={saveOutcome}>
              <div className="card-header">
                <h2>Görüşme sonucu</h2>
                {outcome ? <span className="muted">Son güncelleme: {outcome.updatedAtLocal}</span> : null}
              </div>

              <div className="grid-2">
                <div className="field">
                  <label>Katılım</label>
                  <select
                    className="select"
                    value={attendance}
                    onChange={(event) => setAttendance(event.target.value as AppointmentOutcomeInput['attendance'])}
                  >
                    {attendanceOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Durum</label>
                  <div className="checkbox-row" style={{ paddingTop: 10 }}>
                    <label>
                      <input type="checkbox" checked={isClosed} onChange={(event) => setIsClosed(event.target.checked)} />
                      Görüşme kapandı
                    </label>
                    <label>
                      <input type="checkbox" checked={willRepeat} onChange={(event) => setWillRepeat(event.target.checked)} />
                      Tekrar görüşmek ister
                    </label>
                  </div>
                </div>
              </div>

              <div className="field" style={{ marginTop: 12 }}>
                <label>Özet</label>
                <textarea className="textarea" rows={3} value={summary} onChange={(event) => setSummary(event.target.value)} />
              </div>

              <div className="field" style={{ marginTop: 12 }}>
                <label>Sonraki adımlar / notlar</label>
                <textarea className="textarea" rows={2} value={postNotes} onChange={(event) => setPostNotes(event.target.value)} />
              </div>

              {error ? <div className="alert error">{error}</div> : null}

              <div className="modal-footer" style={{ borderTop: 'none' }}>
                <button type="submit" className="btn btn-primary" disabled={!online || busy}>
                  {busy ? 'Kaydediliyor…' : 'Sonucu kaydet'}
                </button>
              </div>
            </form>
          </>
        )}
      </div>
    </div>
  );
}
