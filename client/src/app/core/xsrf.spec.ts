import { readXsrfToken } from './xsrf';

describe('readXsrfToken', () => {
  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
    document.cookie = 'other=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  });

  it('returns null when the cookie is absent', () => {
    expect(readXsrfToken()).toBeNull();
  });

  it('decodes the cookie value, matching HttpXsrfCookieExtractor', () => {
    // The server writes it URL-encoded (Response.Cookies.Append); a naive raw read would
    // mismatch on any '+'/'=' in the token.
    document.cookie = 'XSRF-TOKEN=abc%2Bdef%3D';
    expect(readXsrfToken()).toBe('abc+def=');
  });

  it('finds the token among multiple cookies', () => {
    document.cookie = 'other=1';
    document.cookie = 'XSRF-TOKEN=tok';
    expect(readXsrfToken()).toBe('tok');
  });
});
