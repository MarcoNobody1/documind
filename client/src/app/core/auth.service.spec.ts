import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

describe('AuthService', () => {
  let httpMock: HttpTestingController;
  let service: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('sets the user on a successful bootstrap and dedupes concurrent calls into one request', async () => {
    const first = service.ensureBootstrapped();
    const second = service.ensureBootstrapped();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/account/me`);
    req.flush({ id: 'u1', email: 'demo@example.com' });

    await Promise.all([first, second]);

    expect(service.user()).toEqual({ id: 'u1', email: 'demo@example.com' });
  });

  it('clears the user on a 401 bootstrap response, rather than throwing', async () => {
    const bootstrap = service.ensureBootstrapped();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/account/me`);
    req.flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(bootstrap).resolves.toBeUndefined();
    expect(service.user()).toBeNull();
  });

  it('login sets the user on success', async () => {
    const login = service.login('demo@example.com', 'Sup3rSecret!23');

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/account/login`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'u1', email: 'demo@example.com' });

    await login;

    expect(service.user()).toEqual({ id: 'u1', email: 'demo@example.com' });
  });

  it('logout clears the user', async () => {
    service.user.set({ id: 'u1', email: 'demo@example.com' });

    const logout = service.logout();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/account/logout`).flush(null);

    await logout;

    expect(service.user()).toBeNull();
  });

  it('redirectToLogin clears the user and navigates to /login', () => {
    service.user.set({ id: 'u1', email: 'demo@example.com' });
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    service.redirectToLogin();

    expect(service.user()).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});
