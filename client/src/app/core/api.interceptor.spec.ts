import { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { of } from 'rxjs';

import { environment } from '../../environments/environment';
import { apiInterceptor } from './api.interceptor';

function capturingNext(): { next: HttpHandlerFn; captured: () => HttpRequest<unknown> | undefined } {
  let captured: HttpRequest<unknown> | undefined;
  const next: HttpHandlerFn = (req) => {
    captured = req;
    return of({} as HttpEvent<unknown>);
  };
  return { next, captured: () => captured };
}

describe('apiInterceptor', () => {
  const originalApiBaseUrl = environment.apiBaseUrl;

  afterEach(() => {
    environment.apiBaseUrl = originalApiBaseUrl;
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  });

  // THE regression guard: this is the exact failure ADR-J's trap describes. Angular's own XSRF
  // interceptor no-ops for a cross-origin ABSOLUTE URL, and dev's apiBaseUrl is exactly that — a
  // test that only covered a relative URL would prove nothing about this failure mode.
  it('attaches X-XSRF-TOKEN and withCredentials to a request against an ABSOLUTE dev API URL', () => {
    environment.apiBaseUrl = 'http://localhost:5092';
    document.cookie = 'XSRF-TOKEN=abc123';

    const req = new HttpRequest('POST', 'http://localhost:5092/api/documents', {});
    const { next, captured } = capturingNext();

    apiInterceptor(req, next);

    expect(captured()?.withCredentials).toBe(true);
    expect(captured()?.headers.get('X-XSRF-TOKEN')).toBe('abc123');
  });

  it('attaches withCredentials but not the header for a GET request', () => {
    environment.apiBaseUrl = 'http://localhost:5092';
    document.cookie = 'XSRF-TOKEN=abc123';

    const req = new HttpRequest('GET', 'http://localhost:5092/api/documents');
    const { next, captured } = capturingNext();

    apiInterceptor(req, next);

    expect(captured()?.withCredentials).toBe(true);
    expect(captured()?.headers.has('X-XSRF-TOKEN')).toBe(false);
  });

  it('no-ops when no XSRF-TOKEN cookie exists yet', () => {
    environment.apiBaseUrl = 'http://localhost:5092';

    const req = new HttpRequest('POST', 'http://localhost:5092/api/documents', {});
    const { next, captured } = capturingNext();

    apiInterceptor(req, next);

    expect(captured()?.withCredentials).toBe(true);
    expect(captured()?.headers.has('X-XSRF-TOKEN')).toBe(false);
  });

  it('no-ops entirely in production, where apiBaseUrl is empty and URLs are same-origin', () => {
    environment.apiBaseUrl = '';
    document.cookie = 'XSRF-TOKEN=abc123';

    const req = new HttpRequest('POST', '/api/documents', {});
    const { next, captured } = capturingNext();

    apiInterceptor(req, next);

    expect(captured()).toBe(req);
    expect(captured()?.withCredentials).toBe(false);
  });
});
