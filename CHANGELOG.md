# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Entries are written for someone deciding whether a version affects them, so each one says what
changed and why it matters rather than which files moved. Unlike the README, this file is kept
in English only: it is a release record read alongside tags and diffs, not a document that
explains the project to a newcomer.

## [Unreleased]

Phase 2 — authentication and per-user documents. **This phase is a breaking change for any
existing deployment**: documents are no longer a shared collection, and the migration that
introduces ownership deletes all existing documents and chunks rather than guessing an owner for
them (see Removed).

### Added

- **User accounts.** `POST /api/account/register`, `POST /api/account/login`,
  `POST /api/account/logout`, and `GET /api/account/me`, built on ASP.NET Core Identity's
  `UserManager`/`SignInManager` rather than `MapIdentityApi`, which defaults to bearer tokens —
  the wrong transport for a browser SPA.
- **Cookie authentication with CSRF protection.** Session state lives in an `HttpOnly` cookie,
  which cross-site script injection cannot read, and the CSRF exposure that choice creates is
  answered with antiforgery tokens rather than left implicit. Authentication failures return
  `401`/`403` instead of redirecting to an HTML login page, because an API must never redirect a
  `fetch` caller.
- **Per-user document ownership.** Every document and chunk carries an owner. Retrieval, listing,
  and chat are scoped to the authenticated caller, and the isolation is enforced by the database
  schema — a composite foreign key makes a chunk whose owner disagrees with its document's owner
  impossible to insert — rather than by remembering to add a `WHERE` clause on every write path.
- **Client authentication surface.** `/login` and `/register` routes with a guarded application
  root, and a bootstrap call that resolves the session once per app load before the guard decides
  anything.
- **Owner-isolation integration test.** Runs against a real pgvector instance via Testcontainers,
  seeds three users with roughly five thousand chunks each, and asserts both that results belong
  exclusively to the querying owner and that PostgreSQL chose the HNSW index scan. Isolation is a
  security property, so it is verified on every commit rather than demonstrated once by hand.

### Changed

- **`dotnet test` now requires Docker.** The owner-isolation test starts its own disposable
  Postgres container. CI needed no change; `ubuntu-latest` already ships Docker.
- **The Postgres connection string must carry `Options=-c hnsw.iterative_scan=strict_order`.**
  Owner-filtered vector search needs it: without iterative scans, pgvector applies the filter
  after the ordered index scan and can return fewer results than requested with no error at all.
  The application asserts this at startup and refuses to boot if it is missing, because a silent
  under-return is worse than a failure to start.

### Removed

- **All pre-existing documents and chunks.** The migration that adds the non-nullable owner
  columns deletes them in the same step. There is no correct owner to assign to a document
  uploaded before ownership existed, and inventing one would be worse than requiring a re-upload.
  This cannot be undone by rolling the migration back.

### Security

- **Anti-forgery is now enforced on `POST /api/documents`.** The `REVISIT` marker from 0.1.0 is
  resolved: the endpoint is authenticated, so there is now an ambient session a cross-origin form
  could replay, and the token requirement is real protection rather than friction. `POST /api/chat`
  deliberately carries no such filter — it accepts only JSON, which a cross-origin HTML form cannot
  send. Both halves of that asymmetry are asserted by tests that read the endpoints' metadata, so
  switching either one off fails the build instead of passing silently.
- **Documents are isolated per user at the schema level.** Deleting an account cannot silently
  destroy documents: the owner foreign key is `ON DELETE RESTRICT` until an explicit
  account-deletion flow decides what should happen.

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
