import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../environments/environment';
import { ChatMessage, Citation, UploadDocumentResponse } from './models';

/**
 * Uploads PDFs and asks questions against the shared document store, and holds the
 * (single-turn, no-history) conversation as an Angular signal so components can render it
 * reactively as tokens stream in.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  readonly messages = signal<ChatMessage[]>([]);
  readonly isStreaming = signal(false);

  constructor(private readonly http: HttpClient) {}

  async uploadDocument(file: File): Promise<UploadDocumentResponse> {
    const formData = new FormData();
    formData.append('file', file);

    return firstValueFrom(
      this.http.post<UploadDocumentResponse>(`${environment.apiBaseUrl}/api/documents`, formData)
    );
  }

  /**
   * Asks a question and streams the answer via Server-Sent Events. Uses `fetch` +
   * `ReadableStream` directly (not `HttpClient`) because the SSE response body must be read
   * incrementally as it arrives, token by token, rather than awaited as a single completed
   * response.
   */
  async ask(question: string): Promise<void> {
    this.messages.update((messages) => [...messages, { role: 'user', text: question }]);

    const assistantIndex = this.messages().length;
    this.messages.update((messages) => [
      ...messages,
      { role: 'assistant', text: '', streaming: true }
    ]);

    this.isStreaming.set(true);

    try {
      const response = await fetch(`${environment.apiBaseUrl}/api/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question })
      });

      if (!response.ok || !response.body) {
        throw new Error(`Chat request failed with status ${response.status}`);
      }

      for await (const event of parseSseStream(response.body)) {
        if (event.type === 'citations') {
          const citations = JSON.parse(event.data) as RawCitation[];
          this.patchAssistantMessage(assistantIndex, (message) => ({
            ...message,
            citations: citations.map(toCitation)
          }));
        } else {
          this.patchAssistantMessage(assistantIndex, (message) => ({
            ...message,
            text: message.text + event.data
          }));
        }
      }
    } finally {
      this.patchAssistantMessage(assistantIndex, (message) => ({ ...message, streaming: false }));
      this.isStreaming.set(false);
    }
  }

  private patchAssistantMessage(index: number, patch: (message: ChatMessage) => ChatMessage): void {
    this.messages.update((messages) =>
      messages.map((message, i) => (i === index ? patch(message) : message))
    );
  }
}

interface RawCitation {
  documentName: string;
  pageNumber: number;
}

function toCitation(raw: RawCitation): Citation {
  return { documentName: raw.documentName, pageNumber: raw.pageNumber };
}

/** One parsed Server-Sent Event: `type` is `undefined` for the default (unnamed) event. */
export interface SseEvent {
  type?: string;
  data: string;
}

/**
 * Parses a `text/event-stream` body into discrete events, per the SSE spec: events are
 * separated by a blank line, `event:` sets the event type, and `data:` lines (possibly several
 * per event) are joined with `\n`. Lines with no recognized field prefix are ignored.
 */
export async function* parseSseStream(body: ReadableStream<Uint8Array>): AsyncGenerator<SseEvent> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });

      let separatorIndex: number;
      while ((separatorIndex = buffer.indexOf('\n\n')) !== -1) {
        const rawEvent = buffer.slice(0, separatorIndex);
        buffer = buffer.slice(separatorIndex + 2);

        const parsed = parseSseEventBlock(rawEvent);
        if (parsed) {
          yield parsed;
        }
      }
    }
    // Deliberately no residual-buffer flush here (sdd-verify SUGGESTION 2): on a normal
    // completion ASP.NET Core's SSE writer always emits the trailing blank line, so `buffer` is
    // always empty at this point. A non-empty leftover only happens on a mid-stream disconnect,
    // where the answer is already broken — flushing a partial token adds nothing, and flushing a
    // truncated `citations` payload would throw on `JSON.parse`. Fixing this is more likely to
    // introduce a new fault (parsing garbage) than to prevent one.
  } finally {
    reader.releaseLock();
  }
}

function parseSseEventBlock(rawEvent: string): SseEvent | null {
  let type: string | undefined;
  const dataLines: string[] = [];

  for (const line of rawEvent.split('\n')) {
    if (line.startsWith('event:')) {
      type = line.slice('event:'.length).trim();
    } else if (line.startsWith('data:')) {
      dataLines.push(line.slice('data:'.length).replace(/^ /, ''));
    }
  }

  if (dataLines.length === 0) {
    return null;
  }

  return { type, data: dataLines.join('\n') };
}
