import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { ChatService } from '../../core/chat.service';
import { Chat } from './chat';

describe('Chat', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Chat],
      providers: [provideHttpClient()]
    });
  });

  it('surfaces an error message and clears the busy state when the API is unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const fixture = TestBed.createComponent(Chat);
    const component = fixture.componentInstance;
    component.question.set('How many vacation days do I get?');

    await component.ask();

    expect(component.errorMessage()).toBe('Failed to get an answer. Is the API running?');
    expect(component.chatService.isStreaming()).toBe(false);
  });

  it('surfaces an error message and clears the busy state on a non-OK response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(null, { status: 500, statusText: 'Internal Server Error' }))
    );

    const fixture = TestBed.createComponent(Chat);
    const component = fixture.componentInstance;
    component.question.set('How many vacation days do I get?');

    await component.ask();

    expect(component.errorMessage()).toBe('Failed to get an answer. Is the API running?');
    expect(component.chatService.isStreaming()).toBe(false);
  });

  it('clears any previous error message on the next successful question', async () => {
    const fixture = TestBed.createComponent(Chat);
    const component = fixture.componentInstance;
    component.errorMessage.set('Failed to get an answer. Is the API running?');

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(new ReadableStream({ start: (c) => c.close() }), {
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' }
        })
      )
    );

    component.question.set('A follow-up question');
    await component.ask();

    expect(component.errorMessage()).toBeNull();
  });

  it('does not surface an error message on the happy path', async () => {
    const chatService = TestBed.inject(ChatService);
    const askSpy = vi.spyOn(chatService, 'ask').mockResolvedValue(undefined);

    const fixture = TestBed.createComponent(Chat);
    const component = fixture.componentInstance;
    component.question.set('How many vacation days do I get?');

    await component.ask();

    expect(askSpy).toHaveBeenCalledWith('How many vacation days do I get?');
    expect(component.errorMessage()).toBeNull();
  });
});
