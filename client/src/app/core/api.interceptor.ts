import { HttpInterceptorFn } from '@angular/common/http';

import { environment } from '../../environments/environment';
import { readXsrfToken } from './xsrf';

/**
 * Dedicated credentials + XSRF interceptor for requests built against `environment.apiBaseUrl`.
 *
 * **The trap this exists to catch**: Angular's own XSRF interceptor (`xsrfInterceptorFn` in
 * `@angular/common/http`) compares the request's origin against the *page's* origin
 * (`new URL(req.url, locationOrigin).origin !== locationOrigin`) and does nothing when they
 * differ — verified against the installed `@angular/common` v22.0.7 source, not assumed. In
 * development, `environment.apiBaseUrl` is the absolute `http://localhost:5092` while the
 * Angular dev server serves the page from `http://localhost:4200`: those origins differ, so
 * Angular's built-in interceptor silently attaches neither credentials nor `X-XSRF-TOKEN` to any
 * request built from `apiBaseUrl`. Once antiforgery is enforced on `/api/documents` (PR5), the
 * dev upload would fail validation in a way that reads as a server bug.
 *
 * In production, `apiBaseUrl` is `''`, every request resolves to a same-origin relative URL, and
 * Angular's built-in interceptor already does the right thing — so this interceptor no-ops there
 * rather than duplicating it.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (environment.apiBaseUrl === '' || !req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  let outgoing = req.clone({ withCredentials: true });

  if (outgoing.method !== 'GET' && outgoing.method !== 'HEAD' && !outgoing.headers.has('X-XSRF-TOKEN')) {
    const token = readXsrfToken();
    if (token != null) {
      outgoing = outgoing.clone({ headers: outgoing.headers.set('X-XSRF-TOKEN', token) });
    }
  }

  return next(outgoing);
};
