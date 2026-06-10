# Release Security Scan & Remediation Loop — 2026-06-09

## Context

User request (pre-authorized end-to-end loop): full security/vulnerability scan of
`main` in preparation for a release → file a GitHub issue per finding → then for each
issue: branch → fix → PR → wait for the user's PR-review routine → address review
comments → merge → delete branch → repeat until all issues cleared.

Base: `origin/main` @ 271da43 (post-#41). Prior audit (SECURITY_AUDIT.md, 2026-06-08)
reports S1–S4 resolved in 1.6.0, no open findings — this round hunts what it missed.

## Exclusions (do NOT re-file)

- #24 GitHub Actions SHA-pinning — **declined by maintainer preference** (lessons 2026-06-09); Dependabot handles action bumps.
- `dependency-review` CI job failure — pre-existing **repo settings** gap (Dependency graph disabled), not a code issue.
- S3 residual gap (`JsonValue` wrapping raw CLR object) — documented usage constraint by maintainer decision.
- S1–S4 themselves — resolved & shipped.

## Phase A — Scan (parallel adversarial subagents + tooling)

- [ ] A1 dependency scan: `dotnet list package --vulnerable --include-transitive` + `--deprecated` (locked-mode restore)
- [ ] A2 CID/Multihash/Varint/Multicodec parsing audit (adversarial, empirical probes)
- [ ] A3 Multibase + SimpleBase wrapper audit (canonicality/malleability/DoS)
- [ ] A4 JCS canonicalizer + EcmaScriptNumber fresh adversarial pass (post-S1–S4 gaps)
- [ ] A5 Multikey / did:key codec audit
- [ ] A6 API-surface/packaging/docs audit (thread safety, info leakage, csproj/release metadata)
- [ ] A7 CI + release workflow audit (injection, permissions, release integrity; excl. SHA-pinning)

## Phase B — Adversarial verification of findings — DONE

Dependency posture: **clean** (0 vulnerable / 0 deprecated across 10 projects, locked-mode).
6 parallel adversarial scan agents + my own end-to-end probes (`/tmp/netcid-verify`).

### VERIFIED FINDINGS (reproduced end-to-end through public API)

| # | Sev | Title | Path | Verified |
|---|-----|-------|------|----------|
| F1 | HIGH | base32 zero-symbol length malleability → 2 strings → 1 CID | `Multibase.ValidateBase32Payload` (`Multibase.cs:267`); reached via `Cid.Parse` | `bafkrei…yeq` & `bafkrei…yeqa` → identical CID `0155…9824`. Only dangling-symbol append (payload len mod 8 ∈ {1,3,6}) slips; mod8∈{4,5,0} correctly rejected. |
| F2 | MEDIUM | base36 Unicode case-fold malleability (Kelvin U+212A→k, long-s U+017F→S) | `Multibase.DecodeBase36` folds case BEFORE alphabet validation (`Multibase.cs:179`) | distinct base36 string → same CID via `Cid.Parse`. Found by 2 agents independently. |
| F3 | MEDIUM | tr-TR/az locale: `Try*` throws `IndexOutOfRangeException`, exception-normalization bypass | SimpleBase case-insensitive alphabet built with culture-sensitive `char.ToUpper`; wrapper catches don't cover it (`Multibase.cs:158,179,82`) | `Cid.TryParse`/`Multibase.TryDecode` throw uncaught under tr-TR at first base32 touch. Environmental (locale-gated), breaks documented Try*/normalization control. |
| F4 | MEDIUM | Multikey accepts invalid SEC1 EC point prefix (0x04/any) on encode+decode | `Multikey` validates length only, not `rawKey[0]∈{0x02,0x03}` for secp256k1/p256/p384/p521 (`Multikey.cs:87-102`) | `Encode(P256Pub, 0x04‖32×00)` mints a did:key; `TryDecode` accepts it. Spec mandates compressed form. |
| F5 | MEDIUM | release.yml `workflow_dispatch` from a branch bypasses tag-vs-version guard | `release.yml:40-43` `exit 0` on non-tag ref, then publishes | docs-confirmed `GITHUB_REF_TYPE=branch` on dispatch → guard skipped → `nuget push`. Requires repo write (not external). |
| F6 | LOW | JCS NaN/∞ in `JsonValue`-wrapped CLR object throws `ArgumentException` not `JcsFormatException` | `JcsCanonicalizer.cs:226-235` catch only `JsonException` | fails closed (no bad bytes), wrong exception type only. Narrow reachability (programmatic node). |
| F7 | LOW | jcs-conformance.yml empty SHA-256 pin → warn-only download integrity | `jcs-conformance.yml:28,62-72` `CONFORMANCE_EXPECTED_SHA256: ''` | test-only blast radius; not in published-artifact path. |

### REFUTED / NOT FILED (audit trail)
- Vuln-scan gate (#25) — re-verified **sound** (fail-closed; URL token + locale pin robust).
- tag-guard tag-triggered path (#26) — **sound**; only the dispatch path (F5) is weak.
- JCS number serialization, key-ordering (UTF-16 code units), dup-key (S2), surrogate (S3), depth/output caps (S1), escaping — heavy fuzz, **0 divergences**; no new bypass.
- CID core: varint canonicality/9-byte cap, MultihashDigest overflow/alloc guards, reserved v2/v3, CIDv0 constraints — held.
- Concurrency / array-aliasing / info-leak / API exception-type robustness — verified **clean** by demonstration.
- Base58 quadratic time — bounded by 4096 input cap; documented residual.
- S3 `JsonValue`-raw-CLR surrogate residual — documented maintainer decision; not re-filed.

### Release-hygiene notes (NOT security; surface to user, file only if wanted)
- H1 `<Version>` still 1.6.0 with shipped `[Unreleased]` items — normal release-prep step (fails safe via tag guard).
- H2 assembly not strong-named — policy decision; document.
- H3 no PublicApiAnalyzers/PackageValidation breaking-change guard — enhancement.
- H4 `release.yml --skip-duplicate` softens double-publish detection — minor; fold into F5 fix.

## Phase C — File GitHub issues — DONE

Filed 2026-06-09 against moisesja/net-cid:

| Finding | Issue | Title | Sev |
|---------|-------|-------|-----|
| F1 | #42 | S5 base32 trailing-symbol malleability | HIGH |
| F2 | #43 | S6 base36 case-fold malleability | MED |
| F3 | #44 | S7 tr-TR Try* IndexOutOfRangeException | MED |
| F4 | #45 | S8 Multikey EC SEC1 prefix | MED |
| F5 | #46 | S9 release.yml dispatch bypass (+H4 --skip-duplicate) | MED |
| F6 | #47 | S10 JCS NaN ArgumentException | LOW |
| F7 | #48 | S11 jcs-conformance SHA-256 pin | LOW |
| H3 | #49 | D5 PackageValidation API-break guard | LOW |
| H2 | #50 | D6 strong-naming decision (document) | LOW |

Fix order: #42 → #43 → #44 → #45 → #46 → #47 → #48 → #49 → #50
(HIGH first; F1/F2/F3 share Multibase.cs so done consecutively with re-sync between).

## Phase D — Per-issue fix loop (repeat for each)

- [ ] `git fetch origin && reset onto origin/main` (re-sync RIGHT BEFORE first edit)
- [ ] branch `feature/issue-N-<slug>`
- [ ] empirically confirm failure mechanism (issue's proposed fix may be wrong — lessons 2026-06-07/08)
- [ ] implement fix + tests (boundary AND far-over tests where DoS; pin default-path behavior)
- [ ] full build + test suite green
- [ ] adversarial review round(s) — ≥2 rounds for security fixes
- [ ] update CHANGELOG/docs/examples
- [ ] push branch, open PR (`Closes #N`), wait for user's PR-review routine
- [ ] address review comments, push
- [ ] CI green → merge (merge commit, per repo history) → delete branch

## Phase E — Final verification

- [ ] All issues closed, full suite green on main, vuln scan clean
- [ ] SECURITY_AUDIT.md updated with this round's findings/status
- [ ] Review section appended below

## Review

(to be completed)
