# DocuMind — working conventions

## Documentation is bilingual, and both languages ship together

The project documentation exists in two languages:

| File | Language | Role |
| --- | --- | --- |
| `README.md` | English | Canonical. GitHub renders it on the repository home. |
| `README.es.md` | Spanish | Translation. Linked from `README.md` line 1 and links back. |

**Any change to one README requires the equivalent change to the other, in the
same commit.** Never commit a documentation change in a single language.

This is not a style preference. A stale translation is worse than a stale
README: a reader who only speaks the translated language has no way to tell they
are following outdated instructions, so the error is invisible precisely to the
people it harms. This has already happened once in this repo in a single
language — the setup steps documented a chat deployment that `appsettings.json`
did not declare — and adding a second language doubles the surface.

### CI enforces this

The `docs` job in `.github/workflows/ci.yml` runs two checks:

1. **Direction** — if `README.md` changed in the pushed range, `README.es.md`
   must have changed too. Enforced in one direction only: English is canonical,
   so the drift that matters is English moving ahead of its translations. A
   Spanish-only commit is a translation fix and introduces no drift.
2. **Structure** — both files must share the same heading shape: every section
   present in both, at the same level, in the same order. This one runs on every
   push regardless of what changed, because it is an invariant.

Reproduce the structure check locally. Note that it strips fenced code blocks
first: the setup sections contain shell comments that a plain `grep '^#'` counts
as headings, which is why the naive version of this check does not work.

```bash
headings() {
  awk '/^```/ { f = !f; next } !f && /^#{1,6}[ \t]/ { match($0, /^#+/); print substr($0, 1, RLENGTH) }' "$1"
}
diff <(headings README.md) <(headings README.es.md)
```

Neither check can see a reworded paragraph — prose edits keep the heading shape
identical. So edit both files in the same pass; the checks are a backstop for
the mistake you did not notice, not a substitute for doing it right.

If an English change genuinely has no translated counterpart, add a trailer to
the commit message rather than working around the check:

```
Docs-i18n-exempt: <reason>
```

That skips the direction check for that range. It does not skip the structure
check, which is an invariant and has no exemption.

Spanish documentation uses a neutral, professional register. No regional slang
or voseo, regardless of the tone of the conversation that produced the change.

When a third language is added, it follows the same pattern: `README.<lang>.md`,
listed in the switcher on line 1 of every other README. Language switchers use
the language name as text, never a flag emoji — a flag denotes a country, not a
language, and text is readable by screen readers. Extend the `docs` CI job to
cover the new file at the same time, or the check silently stops guarding it.

This file is intentionally English-only and outside the translation rule. It
configures how tools and agents work in this repository rather than explaining
the project to its readers, so there is no audience that would be misled by it
existing in one language.

## When a change lands

1. Make the code change.
2. Update `README.md` if it documents anything the change affects — setup steps,
   configuration keys, decisions, project status.
3. Make the same update in `README.es.md`.
4. `dotnet build backend/DocuMind.slnx` — must stay at 0 warnings, 0 errors. The
   zero-warning baseline is deliberate: it is what makes a new NuGet advisory
   visible the day it appears instead of lost in noise.
5. `dotnet test backend/DocuMind.slnx` — all tests must pass.

## When a slice is finished

A slice is a vertical feature increment (Slice A — Ingestion, Slice B — Chat +
UI). On completion, in **both** languages:

1. **Project status** — mark the finished slice done and name what the next one
   covers.
2. **Roadmap** — tick what the slice completed.
3. **Known follow-ups** — add anything deliberately deferred, each with the
   condition that makes it due ("once authentication lands", "once upstream
   raises the floor"), never as a bare TODO. Remove entries the slice resolved.
4. **Architecture / Tech stack** — update if the slice introduced a component or
   dependency.
5. Verify build and tests, then commit with the documentation alongside the code
   rather than in a separate follow-up commit.

## Configuration and secrets

No credential literal belongs in tracked configuration. See the "No credential
literal in tracked configuration" entry in the README for the full rationale.

- `appsettings.json` — non-secret deployment topology only.
- `dotnet user-secrets` — `ConnectionStrings:Postgres`, `AzureOpenAI:Endpoint`,
  `AzureOpenAI:ApiKey`.
- `.env` (untracked, copied from `.env.example`) — the Compose Postgres
  credentials. They must agree with the connection string in user-secrets.

Note for agents: the user-secrets store lives under `%APPDATA%` and an agent
shell may see an overlay of that path rather than the real store. Do not
conclude a secret is missing, or confirm one was set, from `dotnet user-secrets
list` run by an agent. Ask the user to run it in their own terminal.
