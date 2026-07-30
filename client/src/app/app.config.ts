import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { apiInterceptor } from './core/api.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // withFetch(): the chat SSE stream is consumed via fetch + ReadableStream in
    // ChatService, and HttpClient's default XHR backend cannot read a streaming
    // response body incrementally. withFetch() switches HttpClient itself to the
    // Fetch API too, so both the document upload and the chat request go through
    // the same underlying transport.
    //
    // withXsrfConfiguration: stated explicitly rather than left to Angular's defaults, so both
    // sides of the antiforgery contract name the same cookie/header on purpose
    // (AntiforgeryOptions.HeaderName on the API is also `X-XSRF-TOKEN`).
    //
    // withInterceptors([apiInterceptor]): Angular's own XSRF interceptor no-ops for the
    // cross-origin absolute URLs used in development — see api.interceptor.ts for why.
    provideHttpClient(
      withFetch(),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
      withInterceptors([apiInterceptor])
    )
  ]
};
