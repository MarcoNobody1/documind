import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideRouter } from '@angular/router';

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
    provideHttpClient(withFetch())
  ]
};
