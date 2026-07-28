<!--
Keep a pull request to one work unit. If two unrelated things are in here, they are two
pull requests — splitting them costs less than reviewing them together.
-->

## What changed

<!-- One or two sentences. What this does, and why now. -->

## Why this approach

<!-- The tradeoff taken and what was rejected. Delete this section only if it is genuinely obvious. -->

## Verification

<!-- What was actually run and what it printed. Not "should work". -->

- [ ] `dotnet build backend/DocuMind.slnx` — 0 warnings, 0 errors
- [ ] `dotnet test backend/DocuMind.slnx`
- [ ] `pnpm test` and `pnpm build` from `client/`, if the client changed
- [ ] Exercised by hand, if the change is observable in the running app

## Documentation

- [ ] `README.md` updated, or not applicable
- [ ] `README.es.md` updated in the same commit — CI enforces this, and a stale translation is
      invisible precisely to the readers it misleads
- [ ] Entry added to `CHANGELOG.md` under `Unreleased`, if the change is user-visible

## Deliberately left out

<!--
Anything not done here, each with the condition that makes it due — not a bare TODO.
A finding dismissed without a reason comes back; a finding dismissed with one becomes
knowledge about the project.
-->
