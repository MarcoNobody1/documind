/**
 * Production environment. Used by the default (`production`) build configuration.
 *
 * `apiBaseUrl: ''` is deliberate, not a placeholder left empty by mistake: an empty base means
 * every request in `ChatService` resolves against the page's own origin (`fetch('/api/chat')`
 * rather than `fetch('http://something/api/chat')`). That is the correct default once the API is
 * served from the same origin as the client, or from a different origin reached through a
 * reverse proxy that forwards `/api/*` — the common production topology for this kind of app.
 * There is no separate "real" production URL to fill in here; if a future deployment serves the
 * API from a genuinely different origin with no proxy in front, set this to that absolute URL.
 */
export const environment = {
  apiBaseUrl: ''
};
