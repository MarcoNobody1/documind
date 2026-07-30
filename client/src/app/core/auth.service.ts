import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';

/** The authenticated caller's identity, as returned by `POST/GET /api/account/{register,login,me}`. */
export interface AuthUser {
  id: string;
  email: string;
}

/**
 * Client-side auth state. Cookie authentication carries no client-readable claims — the
 * `DocuMind.Auth` cookie is `HttpOnly` by design — so the client cannot know whether it is
 * signed in without asking the server. `ensureBootstrapped()` makes that one `GET
 * /api/account/me` request per app load and caches the in-flight promise, so `authGuard` and any
 * other caller can await it without triggering duplicate requests on concurrent navigations.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly user = signal<AuthUser | null>(null);

  private bootstrapPromise: Promise<void> | null = null;

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {}

  /** Resolves once the current session state is known. Idempotent across repeated calls. */
  ensureBootstrapped(): Promise<void> {
    if (!this.bootstrapPromise) {
      this.bootstrapPromise = this.fetchCurrentUser();
    }
    return this.bootstrapPromise;
  }

  async login(email: string, password: string): Promise<void> {
    const user = await firstValueFrom(
      this.http.post<AuthUser>(`${environment.apiBaseUrl}/api/account/login`, { email, password })
    );
    this.user.set(user);
  }

  async register(email: string, password: string): Promise<void> {
    const user = await firstValueFrom(
      this.http.post<AuthUser>(`${environment.apiBaseUrl}/api/account/register`, { email, password })
    );
    this.user.set(user);
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post(`${environment.apiBaseUrl}/api/account/logout`, {}));
    this.user.set(null);
  }

  /**
   * Used by `authGuard` on a failed check and by `ChatService` on a 401 from its initial (not
   * mid-stream — the HTTP status commits before streaming begins) request.
   */
  redirectToLogin(): void {
    this.user.set(null);
    void this.router.navigateByUrl('/login');
  }

  private async fetchCurrentUser(): Promise<void> {
    try {
      const user = await firstValueFrom(this.http.get<AuthUser>(`${environment.apiBaseUrl}/api/account/me`));
      this.user.set(user);
    } catch {
      this.user.set(null);
    }
  }
}
