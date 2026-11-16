'use client';

import { useEffect, useMemo, useState } from 'react';
import { api } from '@/lib/api';
import { thisYearRange, lastDaysRange, lastMonthRange, firstNameMonthRange } from '@/lib/dates';
import type { ContentManifest, ReportKind, ReportResult, ReportSummary } from '@/lib/types';

type Props = {
  manifest: ContentManifest | null;
  online: boolean;
};

const kinds: { value: ReportKind; label: string }[] = [
  { value: 'Summary', label: 'Özet' },
  { value: 'HostPerformance', label: 'Görüşmeci performansı' },
  { value: 'CategoryTrend', label: 'Kategori trendi' },
  { value: 'Attendance', label: 'Katılım' },
  { value: 'Repeat', label: 'Tekrar görüşme' },
  { value: 'PersonHistory', label: 'Kişi geçmişi' },
  { value: 'AuditActivity', label: 'Denetim / aktivite' },
];

export function ReportsView({ online }: Props) {
  const initial = firstNameMonthRange();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [kind, setKind] = useState<ReportKind>('Summary');
  const [personSearch, setPersonSearch] = useState('');
  const [summary, setSummary] = useState<ReportSummary | null>(null);
  const [report, setReport] = useState<ReportResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!online) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    void (async () => {
      try {
        const [overview, detail] = await Promise.all([
          api.getReportSummary(from, to),
          api.getReport(kind, from, to, kind === 'PersonHistory' ? personSearch : undefined),
        ]);

        if (cancelled) return;
        setSummary(overview);
        setReport(detail);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Rapor yüklenemedi.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [from, to, kind, personSearch, online]);

  const applyPreset = (range: { from: string; to: string }) => {
    setFrom(range.from);
    setTo(range.to);
  };

  const maxPoint = useMemo(
    () => Math.max(1, ...(summary?.points ?? []).map((point) => point.total)),
    [summary],
  );

  return (
    <>
      <div className="card">
        <div className="toolbar">
          <div className="field" style={{ width: 160 }}>
            <label>Başlangıç</label>
            <input className="input" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          </div>
          <div className="field" style={{ width: 160 }}>
            <label>Bitiş</label>
            <input className="input" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          </div>

          <div className="spacer" />

          <div className="tabs">
            <button className="tab" onClick={() => applyPreset(firstNameMonthRange())}>Bu ay</button>
            <button className="tab" onClick={() => applyPreset(lastMonthRange())}>Geçen ay</button>
            <button className="tab" onClick={() => applyPreset(lastDaysRange(30))}>Son 30 gün</button>
            <button className="tab" onClick={() => applyPreset(thisYearRange())}>Bu yıl</button>
          </div>
        </div>

        {!online ? <div className="alert warn">Raporlar için sunucu bağlantısı gerekir.</div> : null}
        {error ? <div className="alert error">{error}</div> : null}
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Özet</h2>
          {summary ? <span className="muted">{summary.rangeText}</span> : null}
          {loading ? <span className="muted">Yükleniyor…</span> : null}
        </div>

        {summary ? (
          <>
            <div className="kpi-grid">
              <Kpi value={summary.total} label="Toplam görüşme" />
              <Kpi value={summary.completed} label="Tamamlanan" />
              <Kpi value={summary.noShow} label="Gelmedi" />
              <Kpi value={summary.cancelled} label="İptal" />
              <Kpi value={summary.completionRateText} label="Tamamlanma" />
              <Kpi value={summary.averageDurationText} label="Ortalama süre" />
            </div>

            <div className="card" style={{ boxShadow: 'none', marginTop: 16 }}>
              <div className="card-header">
                <h2>{summary.bucketLabel}</h2>
              </div>
              {summary.points.length === 0 ? (
                <div className="muted">Bu aralıkta veri yok.</div>
              ) : (
                <div className="chart-scroll">
                  {summary.points.map((point) => (
                    <div key={point.label} style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
                      <span className="muted" style={{ width: 90 }}>{point.label}</span>
                      <div style={{ flex: 1, background: '#eef2ef', borderRadius: 6, height: 10 }}>
                        <div
                          style={{
                            width: `${Math.round((point.total / maxPoint) * 100)}%`,
                            background: '#2e7d5b',
                            height: 10,
                            borderRadius: 6,
                          }}
                        />
                      </div>
                      <span className="muted" style={{ width: 40, textAlign: 'right' }}>{point.total}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="empty">Rapor yükleniyor…</div>
        )}
      </div>

      <div className="card">
        <div className="toolbar">
          <div className="tabs">
            {kinds.map((item) => (
              <button
                key={item.value}
                className={`tab ${kind === item.value ? 'active' : ''}`}
                onClick={() => setKind(item.value)}
              >
                {item.label}
              </button>
            ))}
          </div>

          {kind === 'PersonHistory' ? (
            <>
              <div className="spacer" />
              <input
                className="input search"
                placeholder="Ad veya telefon ara"
                value={personSearch}
                onChange={(event) => setPersonSearch(event.target.value)}
              />
            </>
          ) : null}
        </div>
      </div>

      {report ? <ReportBody report={report} kind={kind} /> : null}
    </>
  );
}

function Kpi({ value, label }: { value: number | string; label: string }) {
  return (
    <div className="kpi">
      <div className="value">{value}</div>
      <div className="label">{label}</div>
    </div>
  );
}

function ReportBody({ report, kind }: { report: ReportResult; kind: ReportKind }) {
  const summary = report.summary;

  if (!summary) {
    return (
      <div className="card">
        <div className="alert error">Rapor verisi alınamadı; aralığı veya türü değiştirip yeniden deneyin.</div>
      </div>
    );
  }

  if (kind === 'CategoryTrend') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Kategori trendi</h2>
        </div>
        {report.categoryTrends.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Kategori</th>
                <th>Toplam</th>
                <th>Dağılım</th>
              </tr>
            </thead>
            <tbody>
              {report.categoryTrends.map((row) => (
                <tr key={row.categoryId}>
                  <td>
                    <span className="badge" style={{ background: row.colorHex, color: '#fff' }}>
                      {row.categoryName}
                    </span>
                  </td>
                  <td>{row.total}</td>
                  <td className="muted">
                    {row.points
                      .filter((point) => point.total > 0)
                      .map((point) => `${point.label}: ${point.total}`)
                      .join(' · ') || '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    );
  }

  if (kind === 'Attendance') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Günlük katılım</h2>
        </div>
        {report.attendance.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Tarih</th>
                <th>Gün</th>
                <th>Geldi</th>
                <th>Tamamlanan</th>
                <th>Gelmedi</th>
                <th>İptal</th>
                <th>Ertelendi</th>
                <th>Planlanan</th>
                <th>Toplam</th>
              </tr>
            </thead>
            <tbody>
              {report.attendance.map((row) => (
                <tr key={row.dateText}>
                  <td>{row.dateText}</td>
                  <td>{row.weekdayText}</td>
                  <td>{row.arrived}</td>
                  <td>{row.completed}</td>
                  <td>{row.noShow}</td>
                  <td>{row.cancelled}</td>
                  <td>{row.postponed}</td>
                  <td>{row.scheduled}</td>
                  <td>{row.total}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    );
  }

  if (kind === 'HostPerformance') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Görüşmeci performansı</h2>
        </div>
        {report.hostPerformance.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Görüşmeci</th>
                <th>Toplam</th>
                <th>Tamamlanan</th>
                <th>Gelmedi</th>
                <th>İptal</th>
                <th>Tamamlanma</th>
                <th>Ortalama</th>
              </tr>
            </thead>
            <tbody>
              {report.hostPerformance.map((row) => (
                <tr key={row.hostName}>
                  <td>{row.hostName}</td>
                  <td>{row.total}</td>
                  <td>{row.completed}</td>
                  <td>{row.noShow}</td>
                  <td>{row.cancelled}</td>
                  <td>{row.completionRateText}</td>
                  <td>{row.averageDurationText}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    );
  }

  if (kind === 'Repeat') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Tekrar görüşen kişiler</h2>
        </div>
        {report.repeatPersons.length === 0 ? (
          <div className="muted">Bu aralıkta tekrar görüşme yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Kişi</th>
                <th>Telefon</th>
                <th>Görüşme</th>
                <th>Son görüşme</th>
                <th>Aralık</th>
              </tr>
            </thead>
            <tbody>
              {report.repeatPersons.map((row) => (
                <tr key={`${row.personName}-${row.phone}`}>
                  <td>{row.personName}</td>
                  <td>{row.phone}</td>
                  <td>{row.visitCount}</td>
                  <td>{row.lastVisitText}</td>
                  <td>{row.intervalText}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    );
  }

  if (kind === 'PersonHistory') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Kişi geçmişi</h2>
        </div>
        {report.personHistory.length === 0 ? (
          <div className="muted">Eşleşen kişi yok.</div>
        ) : (
          report.personHistory.map((person) => (
            <div key={`${person.personName}-${person.phone}`} className="card" style={{ boxShadow: 'none' }}>
              <div className="card-header">
                <h2>{person.personName}</h2>
                <span className="muted">{person.phone}</span>
                <div className="spacer" />
                <span className="muted">{person.summaryText}</span>
              </div>
              <table className="table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Görüşmeci</th>
                    <th>Kategori</th>
                    <th>Durum</th>
                  </tr>
                </thead>
                <tbody>
                  {person.entries.map((entry, index) => (
                    <tr key={index}>
                      <td>{entry.dateText}</td>
                      <td>{entry.hostName}</td>
                      <td>{entry.categoryName}</td>
                      <td>{entry.statusText}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))
        )}
      </div>
    );
  }

  if (kind === 'AuditActivity') {
    return (
      <div className="card">
        <div className="card-header">
          <h2>Denetim / aktivite</h2>
          {report.auditActivity ? <span className="muted">{report.auditActivity.total} kayıt</span> : null}
        </div>
        {!report.auditActivity ? (
          <div className="muted">Bu rapor yönetici erişimi gerektirir.</div>
        ) : (
          <>
            <div className="kpi-grid" style={{ marginBottom: 16 }}>
              {report.auditActivity.actions.map((action) => (
                <Kpi key={action.actionName} value={action.count} label={action.actionName} />
              ))}
            </div>
            <table className="table">
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th>Kullanıcı</th>
                  <th>İşlem</th>
                  <th>Nesne</th>
                </tr>
              </thead>
              <tbody>
                {report.auditActivity.entries.map((entry, index) => (
                  <tr key={index}>
                    <td>{entry.createdAtText}</td>
                    <td>{entry.userName}</td>
                    <td>{entry.actionName}</td>
                    <td>{entry.entityText}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>
    );
  }

  return (
    <>
      <div className="card">
        <div className="card-header">
          <h2>Kategori dağılımı</h2>
        </div>
        {summary.categories.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Kategori</th>
                <th>Toplam</th>
              </tr>
            </thead>
            <tbody>
              {summary.categories.map((row) => (
                <tr key={row.categoryId}>
                  <td>
                    <span className="badge" style={{ background: row.colorHex, color: '#fff' }}>
                      {row.categoryName}
                    </span>
                  </td>
                  <td>{row.total}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Görüşmeci dağılımı</h2>
        </div>
        {summary.hosts.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Görüşmeci</th>
                <th>Toplam</th>
                <th>Tamamlanan</th>
                <th>İptal</th>
              </tr>
            </thead>
            <tbody>
              {summary.hosts.map((row) => (
                <tr key={row.hostUserId}>
                  <td>{row.hostName}</td>
                  <td>{row.total}</td>
                  <td>{row.completed}</td>
                  <td>{row.cancelled}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="card">
        <div className="card-header">
          <h2>Saat dağılımı</h2>
        </div>
        {summary.hours.length === 0 ? (
          <div className="muted">Veri yok.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Saat</th>
                <th>Toplam</th>
              </tr>
            </thead>
            <tbody>
              {summary.hours.map((row) => (
                <tr key={row.hour}>
                  <td>{row.label}</td>
                  <td>{row.total}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
