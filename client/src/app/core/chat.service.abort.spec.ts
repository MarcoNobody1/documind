import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ChatService } from './chat.service';

/**
 * New file, not an addition to `chat.service.spec.ts` — that spec must show a zero-line diff
 * across the whole `ui-redesign` chain (see design ADR-M/N), so cancellation gets its own
 * harness rather than a sibling `describe` there.
 */

/** Builds a `ReadableStream<Uint8Array>` that closes normally once `text` has been delivered. */
function streamFrom(text: string, chunkSize = 7): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  const bytes = encoder.encode(text);
  let offset = 0;

  return new ReadableStream<Uint8Array>({
    pull(controller) {
      if (offset >= bytes.length) {
        controller.close();
        return;
      }
      controller.enqueue(bytes.slice(offset, offset + chunkSize));
      offset += chunkSize;
    }
  });
}

/**
 * Builds a `ReadableStream<Uint8Array>` that delivers one token event immediately, then never
 * closes and never enqueues again — simulating a request still in flight. Errors the stream when
 * `signal` aborts, which is what makes a pending `reader.read()` inside `parseSseStream` reject.
 */
function neverClosingStreamAfter(text: string, signal: AbortSignal): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();

  return new ReadableStream<Uint8Array>({
    start(controller) {
      controller.enqueue(encoder.encode(text));
      signal.addEventListener('abort', () => {
        controller.error(new DOMException('The operation was aborted.', 'AbortError'));
      });
    },
    pull() {
      // Never enqueues further data and never closes — the request stays "in flight" until
      // the abort listener above errors the stream.
    }
  });
}

/** Yields to the macrotask queue so pending microtasks (fetch resolution, stream reads) settle. */
function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function sseResponse(body: ReadableStream<Uint8Array>): Response {
  return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
}

describe('ChatService cancellation', () => {
  it(
    'stopping mid-stream clears isStreaming, keeps the partial text, marks the message ' +
      "stopped, and does not reject ask() — proving the caller's catch never fires",
    async () => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
      const service = TestBed.inject(ChatService);

      const fetchMock = vi.fn().mockImplementation((_url: string, options: RequestInit) =>
        Promise.resolve(
          sseResponse(neverClosingStreamAfter('data: partial\n\n', options.signal as AbortSignal))
        )
      );
      vi.stubGlobal('fetch', fetchMock);

      const askPromise = service.ask('Anything?');
      await flushMicrotasks();

      service.stop();

      // If ask() rethrew on a user-initiated stop, this would reject — which is exactly what
      // would let Chat.ask()'s catch repaint Stop as "Failed to get an answer. Is the API
      // running?" (task 2.4). It must resolve.
      await expect(askPromise).resolves.toBeUndefined();

      expect(service.isStreaming()).toBe(false);
      const messages = service.messages();
      expect(messages[1].streaming).toBe(false);
      expect(messages[1].outcome).toBe('stopped');
      expect(messages[1].text).toBe('partial');
    }
  );

  it('stop() is a no-op once the request has already completed', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(streamFrom('data: hi\n\n'))));

    await service.ask('Completed already?');
    expect(service.messages()[1].outcome).toBe('complete');
    expect(service.messages()[1].text).toBe('hi');

    // Stop after completion must change nothing (spec: "Stop pressed after the stream already
    // completed" is a no-op) — the completed message and its outcome stay exactly as shown.
    service.stop();

    const messages = service.messages();
    expect(messages[1].outcome).toBe('complete');
    expect(messages[1].streaming).toBe(false);
    expect(service.isStreaming()).toBe(false);
  });

  it('a stale controller from a completed request never aborts a later request', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(streamFrom('data: hi\n\n'))));

    await service.ask('First?');
    expect(service.messages()[1].outcome).toBe('complete');

    // The controller field is cleared in ask()'s own finally once the first request settles, so
    // this stop() has nothing to abort — it must not reach into a later request's controller.
    service.stop();

    await service.ask('Second?');

    const messages = service.messages();
    expect(messages).toHaveLength(4);
    expect(messages[3].outcome).toBe('complete');
    expect(messages[3].streaming).toBe(false);
    expect(service.isStreaming()).toBe(false);
  });

  it('a genuine fetch failure still rejects and is marked failed — distinct from a user stop', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network down')));

    await expect(service.ask('Anything?')).rejects.toThrow('network down');

    const messages = service.messages();
    expect(messages[1].outcome).toBe('failed');
    expect(messages[1].streaming).toBe(false);
    expect(service.isStreaming()).toBe(false);
  });
});
