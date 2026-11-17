'use client';

type Props = {
  currentVersion: string;
  requiredVersion: string;
  latestVersion?: string | null;
  onRetry: () => void;
};

const releasesUrl = 'https://github.com/cgportakal/CgpRandevuTakip/releases/latest';

export function ForcedUpdateView({
  currentVersion,
  requiredVersion,
  latestVersion,
  onRetry,
}: Props) {
  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-brand">CGP</div>
        <h1>Güncelleme gerekli</h1>
        <p>
          Bu web sürümü artık desteklenmiyor. Devam etmek için sayfayı yenileyin;
          yeni sürüm otomatik olarak yüklenecektir.
        </p>

        <div
          style={{
            display: 'grid',
            gap: 8,
            margin: '18px 0',
            padding: 14,
            border: '1px solid var(--border)',
            borderRadius: 12,
            background: '#fafcfb',
            fontSize: 13,
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <span className="muted">Mevcut sürüm</span>
            <strong>{currentVersion}</strong>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <span className="muted">Gerekli sürüm</span>
            <strong>{requiredVersion}</strong>
          </div>
          {latestVersion ? (
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
              <span className="muted">Son sürüm</span>
              <strong>{latestVersion}</strong>
            </div>
          ) : null}
        </div>

        <button className="btn btn-primary" style={{ width: '100%' }} onClick={onRetry}>
          Sayfayı yenile
        </button>

        <p className="muted" style={{ marginTop: 12, textAlign: 'center' }}>
          Yenileme sonrası sorun devam ederse{' '}
          <a href={releasesUrl} target="_blank" rel="noreferrer">
            sürüm notlarını kontrol edin
          </a>
          .
        </p>
      </div>
    </div>
  );
}
