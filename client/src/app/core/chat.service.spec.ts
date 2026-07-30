import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { ChatService, parseSseStream } from './chat.service';

/** Builds a `ReadableStream<Uint8Array>` from a plain string, chunked to simulate network arrival. */
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

describe('parseSseStream', () => {
  it('parses default (token) events with no event: line', async () => {
    const sse = 'data: The\n\ndata:  answer\n\n';

    const events = [];
    for await (const event of parseSseStream(streamFrom(sse))) {
      events.push(event);
    }

    expect(events).toEqual([
      { type: undefined, data: 'The' },
      { type: undefined, data: ' answer' }
    ]);
  });

  it('parses a named event (citations) with a JSON payload', async () => {
    const sse = 'event: citations\ndata: [{"documentName":"handbook.pdf","pageNumber":3}]\n\n';

    const events = [];
    for await (const event of parseSseStream(streamFrom(sse))) {
      events.push(event);
    }

    expect(events).toEqual([
      { type: 'citations', data: '[{"documentName":"handbook.pdf","pageNumber":3}]' }
    ]);
  });

  it('joins multiple data: lines within one event with a newline', async () => {
    const sse = 'data: line one\ndata: line two\n\n';

    const events = [];
    for await (const event of parseSseStream(streamFrom(sse))) {
      events.push(event);
    }

    expect(events).toEqual([{ type: undefined, data: 'line one\nline two' }]);
  });

  it('handles a chunk boundary that splits an event across reads', async () => {
    // chunkSize = 3 forces the "data:" prefix itself to be split across two reads.
    const sse = 'data: split-boundary\n\n';

    const events = [];
    for await (const event of parseSseStream(streamFrom(sse, 3))) {
      events.push(event);
    }

    expect(events).toEqual([{ type: undefined, data: 'split-boundary' }]);
  });
});

describe('ChatService.ask', () => {
  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
  });

  it('accumulates streamed tokens into the assistant message and attaches citations at the end', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    const sse =
      'data: The\n\ndata:  answer.\n\nevent: citations\ndata: [{"documentName":"handbook.pdf","pageNumber":3}]\n\n';

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(streamFrom(sse), {
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' }
        })
      )
    );

    await service.ask('How many vacation days do I get?');

    const messages = service.messages();
    expect(messages).toHaveLength(2);
    expect(messages[0]).toEqual({ role: 'user', text: 'How many vacation days do I get?' });
    expect(messages[1].role).toBe('assistant');
    expect(messages[1].text).toBe('The answer.');
    expect(messages[1].streaming).toBe(false);
    expect(messages[1].citations).toEqual([{ documentName: 'handbook.pdf', pageNumber: 3 }]);
  });

  it('sends credentials and an X-XSRF-TOKEN header read from the cookie', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    document.cookie = 'XSRF-TOKEN=tok123';

    const fetchMock = vi.fn().mockResolvedValue(
      new Response(streamFrom('data: hi\n\n'), {
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' }
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await service.ask('Hi');

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(options.credentials).toBe('include');
    expect((options.headers as Record<string, string>)['X-XSRF-TOKEN']).toBe('tok123');
  });

  it('omits the X-XSRF-TOKEN header entirely when no token cookie exists yet', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);

    const fetchMock = vi.fn().mockResolvedValue(
      new Response(streamFrom('data: hi\n\n'), {
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' }
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    await service.ask('Hi');

    const [, options] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect('X-XSRF-TOKEN' in (options.headers as Record<string, string>)).toBe(false);
  });

  it('on a 401 to the initial request, surfaces a visible message, redirects to /login, and does not throw', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideRouter([])] });
    const service = TestBed.inject(ChatService);
    const authService = TestBed.inject(AuthService);
    const redirectSpy = vi.spyOn(authService, 'redirectToLogin').mockImplementation(() => {});

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));

    await expect(service.ask('Anything?')).resolves.toBeUndefined();

    const messages = service.messages();
    expect(messages[1].text).toContain('session has expired');
    expect(messages[1].streaming).toBe(false);
    expect(redirectSpy).toHaveBeenCalledOnce();
    expect(service.isStreaming()).toBe(false);
  });
});
