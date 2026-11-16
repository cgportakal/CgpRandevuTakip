'use client';

import { useState } from 'react';
import { api, ApiError, API_URL } from '@/lib/api';
import type { AuthTokens } from '@/lib/types';

type Props = {
  onLogin: (tokens: AuthTokens) => void;
};

export function LoginView({ onLogin }: Props) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);

    if (!username.trim() || !password) {
      setError('Kullanıcı adı ve şifre gerekli.');
      return;
    }

    setBusy(true);
    try {
      const tokens = await api.login(username.trim(), password);
      onLogin(tokens);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Giriş yapılamadı.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={submit}>
        <div className="login-brand">CGP</div>
        <h1>Randevu Takip</h1>
        <p>
          Kurum hesabınızla giriş yapın. İlk kurulum masaüstü uygulamasında veya API üzerinden
          yapılır; buradan sonra tüm cihazlar aynı veriyi görür.
        </p>

        <div className="field" style={{ marginBottom: 12 }}>
          <label htmlFor="username">Kullanıcı adı</label>
          <input
            id="username"
            className="input"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            autoComplete="username"
            autoFocus
          />
        </div>

        <div className="field">
          <label htmlFor="password">Şifre</label>
          <input
            id="password"
            className="input"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
          />
        </div>

        {error ? <div className="alert error">{error}</div> : null}

        <button className="btn btn-primary" style={{ width: '100%', marginTop: 16 }} disabled={busy}>
          {busy ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </button>

        <div className="login-footer">Sunucu: {API_URL}</div>
      </form>
    </div>
  );
}
