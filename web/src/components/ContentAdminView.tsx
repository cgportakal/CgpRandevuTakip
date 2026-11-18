'use client';

import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { api, API_URL } from '@/lib/api';
import { realtimeEvents } from '@/lib/realtime';
import type {
  Category,
  ContentAdminData,
  ContentSettingsInput,
  Host,
} from '@/lib/types';

type Props = {
  online: boolean;
};

type Tab = 'categories' | 'hosts' | 'settings' | 'publish';

type CategoryForm = {
  id: string | null;
  name: string;
  colorHex: string;
  isActive: boolean;
};

type HostForm = {
  id: string | null;
  displayName: string;
  isActive: boolean;
  loginEnabled: boolean;
  username: string;
  password: string;
  confirmPassword: string;
};

const emptyCategory: CategoryForm = {
  id: null,
  name: '',
  colorHex: '#2E7D5B',
  isActive: true,
};

const emptyHost: HostForm = {
  id: null,
  displayName: '',
  isActive: true,
  loginEnabled: false,
  username: '',
  password: '',
  confirmPassword: '',
};

const reminderOptions = [
  { value: 30, label: '30 dakika' },
  { value: 60, label: '1 saat' },
  { value: 1440, label: '1 gün' },
];

export function ContentAdminView({ online }: Props) {
  const [tab, setTab] = useState<Tab>('categories');
  const [data, setData] = useState<ContentAdminData | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [categoryForm, setCategoryForm] = useState<CategoryForm>(emptyCategory);
  const [hostForm, setHostForm] = useState<HostForm>(emptyHost);
  const [settingsForm, setSettingsForm] = useState<ContentSettingsInput | null>(null);
  const [publishNote, setPublishNote] = useState('');
  const [logoBusy, setLogoBusy] = useState(false);

  const load = useCallback(async () => {
    if (!online) {
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const content = await api.getContentAdmin();
      setData(content);
      setSettingsForm(toSettingsInput(content));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'İçerik yönetimi yüklenemedi.');
    } finally {
      setLoading(false);
    }
  }, [online]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const handler = () => void load();
    realtimeEvents.addEventListener('contentChanged', handler);
    return () => realtimeEvents.removeEventListener('contentChanged', handler);
  }, [load]);

  const run = async (action: () => Promise<void>, successMessage: string) => {
    if (busy) return;
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      await action();
      setNotice(successMessage);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'İşlem başarısız.');
    } finally {
      setBusy(false);
    }
  };

  const saveCategory = async () => {
    const input = {
      name: categoryForm.name,
      colorHex: categoryForm.colorHex,
      isActive: categoryForm.isActive,
    };

    await run(async () => {
      if (categoryForm.id) {
        await api.updateCategory(categoryForm.id, input);
      } else {
        await api.createCategory(input);
      }
      setCategoryForm(emptyCategory);
    }, categoryForm.id ? 'Kategori güncellendi.' : 'Kategori eklendi.');
  };

  const saveHost = async () => {
    await run(async () => {
      if (hostForm.id) {
        await api.updateHost(hostForm.id, {
          displayName: hostForm.displayName,
          isActive: hostForm.isActive,
        });
      } else {
        await api.createHost({
          displayName: hostForm.displayName,
          loginEnabled: hostForm.loginEnabled,
          username: hostForm.loginEnabled ? hostForm.username : null,
          password: hostForm.loginEnabled ? hostForm.password : null,
          confirmPassword: hostForm.loginEnabled ? hostForm.confirmPassword : null,
        });
      }
      setHostForm(emptyHost);
    }, hostForm.id ? 'Görüşmeci güncellendi.' : 'Görüşmeci eklendi.');
  };

  const saveSettings = async () => {
    if (!settingsForm) return;
    await run(() => api.saveContentSettings(settingsForm), 'Kurum ve ayarlar kaydedildi.');
  };

  const uploadLogo = async (file: File) => {
    if (logoBusy) return;

    setLogoBusy(true);
    setError(null);
    setNotice(null);
    try {
      await api.uploadContentLogo(file);
      setNotice('Kurum logosu yüklendi.');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Logo yüklenemedi.');
    } finally {
      setLogoBusy(false);
    }
  };

  const publish = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const version = await api.publishContent(publishNote);
      setNotice(`İçerik v${version.versionNumber} olarak yayınlandı.`);
      setPublishNote('');
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'İçerik yayınlanamadı.');
    } finally {
      setBusy(false);
    }
  };

  const restore = async (versionId: string, versionNumber: number) => {
    if (!window.confirm(`v${versionNumber} sürümüne geri dönülsün mü?`)) return;
    if (busy) return;
    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const version = await api.restoreContentVersion(versionId);
      setNotice(`İçerik geri alındı ve v${version.versionNumber} olarak yayınlandı.`);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'İçerik geri alınamadı.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="card">
        <div className="toolbar">
          <div className="tabs">
            <TabButton active={tab === 'categories'} onClick={() => setTab('categories')}>
              Kategoriler
            </TabButton>
            <TabButton active={tab === 'hosts'} onClick={() => setTab('hosts')}>
              Görüşmeciler
            </TabButton>
            <TabButton active={tab === 'settings'} onClick={() => setTab('settings')}>
              Kurum ve Ayarlar
            </TabButton>
            <TabButton active={tab === 'publish'} onClick={() => setTab('publish')}>
              Yayınla ve Geri Al
            </TabButton>
          </div>
          <div className="spacer" />
          {data?.currentVersion ? (
            <span className="badge live">Yayın: v{data.currentVersion.versionNumber}</span>
          ) : (
            <span className="badge warn">Henüz yayın yok</span>
          )}
          <button className="btn btn-sm" onClick={() => void load()} disabled={!online || loading || busy}>
            Yenile
          </button>
        </div>

        {!online ? <div className="alert warn">İçerik yönetimi için sunucu bağlantısı gerekir.</div> : null}
        {error ? <div className="alert error">{error}</div> : null}
        {notice ? <div className="alert info">{notice}</div> : null}
      </div>

      {loading && !data ? <div className="empty">İçerik yönetimi yükleniyor…</div> : null}

      {data && tab === 'categories' ? (
        <CategoriesPanel
          categories={data.categories}
          form={categoryForm}
          setForm={setCategoryForm}
          busy={busy}
          onSave={saveCategory}
          onMove={(id, direction) =>
            void run(() => api.moveCategory(id, direction), 'Kategori sırası güncellendi.')
          }
          onToggle={(category) =>
            void run(
              () => api.setCategoryActive(category.id, !category.isActive),
              category.isActive ? 'Kategori pasifleştirildi.' : 'Kategori etkinleştirildi.',
            )
          }
          onDelete={(category) => {
            if (!window.confirm(`"${category.name}" kategorisi silinsin mi?`)) return;
            void run(() => api.deleteCategory(category.id), 'Kategori silindi.');
          }}
        />
      ) : null}

      {data && tab === 'hosts' ? (
        <HostsPanel
          hosts={data.hosts}
          form={hostForm}
          setForm={setHostForm}
          busy={busy}
          onSave={saveHost}
          onToggle={(host) =>
            void run(
              () => api.updateHost(host.id, { displayName: host.displayName, isActive: !host.isActive }),
              host.isActive ? 'Görüşmeci pasifleştirildi.' : 'Görüşmeci etkinleştirildi.',
            )
          }
        />
      ) : null}

      {data && tab === 'settings' && settingsForm ? (
        <SettingsPanel
          form={settingsForm}
          setForm={setSettingsForm}
          busy={busy}
          logoUrl={data.institution.logoUrl}
          logoBusy={logoBusy}
          onUploadLogo={uploadLogo}
          onSave={saveSettings}
        />
      ) : null}

      {data && tab === 'publish' ? (
        <PublishPanel
          data={data}
          note={publishNote}
          setNote={setPublishNote}
          busy={busy}
          onPublish={publish}
          onRestore={restore}
        />
      ) : null}
    </>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button className={`tab ${active ? 'active' : ''}`} onClick={onClick}>
      {children}
    </button>
  );
}

