import type {
  AppointmentDetail,
  AppointmentInput,
  AppointmentListItem,
  AppointmentOutcomeInput,
  AuthTokens,
  CategoryInput,
  ContentAdminData,
  ContentManifest,
  ContentSettingsInput,
  ContentVersion,
  HostCreateInput,
  HostUpdateInput,
  ReportKind,
  ReportResult,
  ReportSummary,
  SeriesCancelResult,
  SeriesCreateResult,
  SeriesFrequency,
  SeriesPlan,
  VersionPolicy,
} from './types';

export const API_URL =
  process.env.NEXT_PUBLIC_API_URL ?? 'http://127.0.0.1:5199';

const SESSION_KEY = 'cgp.web.session.v1';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly payload?: unknown,
  ) {
    super(message);
  }
}

export type AppointmentOutcome = {
  attendance: 'Arrived' | 'NoShow' | 'Postponed' | 'Cancelled';
  summary: string | null;
  willRepeat: boolean;
  postNotes: string | null;
  isClosed: boolean;
  closedAtLocal: string | null;
  closedByName: string | null;
  updatedAtLocal: string;
};

class ApiClient {
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private refreshing: Promise<boolean> | null = null;

  setTokens(accessToken: string | null, refreshToken: string | null) {
    this.accessToken = accessToken;
    this.refreshToken = refreshToken;

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('cgp:tokens', { detail: accessToken }));
    }
  }

  loadSession(): AuthTokens | null {
    if (typeof window === 'undefined') return null;

    try {
      const raw = window.localStorage.getItem(SESSION_KEY);
      if (!raw) return null;

      const tokens = JSON.parse(raw) as AuthTokens;
      this.setTokens(tokens.accessToken, tokens.refreshToken);
      return tokens;
    } catch {
      return null;
    }
  }

  saveSession(tokens: AuthTokens) {
    this.setTokens(tokens.accessToken, tokens.refreshToken);
    window.localStorage.setItem(SESSION_KEY, JSON.stringify(tokens));
  }

  clearSession() {
    this.setTokens(null, null);
    window.localStorage.removeItem(SESSION_KEY);
  }

  async login(username: string, password: string): Promise<AuthTokens> {
    const tokens = await this.request<AuthTokens>(
      '/v1/auth/login',
      {
        method: 'POST',
        body: JSON.stringify({ username, password, deviceName: 'web' }),
      },
      false,
    );

    this.saveSession(tokens);
    return tokens;
  }

  async logout(): Promise<void> {
    const token = this.refreshToken;
    this.clearSession();

    if (!token) return;

    try {
      await this.request('/v1/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: token }),
      }, false);
    } catch {
      // Yerel oturum yine kapanır.
    }
  }

  getDay(dateKey: string): Promise<AppointmentListItem[]> {
    return this.request(`/v1/appointments/day?date=${dateKey}`);
  }

  search(text: string): Promise<AppointmentListItem[]> {
    return this.request(`/v1/appointments/search?search=${encodeURIComponent(text)}`);
  }

  getManifest(): Promise<ContentManifest> {
    return this.request('/v1/content/manifest');
  }

  getVersionPolicy(): Promise<VersionPolicy> {
    return this.request('/v1/version-policy', {}, false);
  }

  getPushPublicKey(): Promise<{ publicKey: string; subject: string }> {
    return this.request('/v1/push/public-key');
  }

  registerDevice(request: {
    platform: 'web' | 'android' | 'ios';
    token?: string;
    endpoint?: string;
    p256dh?: string;
    auth?: string;
    appVersion?: string;
  }): Promise<{ id: string; platform: string }> {
    return this.request('/v1/devices', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  unregisterDevice(endpoint: string): Promise<void> {
    return this.request(`/v1/devices?endpoint=${encodeURIComponent(endpoint)}`, {
      method: 'DELETE',
    });
  }

  sendTestPush(): Promise<{ sent: number; deviceCount: number }> {
    return this.request('/v1/push/test', { method: 'POST' });
  }

  getDetail(id: string): Promise<AppointmentDetail> {
    return this.request(`/v1/appointments/${id}`);
  }

  getOutcome(id: string): Promise<AppointmentOutcome | null> {
    return this.request<AppointmentOutcome | null>(`/v1/appointments/${id}/outcome`).catch((error) => {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    });
  }

  createAppointment(input: AppointmentInput): Promise<{ success: boolean; appointmentId: string | null; error: string | null }> {
    return this.request('/v1/appointments', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  updateAppointment(id: string, input: AppointmentInput): Promise<void> {
    return this.request(`/v1/appointments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  deleteAppointment(id: string): Promise<void> {
    return this.request(`/v1/appointments/${id}`, { method: 'DELETE' });
  }

  saveOutcome(id: string, input: AppointmentOutcomeInput): Promise<void> {
    return this.request(`/v1/appointments/${id}/outcome`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  previewSeries(input: AppointmentInput, frequency: SeriesFrequency, count: number): Promise<SeriesPlan> {
    return this.request('/v1/appointments/series/preview', {
      method: 'POST',
      body: JSON.stringify({ input, frequency, count }),
    });
  }

  createSeries(input: AppointmentInput, frequency: SeriesFrequency, count: number): Promise<SeriesCreateResult> {
    return this.request('/v1/appointments/series', {
      method: 'POST',
      body: JSON.stringify({ input, frequency, count }),
    });
  }

  cancelSeries(seriesId: string): Promise<SeriesCancelResult> {
    return this.request(`/v1/appointments/series/${seriesId}/cancel`, { method: 'POST' });
  }

  getReportSummary(from: string, to: string): Promise<ReportSummary> {
    return this.request(`/v1/reports/summary?from=${from}&to=${to}`);
  }

  getReport(kind: ReportKind, from: string, to: string, personSearch?: string): Promise<ReportResult> {
    const search = personSearch ? `&personSearch=${encodeURIComponent(personSearch)}` : '';
    return this.request(`/v1/reports/kind/${kind}?from=${from}&to=${to}${search}`);
  }

  getContentAdmin(): Promise<ContentAdminData> {
    return this.request('/v1/admin/content');
  }

  saveContentSettings(input: ContentSettingsInput): Promise<void> {
    return this.request('/v1/admin/content/settings', {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  uploadContentLogo(file: File): Promise<{ logoUrl: string }> {
    const form = new FormData();
    form.append('file', file);
    return this.request('/v1/admin/content/logo', {
      method: 'POST',
      body: form,
    });
  }

  createCategory(input: CategoryInput): Promise<void> {
    return this.request('/v1/admin/content/categories', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  updateCategory(id: string, input: CategoryInput): Promise<void> {
    return this.request(`/v1/admin/content/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  setCategoryActive(id: string, isActive: boolean): Promise<void> {
    return this.request(`/v1/admin/content/categories/${id}/active`, {
      method: 'POST',
      body: JSON.stringify({ isActive }),
    });
  }

  moveCategory(id: string, direction: -1 | 1): Promise<void> {
    return this.request(`/v1/admin/content/categories/${id}/move`, {
      method: 'POST',
      body: JSON.stringify({ direction }),
    });
  }

  deleteCategory(id: string): Promise<void> {
    return this.request(`/v1/admin/content/categories/${id}`, { method: 'DELETE' });
  }

  createHost(input: HostCreateInput): Promise<void> {
    return this.request('/v1/admin/content/hosts', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  updateHost(id: string, input: HostUpdateInput): Promise<void> {
    return this.request(`/v1/admin/content/hosts/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  publishContent(note?: string): Promise<ContentVersion> {
    return this.request('/v1/admin/content/publish', {
      method: 'POST',
      body: JSON.stringify({ note: note?.trim() || null }),
    });
  }

  restoreContentVersion(id: string): Promise<ContentVersion> {
    return this.request(`/v1/admin/content/versions/${id}/restore`, { method: 'POST' });
  }

  private async refreshTokens(): Promise<boolean> {
    if (!this.refreshToken) return false;

    if (!this.refreshing) {
      this.refreshing = (async () => {
        try {
          const tokens = await this.request<AuthTokens>(
            '/v1/auth/refresh',
            {
              method: 'POST',
              body: JSON.stringify({ refreshToken: this.refreshToken }),
            },
            false,
          );

          const stored = this.loadSession();
          this.saveSession({ ...tokens, user: stored?.user ?? tokens.user });
          return true;
        } catch {
          this.clearSession();
          return false;
        } finally {
          this.refreshing = null;
        }
      })();
    }

    return this.refreshing;
  }

  private async request<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 15000);
    const isFormData = typeof FormData !== 'undefined' && init.body instanceof FormData;

    try {
      const response = await fetch(`${API_URL}${path}`, {
        ...init,
        signal: controller.signal,
        headers: {
          ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
          ...(this.accessToken ? { Authorization: `Bearer ${this.accessToken}` } : {}),
          ...(init.headers ?? {}),
        },
      });

      if (response.status === 401 && retry && (await this.refreshTokens())) {
        return this.request<T>(path, init, false);
      }

      if (!response.ok) {
        const text = await response.text();
        let payload: unknown = undefined;
        let message = `Sunucu hatası (${response.status})`;

        if (text) {
          try {
            payload = JSON.parse(text);
            const parsed = payload as { error?: string };
            message = parsed.error ?? message;
          } catch {
            message = text;
          }
        }

        throw new ApiError(message, response.status, payload);
      }

      if (response.status === 204) return undefined as T;

      return (await response.json()) as T;
    } catch (error) {
      if (error instanceof ApiError) throw error;
      if (error instanceof Error && error.name === 'AbortError') {
        throw new ApiError('Sunucuya ulaşılamadı (zaman aşımı).', 0);
      }
      throw new ApiError('Sunucuya ulaşılamadı.', 0);
    } finally {
      clearTimeout(timeout);
    }
  }
}

export const api = new ApiClient();
