import { Component, signal } from '@angular/core';

import { ChatService } from '../../core/chat.service';

/**
 * Single-turn chat UI: ask a question, watch the cited answer stream in. No conversation
 * history is kept or sent — each question is independent, matching the backend's
 * `ChatRequest(string Question)` contract. Deliberately minimal/throwaway (see
 * portfolio/documind-ui-future) — a dedicated design slice follows.
 */
@Component({
  selector: 'app-chat',
  templateUrl: './chat.html'
})
export class Chat {
  readonly question = signal('');
  readonly errorMessage = signal<string | null>(null);

  constructor(readonly chatService: ChatService) {}

  onQuestionInput(event: Event): void {
    this.question.set((event.target as HTMLInputElement).value);
  }

  async ask(): Promise<void> {
    const question = this.question().trim();
    if (!question || this.chatService.isStreaming()) {
      return;
    }

    this.question.set('');
    this.errorMessage.set(null);

    try {
      await this.chatService.ask(question);
    } catch {
      this.errorMessage.set('Failed to get an answer. Is the API running?');
    }
    // No `finally` block touching busy state here, unlike Upload: `chatService.isStreaming`
    // (and the in-flight assistant message's own `streaming` flag) are owned by the service, not
    // this component, and `ChatService.ask()` already clears both in its own `finally` — which
    // runs on every exit path, including the one that produced the error caught above. Setting
    // them again from here would reach into state this component doesn't own for no benefit.
  }
}
