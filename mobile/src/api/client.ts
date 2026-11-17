import { API_URL } from '../config';
import type {
  AppointmentDetail,
  AppointmentInput,
  AppointmentListItem,
  AppointmentOutcomeInput,
  AuthTokens,
  ContentManifest,
  ReportKind,
  ReportResult,
  ReportSummary,
  SeriesCancelResult,
  SeriesCreateResult,
  SeriesPlan,
  SeriesRequest,
  VersionPolicy,
} from './types';

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

class ApiClient {
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private refreshing: Promise<boolean> | null = null;

  setTokens(accessToken: string | null, refreshToken: string | null) {
    this.accessToken = accessToken;
    this.refreshToken = refreshToken;
  }

  get hasSession(): boolean {
    return Boolean(this.accessToken && this.refreshToken);
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  async login(username: string, password: string, deviceName: string): Promise<AuthTokens> {
    const tokens = await this.request<AuthTokens>(
      '/v1/auth/login',
      {
        method: 'POST',
        body: JSON.stringify({ username, password, deviceName }),
      },
      false,
    );

    this.setTokens(tokens.accessToken, tokens.refreshToken);
    return tokens;
  }

  async logout(): Promise<void> {
    const token = this.refreshToken;
    this.setTokens(null, null);

    if (!token) return;

    try {
      await this.request('/v1/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken: token }),
      }, false);
    } catch {
      // Sunucuya ulaşılamazsa yerel oturum yine kapanır.
    }
  }

  async getDay(dateKey: string): Promise<AppointmentListItem[]> {
    return this.request<AppointmentListItem[]>(`/v1/appointments/day?date=${dateKey}`);
  }

  async getManifest(): Promise<ContentManifest> {
    return this.request<ContentManifest>('/v1/content/manifest');
  }

  async getVersionPolicy(): Promise<VersionPolicy> {
    return this.request<VersionPolicy>('/v1/version-policy', {}, false);
  }

  async registerDevice(request: {
    platform: 'android' | 'ios';
    token: string;
    appVersion?: string;
  }): Promise<void> {
    await this.request('/v1/devices', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async unregisterDeviceToken(token: string): Promise<void> {
    await this.request(`/v1/devices?token=${encodeURIComponent(token)}`, {
      method: 'DELETE',
    });
  }

  async saveOutcome(id: string, input: AppointmentOutcomeInput): Promise<void> {
    await this.request(`/v1/appointments/${id}/outcome`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  async getDetail(id: string): Promise<AppointmentDetail> {
    return this.request<AppointmentDetail>(`/v1/appointments/${id}`);
  }

  async createAppointment(input: AppointmentInput): Promise<{ success: boolean; error: string | null; appointmentId: string | null }> {
    return this.request('/v1/appointments', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  async updateAppointment(id: string, input: AppointmentInput): Promise<void> {
    await this.request(`/v1/appointments/${id}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  }

  async previewSeries(input: AppointmentInput, request: SeriesRequest): Promise<SeriesPlan> {
    return this.request<SeriesPlan>('/v1/appointments/series/preview', {
      method: 'POST',
      body: JSON.stringify({ input, frequency: request.frequency, count: request.count }),
    });
  }

  async createSeries(input: AppointmentInput, request: SeriesRequest): Promise<SeriesCreateResult> {
    return this.request<SeriesCreateResult>('/v1/appointments/series', {
      method: 'POST',
      body: JSON.stringify({ input, frequency: request.frequency, count: request.count }),
    });
  }

  async cancelSeries(seriesId: string): Promise<SeriesCancelResult> {
    return this.request<SeriesCancelResult>(`/v1/appointments/series/${seriesId}/cancel`, {
      method: 'POST',
    });
  }

  async getReportSummary(from: string, to: string): Promise<ReportSummary> {
    return this.request<ReportSummary>(`/v1/reports/summary?from=${from}&to=${to}`);
  }

  async getReport(
    kind: ReportKind,
    from: string,
    to: string,
    personSearch?: string,
  ): Promise<ReportResult> {
    const search = personSearch ? `&personSearch=${encodeURIComponent(personSearch)}` : '';
    return this.request<ReportResult>(`/v1/reports/kind/${kind}?from=${from}&to=${to}${search}`);
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
          this.setTokens(tokens.accessToken, tokens.refreshToken);
          return true;
        } catch {
          this.setTokens(null, null);
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

    try {
      const response = await fetch(`${API_URL}${path}`, {
        ...init,
        signal: controller.signal,
        headers: {
          'Content-Type': 'application/json',
          ...(this.accessToken ? { Authorization: `Bearer ${this.accessToken}` } : {}),
          ...(init.headers ?? {}),
        },
      });

      if (response.status === 401 && retry && (await this.refreshTokens())) {
        return this.request<T>(path, init, false);
      }

      if (!response.ok) {
        const text = await response.text();
        let message = `Sunucu hatası (${response.status})`;

        if (text) {
          try {
            const parsed = JSON.parse(text) as { error?: string };
            message = parsed.error ?? message;
          } catch {
            message = text;
          }
        }

        throw new ApiError(message, response.status);
      }

      if (response.status === 204) {
        return undefined as T;
      }

      return (await response.json()) as T;
    } catch (error) {
      if (error instanceof ApiError) throw error;
      if (error instanceof Error && error.name === 'AbortError') {
        throw new ApiError('Sunucuya ulaşılamadı (zaman aşımı).', 0);
      }
      throw new ApiError('Sunucuya ulaşılamadı. Çevrimdışı önbellek kullanılıyor.', 0);
    } finally {
      clearTimeout(timeout);
    }
  }
}

export const api = new ApiClient();
