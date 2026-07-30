import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    );
  }

  it('allows navigation once bootstrap resolves with an authenticated user', async () => {
    const authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'ensureBootstrapped').mockResolvedValue(undefined);
    authService.user.set({ id: 'u1', email: 'demo@example.com' });

    const result = await runGuard();

    expect(result).toBe(true);
  });

  it('redirects to /login when bootstrap resolves with no user', async () => {
    const authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'ensureBootstrapped').mockResolvedValue(undefined);

    const result = await runGuard();

    expect(result).toBeInstanceOf(UrlTree);
  });
});
