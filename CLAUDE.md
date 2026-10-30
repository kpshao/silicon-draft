## Agent skills

### Issue tracker

Issues and specs live as GitHub issues in this repo, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Acceptance criteria

Before closing a completed issue, tick every met Acceptance criteria checkbox — in both the Chinese list and the English version inside the `<details>` block. Leave a criterion unchecked only when it is explicitly deferred, and say why in a comment.

### Issue and PR descriptions

GitHub issue and pull request descriptions are bilingual: Chinese first, then the English version in a collapsed `<details><summary>English</summary>` block, matching the format used by issues #2 and #3. Keep both versions in sync.

### Documentation language

`README.md`, `GLOSSARY.md`, and `docs/adr/*.md` are bilingual: Chinese first, then the English original in a collapsed `<details><summary>English</summary>` block at the end of the file. Chinese is the primary version and both versions are updated together in the same change. Keep canonical English domain terms, code identifiers, JSON keys, CLI flags, and file names untranslated.

### Triage labels

Five canonical triage labels use their default strings: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
