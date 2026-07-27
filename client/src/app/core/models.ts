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

/** A single turn in the (single-turn, no-history) chat UI. */
export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  citations?: Citation[];
  /** True while the assistant's answer is still streaming in. */
  streaming?: boolean;
}
