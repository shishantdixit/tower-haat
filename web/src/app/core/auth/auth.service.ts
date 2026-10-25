import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthResponse, Me, OtpRequested, SaveProfileRequest } from '../api/models';

/**
 * The session: a short-lived access token kept in memory, renewed through the
 * HTTP-only refresh cookie that the API sets (scripts never see it).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<AuthResponse | null>(null);
  private refreshing: Promise<boolean> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly signedIn = computed(() => this.session() !== null);
  readonly hasProfile = computed(() => this.user()?.profile != null);
  readonly isPlatformAdmin = computed(() => this.user()?.isPlatformAdmin ?? false);

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** A token valid for at least another minute, refreshing first if needed (for long-lived connections). */
  async freshAccessToken(): Promise<string | null> {
    const session = this.session();
    if (session && Date.parse(session.accessTokenExpiresAt) - Date.now() < 60_000) {
      await this.refresh();
    }
    return this.accessToken;
  }

  requestOtp(phone: string): Promise<OtpRequested> {
    return firstValueFrom(this.http.post<OtpRequested>('/api/auth/otp', { phone }));
  }

  async verifyOtp(phone: string, code: string): Promise<Me> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>('/api/auth/verify', { phone, code }),
    );
    this.session.set(response);
    return response.user;
  }

  async saveProfile(request: SaveProfileRequest): Promise<Me> {
    const response = await firstValueFrom(this.http.put<AuthResponse>('/api/me/profile', request));
    this.session.set(response);
    return response.user;
  }

  /** Renews the access token from the refresh cookie. Concurrent callers share one request. */
  refresh(): Promise<boolean> {
    this.refreshing ??= firstValueFrom(this.http.post<AuthResponse>('/api/auth/refresh', null))
      .then((response) => {
        this.session.set(response);
        return true;
      })
      .catch((error: unknown) => {
        // Only a refused refresh ends the session; a network blip or rate limit leaves it alone.
        if (error instanceof HttpErrorResponse && error.status === 401) {
          this.session.set(null);
        }
        return false;
      })
      .finally(() => (this.refreshing = null));
    return this.refreshing;
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', null));
    } finally {
      this.session.set(null);
    }
  }
}
