'use client';

import { useEffect, useMemo, useState } from 'react';
import { api, ApiError } from '@/lib/api';
import { minutesToLabel } from '@/lib/dates';
import type {
  AppointmentDetail,
  AppointmentInput,
  AppointmentMethod,
  ContentManifest,
  SeriesFrequency,
} from '@/lib/types';

type Props = {
  manifest: ContentManifest | null;
  defaultDate: string;
  initial?: AppointmentDetail | null;
  onClose: () => void;
  onSaved: () => void;
};

type Conflict = {
  personName: string;
  startsAtLocal: string;
  categoryName?: string | null;
};

const methods: { value: AppointmentMethod; label: string }[] = [
  { value: 'FaceToFace', label: 'Yüz yüze' },
  { value: 'Phone', label: 'Telefon' },
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'Online', label: 'İnternet' },
];

const frequencies: { value: SeriesFrequency; label: string }[] = [
  { value: 'Weekly', label: 'Her hafta' },
  { value: 'Biweekly', label: '2 haftada bir' },
  { value: 'Monthly', label: 'Her ay' },
];

function nextHalfHour(): string {
  const now = new Date();
  now.setMinutes(now.getMinutes() + (30 - (now.getMinutes() % 30)));
  return `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
}

export function AppointmentForm({ manifest, defaultDate, initial, onClose, onSaved }: Props) {
  const editing = Boolean(initial);

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [date, setDate] = useState(defaultDate);
  const [time, setTime] = useState(nextHalfHour());
  const [duration, setDuration] = useState(30);
  const [hostUserId, setHostUserId] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [method, setMethod] = useState<AppointmentMethod>('FaceToFace');
  const [description, setDescription] = useState('');
  const [studentFirstName, setStudentFirstName] = useState('');
  const [studentLastName, setStudentLastName] = useState('');
  const [reminders, setReminders] = useState<number[]>([manifest?.settings.defaultReminderMinutes ?? 30]);
  const [seriesEnabled, setSeriesEnabled] = useState(false);
  const [frequency, setFrequency] = useState<SeriesFrequency>('Weekly');
  const [count, setCount] = useState(4);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [conflicts, setConflicts] = useState<Conflict[]>([]);
  const [ignoreConflicts, setIgnoreConflicts] = useState(false);
  const [seriesNote, setSeriesNote] = useState<string | null>(null);

  useEffect(() => {
    setHostUserId(manifest?.hosts[0]?.id ?? '');
    setCategoryId(manifest?.categories[0]?.id ?? '');
    setDate(defaultDate);
    setTime(nextHalfHour());
  }, [manifest, defaultDate]);

  useEffect(() => {
    if (!initial) return;

    const [first = '', ...rest] = initial.personFullName.split(' ');
    setFirstName(first);
    setLastName(rest.join(' '));
    setPhone(initial.phoneE164);
    setDate(initial.startsAtLocal.slice(0, 10));
    setTime(initial.startsAtLocal.slice(11, 16));
    setDuration(initial.durationMinutes);
    setHostUserId(initial.hostUserId);
    setCategoryId(initial.categoryId);
    setMethod(initial.method);
    setDescription(initial.description ?? '');
    setStudentFirstName(initial.studentFullName ?? '');
    setStudentLastName('');
    setReminders([manifest?.settings.defaultReminderMinutes ?? 30]);
  }, [initial, manifest]);

  const reminderOptions = useMemo(() => {
    const defaults = [30, 60, 1440];
    const extra = manifest?.settings.defaultReminderMinutes;
    const values = extra && !defaults.includes(extra) ? [extra, ...defaults] : defaults;
    return values.sort((a, b) => a - b);
  }, [manifest]);

  const toggleReminder = (value: number) => {
    setReminders((current) =>
      current.includes(value) ? current.filter((item) => item !== value) : [...current, value].sort((a, b) => a - b),
    );
  };

  const buildInput = (force = false): AppointmentInput => ({
    firstName: firstName.trim(),
    lastName: lastName.trim(),
    phone: phone.trim(),
    startsAtLocal: `${date}T${time}:00`,
    durationMinutes: duration,
    hostUserId,
    categoryId,
    method,
    description: description.trim() || null,
    studentFirstName: studentFirstName.trim() || null,
    studentLastName: studentLastName.trim() || null,
    reminderOffsetsMinutes: reminders,
    ignoreConflicts: force || ignoreConflicts,
  });

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    setSeriesNote(null);

    if (!firstName.trim() || !lastName.trim() || !phone.trim() || !hostUserId || !categoryId) {
      setError('Ad, soyad, telefon, görüşmeci ve kategori zorunludur.');
      return;
    }

    if (seriesEnabled && (count < 2 || count > 12)) {
      setError('Seri kayıt sayısı 2 ile 12 arasında olmalıdır.');
      return;
    }

    setBusy(true);

    try {
      if (editing && initial) {
        await api.updateAppointment(initial.id, buildInput());
      } else if (seriesEnabled) {
        const result = await api.createSeries(buildInput(false), frequency, count);
        setSeriesNote(result.summary);
        window.setTimeout(onSaved, 600);
        return;
      } else {
        await api.createAppointment(buildInput());
      }

      onSaved();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        const payload = err.payload as { conflicts?: Conflict[] } | undefined;
        if (payload?.conflicts?.length) setConflicts(payload.conflicts);
      } else {
        setError('Kayıt yapılamadı.');
      }
    } finally {
      setBusy(false);
    }
  };

  const retryIgnoringConflicts = async () => {
    setIgnoreConflicts(true);
    setBusy(true);
    setError(null);

    try {
      await api.createAppointment(buildInput(true));
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Kayıt yapılamadı.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <form className="modal" onSubmit={submit}>
        <div className="modal-header">
          <h2>{editing ? 'Görüşmeyi düzenle' : 'Yeni görüşme'}</h2>
          <div className="spacer" />
          <button type="button" className="btn btn-sm btn-ghost" onClick={onClose}>
            Kapat
          </button>
        </div>

        <div className="grid-2">
          <div className="field">
            <label>Ad</label>
            <input className="input" value={firstName} onChange={(event) => setFirstName(event.target.value)} autoFocus />
          </div>
          <div className="field">
            <label>Soyad</label>
            <input className="input" value={lastName} onChange={(event) => setLastName(event.target.value)} />
          </div>
        </div>

        <div className="grid-2" style={{ marginTop: 12 }}>
          <div className="field">
            <label>Telefon</label>
            <input className="input" value={phone} onChange={(event) => setPhone(event.target.value)} placeholder="05551234567" />
          </div>
          <div className="field">
            <label>Yöntem</label>
            <select className="select" value={method} onChange={(event) => setMethod(event.target.value as AppointmentMethod)}>
              {methods.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="grid-3" style={{ marginTop: 12 }}>
          <div className="field">
            <label>Tarih</label>
            <input className="input" type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          </div>
          <div className="field">
            <label>Saat</label>
            <input className="input" type="time" value={time} onChange={(event) => setTime(event.target.value)} />
          </div>
          <div className="field">
            <label>Süre</label>
            <select className="select" value={duration} onChange={(event) => setDuration(Number(event.target.value))}>
              {[15, 20, 30, 45, 60, 90].map((value) => (
                <option key={value} value={value}>
                  {value} dk
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="grid-2" style={{ marginTop: 12 }}>
          <div className="field">
            <label>Görüşmeci</label>
            <select className="select" value={hostUserId} onChange={(event) => setHostUserId(event.target.value)}>
              <option value="">Seçin</option>
              {(manifest?.hosts ?? []).map((host) => (
                <option key={host.id} value={host.id}>
                  {host.displayName}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Kategori</label>
            <select className="select" value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">Seçin</option>
              {(manifest?.categories ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="grid-2" style={{ marginTop: 12 }}>
          <div className="field">
            <label>Öğrenci adı (isteğe bağlı)</label>
            <input className="input" value={studentFirstName} onChange={(event) => setStudentFirstName(event.target.value)} />
          </div>
          <div className="field">
            <label>Öğrenci soyadı</label>
            <input className="input" value={studentLastName} onChange={(event) => setStudentLastName(event.target.value)} />
          </div>
        </div>

        <div className="field" style={{ marginTop: 12 }}>
          <label>Açıklama</label>
          <textarea
            className="textarea"
            rows={2}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </div>

        <div className="field" style={{ marginTop: 14 }}>
          <label>Hatırlatıcılar</label>
          <div className="checkbox-row">
            {reminderOptions.map((value) => (
              <label key={value}>
                <input
                  type="checkbox"
                  checked={reminders.includes(value)}
                  onChange={() => toggleReminder(value)}
                />
                {minutesToLabel(value)} önce
              </label>
            ))}
          </div>
        </div>

        {!editing ? (
          <div className="series-box">
            <div className="checkbox-row">
              <label>
                <input
                  type="checkbox"
                  checked={seriesEnabled}
                  onChange={(event) => setSeriesEnabled(event.target.checked)}
                />
                Tekrarlayan görüşme serisi
              </label>
            </div>

            {seriesEnabled ? (
              <div className="grid-2" style={{ marginTop: 12 }}>
                <div className="field">
                  <label>Sıklık</label>
                  <select
                    className="select"
                    value={frequency}
                    onChange={(event) => setFrequency(event.target.value as SeriesFrequency)}
                  >
                    {frequencies.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Kayıt sayısı (2–12)</label>
                  <select className="select" value={count} onChange={(event) => setCount(Number(event.target.value))}>
                    {Array.from({ length: 11 }, (_, index) => index + 2).map((value) => (
                      <option key={value} value={value}>
                        {value} kayıt
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            ) : null}
          </div>
        ) : null}

        {error ? <div className="alert error">{error}</div> : null}
        {seriesNote ? <div className="alert info">{seriesNote}</div> : null}

        {conflicts.length > 0 ? (
          <div className="alert warn">
            <strong>Çakışan görüşmeler:</strong>
            <ul style={{ margin: '6px 0 0 18px' }}>
              {conflicts.map((conflict, index) => (
                <li key={index}>
                  {conflict.personName} · {new Date(conflict.startsAtLocal).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
                  {conflict.categoryName ? ` · ${conflict.categoryName}` : ''}
                </li>
              ))}
            </ul>
            <button type="button" className="btn btn-sm" style={{ marginTop: 10 }} onClick={() => void retryIgnoringConflicts()}>
              Çakışmaya rağmen kaydet
            </button>
          </div>
        ) : null}

        <div className="modal-footer">
          <button type="button" className="btn" onClick={onClose}>
            Vazgeç
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? 'Kaydediliyor…' : editing ? 'Güncelle' : seriesEnabled ? 'Seriyi oluştur' : 'Kaydet'}
          </button>
        </div>
      </form>
    </div>
  );
}
