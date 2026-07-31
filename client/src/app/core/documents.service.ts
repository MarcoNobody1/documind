import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { DocumentListItem } from './models';

/**
 * Lists the caller's own documents (ADR-P). A new service, not a `ChatService` method: document
 * listing is not chat, and widening `ChatService` widens the highest-regression-risk file in the
 * client for no benefit. Uses `HttpClient`, not raw `fetch` (unlike `ChatService.ask()`): the
 * response is not streamed, so there is no reason to bypass `apiInterceptor` — the GET gets
 * `withCredentials` + `X-XSRF-TOKEN` handling for free, the opposite of `ask()`'s situation.
 */
@Injectable({ providedIn: 'root' })
export class DocumentsService {
  readonly documents = signal<DocumentListItem[]>([]);
  readonly isLoading = signal(false);
  readonly loadFailed = signal(false);

  constructor(private readonly http: HttpClient) {}

  async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    try {
      const documents = await firstValueFrom(
        this.http.get<DocumentListItem[]>(`${environment.apiBaseUrl}/api/documents`)
      );
      this.documents.set(documents);
    } catch {
      this.loadFailed.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }
}
