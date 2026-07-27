/**
 * Development environment. Swapped in for `environment.ts` by the `development` build
 * configuration's `fileReplacements` (see angular.json) — i.e. whenever `ng serve` / `pnpm start`
 * runs, or `pnpm test` builds against the development configuration.
 *
 * The API runs locally via `dotnet run --project backend/src/DocuMind.Api --launch-profile http`
 * on port 5092 (see README "Getting started"), separate from the Angular dev server on 4200, so
 * requests need an absolute base URL here — unlike production, there is no shared origin or
 * reverse proxy in front of the two during local development.
 */
export const environment = {
  apiBaseUrl: 'http://localhost:5092'
};
