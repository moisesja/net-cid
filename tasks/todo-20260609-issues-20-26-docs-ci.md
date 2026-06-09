# Issues #20–#26 — docs refresh + CI hardening (one branch, one PR)

Branch: `feature/issues-20-26-docs-ci-hardening` · PR: #41 · orchestrated with the Workflow tool.

## Plan (approved)
- [x] Sync `origin/main`, branch off, capture baseline test counts (unit 230/1-skip/231, integration 6).
- [x] Workflow: Implement (sequential) + adversarial Verify (parallel).
- [x] #20 SECURITY_AUDIT.md — cover JCS/P-521/Multikey, all 14 `NetCid/` files, real counts, S1–S4, honest conclusion.
- [x] #21 ARCHITECTURE.md — JCS/Multikey rows, RFC 8785 row, JCS design section, static-class fix.
- [x] #22 CHANGELOG.md — `## [Released]` → `## [Unreleased]` + Security note.
- [x] #23 AGENTS.md — two typos.
- [x] #25 ci.yml + security.yml — gate the vuln scan to fail the build.
- [x] #26 release.yml — tag↔`<Version>` guard.
- [~] #24 SHA-pinning — **implemented then reverted by the maintainer; declined by preference (left open).**

## Review / results
- **Adversarial verification (multi-agent workflow):**
  - #25 vuln-gate matcher probed with crafted vulnerable / clean / severity-word-in-package-name inputs + the real `dotnet list` output → no false negative or false positive reproduced. Matches `has the following vulnerable packages` sentinel under `set -euo pipefail`.
  - #26 release guard executed across 4 scenarios (matching tag → pass; mismatch → fail; no-`v` prefix → pass; branch/`workflow_dispatch` → skip). Portable `sed` extractor returns `1.6.0`.
  - Docs checked against ground truth: every one of 14 `NetCid/` files in scope, counts match a live `dotnet test`, S1–S4 listed with severities + Resolved.
- **Mid-flight correction:** the maintainer reverted the #24 SHA pins ("don't change them, move on"). Removed the now-false "Pinned all GitHub Actions … to 40-char SHAs" claim from the CHANGELOG; PR does **not** close #24.
- **Verification:** all 5 changed workflow YAML files parse; unit + integration tests green (no `.cs` changed); single squashed commit `2cfb0d3`.
- **PR #41** closes #20, #21, #22, #23, #25, #26.
- **Pre-existing CI failure (out of scope):** `dependency-review` job fails — "Dependency review is not supported on this repository … enable Dependency graph." Repo-settings issue, not introduced here.

## Follow-ups
- #24 stays open (maintainer keeps mutable major tags + Dependabot).
- Optional: enable GitHub Dependency graph in repo settings so the `dependency-review` job stops failing on PRs.
