/**
 * Reads the `XSRF-TOKEN` cookie value, shared by `api.interceptor.ts` (for `HttpClient`
 * requests) and `ChatService.ask()` (which uses raw `fetch`, bypassing every Angular
 * interceptor — so it must read and attach the header itself).
 *
 * `decodeURIComponent`s the value: `AccountEndpoints.IssueAntiforgeryCookie` writes it via
 * `Response.Cookies.Append`, which URL-encodes it, and Angular's own `HttpXsrfCookieExtractor`
 * decodes on read. Matching that behaviour keeps both paths byte-identical to what the server
 * actually issued — a naive raw read would mismatch on any `+`/`=` in the token.
 */
export function readXsrfToken(cookieName = 'XSRF-TOKEN'): string | null {
  if (typeof document === 'undefined' || !document.cookie) {
    return null;
  }

  for (const cookie of document.cookie.split('; ')) {
    const separatorIndex = cookie.indexOf('=');
    if (separatorIndex === -1) {
      continue;
    }

    if (cookie.slice(0, separatorIndex) === cookieName) {
      return decodeURIComponent(cookie.slice(separatorIndex + 1));
    }
  }

  return null;
}
