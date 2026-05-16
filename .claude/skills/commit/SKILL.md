---
name: commit
description: Git commit conventions for Coralia — prefixes, branch naming, CHANGELOG update
---

# Git Commit Conventions — Coralia

## Branch naming
`issue-N-short-description` — always branch per issue, never work directly on main.

## Commit message format
```
prefix: short description in lowercase

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

**Prefixes:**
- `feat:` — new feature
- `fix:` — bug fix
- `chore:` — config, tooling, non-functional
- `docs:` — documentation only
- `refactor:` — code restructure, no behavior change
- `polish:` — visual/ux improvements, no new behavior

**Rule: one issue = one commit.** Don't split a feature across multiple commits.

## After each commit
1. Update `CHANGELOG.md` with a bullet under the appropriate phase/chunk
2. Open a PR against `main` even solo — forces self-review
3. Close the GitHub issue in the PR description with `Closes #N`

## Staging files
- Never `git add -A` or `git add .` — stage specific files to avoid committing `.gitignore`-worthy files
- Never commit: `*.keystore`, `google-services.json`, `GoogleService-Info.plist`, `user://` files