function CategoriesPanel({
  categories,
  form,
  setForm,
  busy,
  onSave,
  onMove,
  onToggle,
  onDelete,
}: {
  categories: Category[];
  form: CategoryForm;
  setForm: (form: CategoryForm) => void;
  busy: boolean;
  onSave: () => void;
  onMove: (id: string, direction: -1 | 1) => void;
  onToggle: (category: Category) => void;
  onDelete: (category: Category) => void;
}) {
  return (
    <>
      <div className="card">
        <div className="card-header">
          <h2>{form.id ? 'Kategoriyi düzenle' : 'Yeni kategori'}</h2>
        </div>
        <div className="grid-3">
          <div className="field">
            <label>Ad</label>
            <input
              className="input"
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
              placeholder="Örn. Veli Görüşmesi"
            />
          </div>
          <div className="field">
            <label>Renk (#RRGGBB)</label>
            <input
              className="input"
              value={form.colorHex}
              onChange={(event) => setForm({ ...form, colorHex: event.target.value })}
              placeholder="#2E7D5B"
            />
          </div>
          <div className="field">
            <label>Durum</label>
            <label className="checkbox-row" style={{ marginTop: 8 }}>
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
              />
              Etkin
            </label>
          </div>
        </div>
        <div className="toolbar" style={{ marginTop: 14 }}>
          <button className="btn btn-primary btn-sm" onClick={onSave} disabled={busy || !form.name.trim()}>
            {form.id ? 'Güncelle' : 'Ekle'}
          </button>
          {form.id ? (
            <button className="btn btn-sm" onClick={() => setForm(emptyCategory)} disabled={busy}>
              Vazgeç
            </button>
          ) : null}
        </div>
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Kategoriler</h2>
          <span className="muted">{categories.length} kayıt</span>
        </div>
        {categories.length === 0 ? (
          <div className="empty">Henüz kategori yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Sıra</th>
                <th>Ad</th>
                <th>Renk</th>
                <th>Kullanım</th>
                <th>Durum</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {categories.map((category, index) => (
                <tr key={category.id}>
                  <td>
                    <button className="btn btn-ghost btn-sm" onClick={() => onMove(category.id, -1)} disabled={busy || index === 0}>
                      ↑
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      onClick={() => onMove(category.id, 1)}
                      disabled={busy || index === categories.length - 1}
                    >
                      ↓
                    </button>
                  </td>
                  <td>
                    <strong>{category.name}</strong>
                  </td>
                  <td>
                    <span
                      style={{
                        display: 'inline-block',
                        width: 18,
                        height: 18,
                        borderRadius: 6,
                        background: category.colorHex,
                        verticalAlign: 'middle',
                        marginRight: 8,
                      }}
                    />
                    {category.colorHex}
                  </td>
                  <td>{category.usageCount}</td>
                  <td>
                    <span className={`badge ${category.isActive ? 'live' : 'warn'}`}>
                      {category.isActive ? 'Etkin' : 'Pasif'}
                    </span>
                  </td>
                  <td>
                    <div className="toolbar" style={{ gap: 6 }}>
                      <button
                        className="btn btn-sm"
                        onClick={() =>
                          setForm({
                            id: category.id,
                            name: category.name,
                            colorHex: category.colorHex,
                            isActive: category.isActive,
                          })
                        }
                        disabled={busy}
                      >
                        Düzenle
                      </button>
                      <button className="btn btn-sm" onClick={() => onToggle(category)} disabled={busy}>
                        {category.isActive ? 'Pasifleştir' : 'Etkinleştir'}
                      </button>
                      <button
                        className="btn btn-danger btn-sm"
                        onClick={() => onDelete(category)}
                        disabled={busy || category.usageCount > 0}
                        title={category.usageCount > 0 ? 'Kullanılan kategori silinemez.' : undefined}
                      >
                        Sil
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

function HostsPanel({
  hosts,
  form,
  setForm,
  busy,
  onSave,
  onToggle,
}: {
  hosts: Host[];
  form: HostForm;
  setForm: (form: HostForm) => void;
  busy: boolean;
  onSave: () => void;
  onToggle: (host: Host) => void;
}) {
  const hostUsers = hosts.filter((host) => host.role === 'Host');

  return (
    <>
      <div className="card">
        <div className="card-header">
          <h2>{form.id ? 'Görüşmeciyi düzenle' : 'Yeni görüşmeci'}</h2>
        </div>
        <div className="grid-2">
          <div className="field">
            <label>Ad soyad</label>
            <input
              className="input"
              value={form.displayName}
              onChange={(event) => setForm({ ...form, displayName: event.target.value })}
              placeholder="Ad Soyad"
            />
          </div>
          <div className="field">
            <label>Durum</label>
            <label className="checkbox-row" style={{ marginTop: 8 }}>
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) => setForm({ ...form, isActive: event.target.checked })}
              />
              Etkin
            </label>
          </div>
        </div>

        {!form.id ? (
          <div style={{ marginTop: 14 }}>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={form.loginEnabled}
                onChange={(event) => setForm({ ...form, loginEnabled: event.target.checked })}
              />
              Giriş hesabı oluşturulsun
            </label>
            {form.loginEnabled ? (
              <div className="grid-3" style={{ marginTop: 12 }}>
                <div className="field">
                  <label>Kullanıcı adı</label>
                  <input
                    className="input"
                    value={form.username}
                    onChange={(event) => setForm({ ...form, username: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label>Parola</label>
                  <input
                    className="input"
                    type="password"
                    value={form.password}
                    onChange={(event) => setForm({ ...form, password: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label>Parola tekrar</label>
                  <input
                    className="input"
                    type="password"
                    value={form.confirmPassword}
                    onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })}
                  />
                </div>
              </div>
            ) : null}
          </div>
        ) : null}

        <div className="toolbar" style={{ marginTop: 14 }}>
          <button className="btn btn-primary btn-sm" onClick={onSave} disabled={busy || !form.displayName.trim()}>
            {form.id ? 'Güncelle' : 'Ekle'}
          </button>
          {form.id ? (
            <button className="btn btn-sm" onClick={() => setForm(emptyHost)} disabled={busy}>
              Vazgeç
            </button>
          ) : null}
        </div>
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Görüşmeciler</h2>
          <span className="muted">{hostUsers.length} görüşmeci</span>
        </div>
        {hostUsers.length === 0 ? (
          <div className="empty">Henüz görüşmeci yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Ad soyad</th>
                <th>Kullanıcı adı</th>
                <th>Giriş</th>
                <th>Görüşme</th>
                <th>Durum</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {hostUsers.map((host) => (
                <tr key={host.id}>
                  <td>
                    <strong>{host.displayName}</strong>
                  </td>
                  <td>{host.username}</td>
                  <td>{host.isLoginEnabled ? 'Açık' : 'Kapalı'}</td>
                  <td>{host.appointmentCount}</td>
                  <td>
                    <span className={`badge ${host.isActive ? 'live' : 'warn'}`}>
                      {host.isActive ? 'Etkin' : 'Pasif'}
                    </span>
                  </td>
                  <td>
                    <div className="toolbar" style={{ gap: 6 }}>
                      <button
                        className="btn btn-sm"
                        onClick={() =>
                          setForm({
                            ...emptyHost,
                            id: host.id,
                            displayName: host.displayName,
                            isActive: host.isActive,
                          })
                        }
                        disabled={busy}
                      >
                        Düzenle
                      </button>
                      <button className="btn btn-sm" onClick={() => onToggle(host)} disabled={busy}>
                        {host.isActive ? 'Pasifleştir' : 'Etkinleştir'}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

function SettingsPanel({
  form,
  setForm,
  busy,
  logoUrl,
  logoBusy,
  onUploadLogo,
  onSave,
}: {
  form: ContentSettingsInput;
  setForm: (form: ContentSettingsInput) => void;
  busy: boolean;
  logoUrl: string | null;
  logoBusy: boolean;
  onUploadLogo: (file: File) => void;
  onSave: () => void;
}) {
  return (
    <div className="card">
      <div className="card-header">
        <h2>Kurum ve genel ayarlar</h2>
        <span className="muted">Bu alanlar yayınlandığında web ve mobil istemcilere yansır.</span>
      </div>

      <div className="field" style={{ marginBottom: 16 }}>
        <label>Kurum logosu</label>
        {logoUrl ? (
          <img
            src={logoUrl.startsWith('http') ? logoUrl : `${API_URL}${logoUrl}`}
            alt="Kurum logosu"
            style={{
              maxHeight: 72,
              maxWidth: 220,
              objectFit: 'contain',
              alignSelf: 'flex-start',
              border: '1px solid var(--border)',
              borderRadius: 8,
              padding: 6,
              background: '#fff',
            }}
          />
        ) : (
          <span className="muted">Logo yüklenmemiş.</span>
        )}
        <input
          type="file"
          accept=".png,.jpg,.jpeg,.bmp"
          disabled={logoBusy || busy}
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) onUploadLogo(file);
            event.currentTarget.value = '';
          }}
        />
        <span className="muted">
          PNG, JPG veya BMP · en fazla 4 MB{logoBusy ? ' · Yükleniyor…' : ''}
        </span>
      </div>

      <div className="grid-2">
        <div className="field">
          <label>Kurum adı</label>
          <input
            className="input"
            value={form.institutionName}
            onChange={(event) => setForm({ ...form, institutionName: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Kurum e-postası</label>
          <input
            className="input"
            type="email"
            value={form.institutionEmail}
            onChange={(event) => setForm({ ...form, institutionEmail: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Telefon</label>
          <input
            className="input"
            value={form.institutionPhone}
            onChange={(event) => setForm({ ...form, institutionPhone: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Adres</label>
          <textarea
            className="textarea"
            rows={3}
            value={form.institutionAddress}
            onChange={(event) => setForm({ ...form, institutionAddress: event.target.value })}
          />
        </div>
      </div>

      <div className="grid-3" style={{ marginTop: 14 }}>
        <div className="field">
          <label>Varsayılan hatırlatıcı</label>
          <select
            className="select"
            value={form.defaultReminderMinutes}
            onChange={(event) => setForm({ ...form, defaultReminderMinutes: Number(event.target.value) })}
          >
            {reminderOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Çalışma saati başlangıcı</label>
          <input
            className="input"
            type="time"
            value={form.workHoursStart}
            onChange={(event) => setForm({ ...form, workHoursStart: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Çalışma saati bitişi</label>
          <input
            className="input"
            type="time"
            value={form.workHoursEnd}
            onChange={(event) => setForm({ ...form, workHoursEnd: event.target.value })}
          />
        </div>
      </div>

      <div className="grid-3" style={{ marginTop: 14 }}>
        <div className="field">
          <label>Günlük paylaşım saati</label>
          <input
            className="input"
            type="time"
            value={form.dailyShareTime}
            onChange={(event) => setForm({ ...form, dailyShareTime: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Sessiz saat başlangıcı</label>
          <input
            className="input"
            type="time"
            value={form.quietHoursStart}
            onChange={(event) => setForm({ ...form, quietHoursStart: event.target.value })}
          />
        </div>
        <div className="field">
          <label>Sessiz saat bitişi</label>
          <input
            className="input"
            type="time"
            value={form.quietHoursEnd}
            onChange={(event) => setForm({ ...form, quietHoursEnd: event.target.value })}
          />
        </div>
      </div>

      <div className="toolbar" style={{ marginTop: 16, gap: 18 }}>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.allowOutOfHoursAppointments}
            onChange={(event) => setForm({ ...form, allowOutOfHoursAppointments: event.target.checked })}
          />
          Çalışma saatleri dışında randevu
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.notificationsEnabled}
            onChange={(event) => setForm({ ...form, notificationsEnabled: event.target.checked })}
          />
          Bildirimler açık
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.pushEnabled}
            onChange={(event) => setForm({ ...form, pushEnabled: event.target.checked })}
          />
          Push açık
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.quietHoursEnabled}
            onChange={(event) => setForm({ ...form, quietHoursEnabled: event.target.checked })}
          />
          Sessiz saatler açık
        </label>
      </div>

      <div className="toolbar" style={{ marginTop: 16 }}>
        <button className="btn btn-primary" onClick={onSave} disabled={busy || !form.institutionName.trim()}>
          Kaydet
        </button>
      </div>
    </div>
  );
}

function PublishPanel({
  data,
  note,
  setNote,
  busy,
  onPublish,
  onRestore,
}: {
  data: ContentAdminData;
  note: string;
  setNote: (note: string) => void;
  busy: boolean;
  onPublish: () => void;
  onRestore: (id: string, versionNumber: number) => void;
}) {
  return (
    <>
      <div className="card">
        <div className="card-header">
          <h2>Yayınla</h2>
          {data.currentVersion ? (
            <span className="badge live">Güncel: v{data.currentVersion.versionNumber}</span>
          ) : (
            <span className="badge warn">Henüz yayınlanmadı</span>
          )}
        </div>
        <p className="muted">
          Kategori, görüşmeci ve ayar değişiklikleri kaydedildiğinde canlı veriye uygulanır.
          Yayınlama, bu içeriğin anlık görüntüsünü saklar ve bağlı istemcilere <code>contentChanged</code>
          olayı gönderir.
        </p>
        <div className="field" style={{ marginTop: 12 }}>
          <label>Yayın notu (isteğe bağlı)</label>
          <input
            className="input"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Örn. Kasım dönemi kategori güncellemesi"
          />
        </div>
        <div className="toolbar" style={{ marginTop: 12 }}>
          <button className="btn btn-primary" onClick={onPublish} disabled={busy}>
            İçeriği yayınla
          </button>
        </div>
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Sürüm geçmişi</h2>
          <span className="muted">Son 20 yayın</span>
        </div>
        {data.versions.length === 0 ? (
          <div className="empty">Henüz yayınlanmış bir sürüm yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Sürüm</th>
                <th>Tarih</th>
                <th>Yayınlayan</th>
                <th>Not</th>
                <th>Kategori</th>
                <th>Görüşmeci</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {data.versions.map((version) => (
                <tr key={version.id}>
                  <td>
                    <strong>v{version.versionNumber}</strong>{' '}
                    {version.isCurrent ? <span className="badge live">Güncel</span> : null}
                  </td>
                  <td>{new Date(version.publishedAtLocal).toLocaleString('tr-TR')}</td>
                  <td>{version.publishedBy}</td>
                  <td>{version.note ?? '—'}</td>
                  <td>{version.categoryCount}</td>
                  <td>{version.hostCount}</td>
                  <td>
                    <button
                      className="btn btn-sm"
                      onClick={() => onRestore(version.id, version.versionNumber)}
                      disabled={busy || version.isCurrent}
                    >
                      Geri al
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

function toSettingsInput(data: ContentAdminData): ContentSettingsInput {
  return {
    institutionName: data.institution.name,
    institutionEmail: data.institution.email,
    institutionPhone: data.institution.phone,
    institutionAddress: data.institution.address,
    defaultReminderMinutes: data.settings.defaultReminderMinutes,
    workHoursStart: data.settings.workHoursStart,
    workHoursEnd: data.settings.workHoursEnd,
    allowOutOfHoursAppointments: data.settings.allowOutOfHoursAppointments,
    notificationsEnabled: data.settings.notificationsEnabled,
    pushEnabled: data.settings.pushEnabled,
    quietHoursEnabled: data.settings.quietHoursEnabled,
    quietHoursStart: data.settings.quietHoursStart,
    quietHoursEnd: data.settings.quietHoursEnd,
    dailyShareTime: data.settings.dailyShareTime,
  };
}
