# Release Strategy — Smart Document Control for SAP Business One

**Version:** 0.1.0-alpha  
**Last updated:** 2026-05-10

---

## 1. Versioning

This project uses [Semantic Versioning 2.0.0](https://semver.org/).

```
MAJOR.MINOR.PATCH[-prerelease]
```

| Segment | Increments when |
|---|---|
| MAJOR | Breaking change in SAP schema, CLI interface, or config file format |
| MINOR | New feature that is backward-compatible (new document type, new rule option) |
| PATCH | Bug fix or doc update with no behavioral change |
| prerelease | `-alpha`, `-beta`, `-rc.1` — not safe for PRD |

**Current version: `0.1.0-alpha`**

`0.x.0` versions are pre-1.0: backward compatibility is not guaranteed between minor versions. The `1.0.0` release marks the first version considered safe for unattended PRD operation.

---

## 2. Version Milestones

| Version | Milestone | Entry criteria |
|---|---|---|
| `0.1.0-alpha` | Validate-only functional in TST | SAP login + UDT check validated against real TST (achieved 2026-05-08) |
| `0.2.0` | Schema installer (`--install-schema`) | UDTs provisionable via Service Layer; idempotent; reversible |
| `0.3.0` | Document discovery (read) | Open POs/ARInvoices read in TST; simulation report generated |
| `0.4.0` | Rule evaluation engine | Rules/Exclusions loaded from UDT; DocumentCloseEvaluator integrated into loop |
| `0.5.0` | Document close (simulation only) | Full simulation run in TST; LOG + RUN UDTs populated |
| `0.6.0` | Document close (real, TST) | Real close in TST; MaxDocumentsPerRun enforced; LOG/RUN verified |
| `1.0.0-rc.1` | Release candidate for PRD | All acceptance criteria from PRD §18 met; security review passed |
| `1.0.0` | First PRD-ready release | PRD simulation run validates correctly; business sign-off |

---

## 3. Branch Strategy

```
main
  └── feature/*
```

**`main`** is the stable branch. Every commit on `main` must:
- Build without warnings.
- Pass all 114+ unit tests.
- Not regress any previously validated behavior.

**`feature/*`** branches are created for each task:
```
feature/schema-installer
feature/document-discovery
feature/rule-evaluation-loop
```

Merge strategy: squash merge or regular merge into `main` after review. No rebase of `main`.

No `develop` branch at this stage. GitFlow adds overhead that is not justified until there are multiple contributors or parallel release lines.

---

## 4. Release Tags

Tags follow the format `vMAJOR.MINOR.PATCH[-prerelease]`:

```
v0.1.0-alpha
v0.2.0
v1.0.0-rc.1
v1.0.0
```

Tag after:
1. `dotnet build` passes clean.
2. `dotnet test` passes 100%.
3. `--validate-only` validated against TST (for anything touching Infrastructure/Runner).
4. `CHANGELOG.md` updated.

Tag command:
```powershell
git tag -a v0.1.0-alpha -m "Release 0.1.0-alpha: validate-only functional in TST"
git push origin v0.1.0-alpha
```

---

## 5. Changelog

This project uses the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

Changelog file: `CHANGELOG.md` at repository root.

Sections per release: `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`.

Update `CHANGELOG.md` as part of the same commit that bumps the version, before tagging.

---

## 6. PRD Promotion Gate

A release may only be tagged for `1.0.0` when all of the following are confirmed:

- [ ] All PRD §18 acceptance criteria are IMPLEMENTED (see `docs/02_PRD_VALIDATION.md`).
- [ ] UDTs created in PRD company via `--install-schema`.
- [ ] Full simulation run in PRD confirms correct candidate identification.
- [ ] `DefaultSimulation=true` in `appsettings.PRD.json` at promotion time.
- [ ] Service account permissions verified in PRD.
- [ ] `IgnoreSslErrors=false` in `appsettings.PRD.json`.
- [ ] Business stakeholder has reviewed simulation report and signed off.
- [ ] `docs/10_OPERATIONAL_RUNBOOK.md` reviewed and current.

Real document closure (`DefaultSimulation=false`) requires an additional explicit confirmation parameter not yet implemented (`--confirm-production-close`, per D10).

---

## 7. Hotfix Policy

For `PATCH` releases on an already-deployed version:

1. Branch from the release tag: `git checkout -b hotfix/v0.2.1 v0.2.0`
2. Apply fix with a targeted test.
3. Merge to `main`.
4. Tag `v0.2.1`.
5. Deploy using the standard update procedure in `docs/05_DEPLOYMENT_STRATEGY.md §7`.
