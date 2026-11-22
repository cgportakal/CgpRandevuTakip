'use client';

import { useEffect, useState } from 'react';
import { api, ApiError, type AppointmentOutcome } from '@/lib/api';
import type {
  AppointmentDetail,
  AppointmentGuest,
  AppointmentOutcomeInput,
  GuestOutcomeInput,
} from '@/lib/types';

type Props = {
  id: string;
  online: boolean;
  onClose: () => void;
  onChanged: () => void;
  onEdit: (detail: AppointmentDetail) => void;
};

type Attendance = AppointmentOutcomeInput['attendance'];

type GuestForm = {
  attendance: Attendance;
  summary: string;
  willRepeat: boolean;
  postNotes: string;
  isClosed: boolean;
};

const attendanceOptions: { value: Attendance; label: string }[] = [
  { value: 'Arrived', label: 'Geldi' },
  { value: 'NoShow', label: 'Gelmedi' },
  { value: 'Postponed', label: 'Ertelendi' },
  { value: 'Cancelled', label: 'İptal' },
];

function guestForm(guest: AppointmentGuest): GuestForm {
  return {
    attendance: guest.outcome?.attendance ?? 'Arrived',
    summary: guest.outcome?.summary ?? '',
    willRepeat: guest.outcome?.willRepeat ?? false,
    postNotes: guest.outcome?.postNotes ?? '',
    isClosed: guest.outcome?.isClosed ?? false,
  };
}

function aggregate(forms: GuestForm[]): Attendance {
  if (forms.every((item) => item.attendance === 'NoShow')) return 'NoShow';
  if (forms.every((item) => item.attendance === 'Cancelled')) return 'Cancelled';
  if (forms.every((item) => item.attendance === 'Postponed')) return 'Postponed';
  return forms.some((item) => item.attendance === 'Arrived') ? 'Arrived' : forms[0]?.attendance ?? 'Arrived';
}

export function DetailView({ id, online, onClose, onChanged, onEdit }: Props) {
  const [detail, setDetail] = useState<AppointmentDetail | null>(null);
  const [outcome, setOutcome] = useState<AppointmentOutcome | null>(null);
  const [guestForms, setGuestForms] = useState<Record<string, GuestForm>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [attendance, setAttendance] = useState<Attendance>('Arrived');
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

        const forms: Record<string, GuestForm> = {};
        for (const guest of appointment.guests ?? []) {
          forms[guest.guestId] = guestForm(guest);
        }
        setGuestForms(forms);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Detay yüklenemedi.');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const updateGuest = (guestId: string, patch: Partial<GuestForm>) => {
    setGuestForms((current) => ({
      ...current,
      [guestId]: { ...(current[guestId] ?? guestForm({} as AppointmentGuest)), ...patch },
    }));
  };

  const saveOutcome = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!detail) return;

    setBusy(true);
    setError(null);

    try {
      const guests = detail.guests ?? [];
      const guestOutcomes: GuestOutcomeInput[] | null =
        guests.length > 1
          ? guests.map((guest) => {
              const form = guestForms[guest.guestId];
              return {
                guestId: guest.guestId,
                attendance: form?.attendance ?? 'Arrived',
                summary: form?.summary?.trim() || null,
                willRepeat: form?.willRepeat ?? false,
                postNotes: form?.postNotes?.trim() || null,
                isClosed: form?.isClosed ?? isClosed,
              };
            })
          : null;

      const aggregateAttendance = guestOutcomes
        ? aggregate(
            guestOutcomes.map((item) => ({
              attendance: item.attendance,
              summary: item.summary ?? '',
              willRepeat: item.willRepeat,
              postNotes: item.postNotes ?? '',
              isClosed: item.isClosed,
            })),
          )
        : attendance;

      await api.saveOutcome(detail.id, {
        attendance: aggregateAttendance,
        summary: summary.trim() || null,
        willRepeat,
        postNotes: postNotes.trim() || null,
        isClosed,
        guestOutcomes,
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
                  <dt>Birincil misafir</dt>
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
                  <dd>{detail.hostNameWithTitle}</dd>
                </div>
                <div>
                  <dt>Oda</dt>
                  <dd>{detail.roomDisplayName}</dd>
                </div>
                <div>
                  <dt>Katılımcılar</dt>
                  <dd>
                    {(detail.participants ?? [])
                      .map((participant) => participant.displayNameWithTitle)
                      .join(', ') || '—'}
                  </dd>
                </div>
                <div>
                  <dt>Yöntem</dt>
                  <dd>
                    {detail.method === 'FaceToFace'
                      ? 'Yüz yüze'
                      : detail.method === 'Phone'
                        ? 'Telefon'
                        : detail.method === 'WhatsApp'
                          ? 'WhatsApp'
                          : 'İnternet'}
                  </dd>
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

            {detail.hasMultipleGuests ? (
              <div className="card" style={{ boxShadow: 'none' }}>
                <div className="card-header">
                  <h2>Misafir sonuçları</h2>
                  <span className="muted">Her misafir bağımsız değerlendirilir.</span>
                </div>

                {(detail.guests ?? []).map((guest) => {
                  const form = guestForms[guest.guestId] ?? guestForm(guest);
                  return (
                    <div key={guest.guestId} className="card" style={{ boxShadow: 'none', marginBottom: 10 }}>
                      <div className="card-header">
                        <strong>{guest.fullName}</strong>
                        {guest.isPrimary ? <span className="badge">Birincil</span> : null}
                        <div className="spacer" />
                        <label className="checkbox-row">
                          <input
                            type="checkbox"
                            checked={form.isClosed}
                            onChange={(event) => updateGuest(guest.guestId, { isClosed: event.target.checked })}
                          />
                          Kapandı
                        </label>
                      </div>

                      <div className="grid-2">
                        <div className="field">
                          <label>Katılım</label>
                          <select
                            className="select"
                            value={form.attendance}
                            onChange={(event) =>
                              updateGuest(guest.guestId, { attendance: event.target.value as Attendance })
                            }
                          >
                            {attendanceOptions.map((option) => (
                              <option key={option.value} value={option.value}>
                                {option.label}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div className="field">
                          <label className="checkbox-row" style={{ paddingTop: 26 }}>
                            <input
                              type="checkbox"
                              checked={form.willRepeat}
                              onChange={(event) => updateGuest(guest.guestId, { willRepeat: event.target.checked })}
                            />
                            Tekrar görüşmek ister
                          </label>
                        </div>
                      </div>

                      <div className="field" style={{ marginTop: 8 }}>
                        <label>Değerlendirme</label>
                        <textarea
                          className="textarea"
                          rows={2}
                          value={form.summary}
                          onChange={(event) => updateGuest(guest.guestId, { summary: event.target.value })}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : null}

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
                    onChange={(event) => setAttendance(event.target.value as Attendance)}
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
