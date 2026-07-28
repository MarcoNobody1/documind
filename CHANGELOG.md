# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Entries are written for someone deciding whether a version affects them, so each one says what
changed and why it matters rather than which files moved. Unlike the README, this file is kept
in English only: it is a release record read alongside tags and diffs, not a document that
explains the project to a newcomer.

## [Unreleased]

## [0.1.0] — 2026-07-28

First functional release. The complete "chat over your own documents" path works end to end
locally: upload a PDF, ask a question, receive a streamed answer with the document and page it
came from.

### Added

- **PDF ingestion pipeline.** `POST /api/documents` accepts a PDF, extracts text per page,
  splits it into ~800-token chunks with 15% overlap, embeds each chunk with Azure OpenAI
  `text-embedding-3-small`, and persists them to PostgreSQL with the pgvector extension.
- **Semantic retrieval.** Cosine-distance search across all stored chunks, ordered with the
  `<=>` operator so the HNSW `vector_cosine_ops` index is genuinely used. Any other distance
  function still returns correct results but silently falls back to a sequential scan.
- **Streaming chat with citations.** `POST /api/chat` streams the answer over Server-Sent
  Events and closes with a `citations` event. Citations are assembled from the retrieved
  chunks' own metadata and never parsed out of the model's output, which makes a fabricated
  page number structurally impossible rather than merely unlikely.
- **Angular client** with document upload and a streaming chat view. Deliberately minimal and
  restyleable; a dedicated design pass is planned.
- **Retry policy** on the chat client, with exponential backoff honouring the `Retry-After`
  header. The chat deployment runs on a low tokens-per-minute quota, so rate limiting is
  expected under any real burst and must not surface as a raw error.
- **Configurable retrieval depth** via `Retrieval:TopK` (default 5), tunable without a rebuild.
- **Bilingual documentation** with a CI job that enforces both languages staying in step, in
  both change direction and heading structure.
- **Test fixtures**: three fictional PDFs committed to the repository so PDF extraction is
  exercised against real parser output instead of a hand-built fake.

### Security

- No credential literal in tracked configuration. Azure OpenAI endpoint and key live in
  `dotnet user-secrets` or environment variables; the Compose database credentials live in an
  untracked `.env` created from `.env.example`.
- Upload endpoint validates file extension and caps request body size. Anti-forgery is
  explicitly disabled on it and marked `REVISIT`, because the endpoint is unauthenticated in
  this phase and there is therefore no session for a browser to replay.

### Known limitations

- Not deployed; the application runs locally only.
- The client UI is intentionally unstyled.
- Chat is single-turn: there is no conversation history.
- No authentication. Every uploaded document lives in one shared collection.

[Unreleased]: https://github.com/MarcoNobody1/documind/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/MarcoNobody1/documind/releases/tag/v0.1.0
