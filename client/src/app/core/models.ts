/**
 * A document + page a chat answer draws from. Always derived from the backend's
 * retrieved-chunk metadata (see AskQuestionHandler), never from the model's
 * free-text output.
 */
export interface Citation {
  documentName: string;
  pageNumber: number;
}

/** The outcome of a completed (or failed) document upload, as returned by POST /api/documents. */
export interface UploadDocumentResponse {
  documentId: string;
  pageCount: number;
  chunkCount: number;
  warning: string | null;
}

/** Terminal outcome of a streamed assistant turn. Absent while streaming and on every user turn. */
export type MessageOutcome = 'complete' | 'stopped' | 'failed';

/**
 * A document owned by the caller, as listed by `GET /api/documents`. Mirrors the backend's
 * `DocumentListItem(Guid Id, string FileName, int PageCount, DateTime UploadedAtUtc)` exactly —
 * no chunk count, no owner: the endpoint deliberately excludes both (see `documind/ui-redesign-scope`).
 */
export interface DocumentListItem {
  id: string;
  fileName: string;
  pageCount: number;
  /** ISO 8601 — `DateTime` serialises this way. Format with `DatePipe` in the template. */
  uploadedAtUtc: string;
}

/** A single turn in the (single-turn, no-history) chat UI. */
export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  citations?: Citation[];
  /** True while the assistant's answer is still streaming in. */
  streaming?: boolean;
  /**
   * Terminal outcome of an assistant turn: set only via `ChatService`'s `patchAssistantMessage`,
   * never on a user turn and never while `streaming` is true.
   */
  outcome?: MessageOutcome;
}
