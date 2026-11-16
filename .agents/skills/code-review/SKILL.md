---
name: code-review
description: Review VirtualPaper working changes, staged changes, branches or PRs for correctness, UI behavior, architecture, security, tests, localization and packaging. Use for review my diff, re 下 diff, review before commit or PR readiness questions; handle a narrow function explanation directly.
---

# VirtualPaper change review

Read [AGENTS.md](../../../AGENTS.md) for ownership and execution boundaries. Review the WPF Core, WinUI UI, gRPC clients and players as separate processes; do not apply Reactor reconciler, hooks or DSL conventions to this application.

The review phase is read-only. Carry out separately authorized fixes, commits or pushes after review; preserve instructions such as "analysis only", "do not test" and "do not commit".

## Resolve the actual scope

| User intent | Snapshot |
| --- | --- |
| Uncommitted changes / before committing | `git diff HEAD`, including staged changes, plus relevant untracked files |
| Staged changes only | `git diff --cached` and the staged versions of files |
| Branch / PR | `git diff <base>...HEAD` and surrounding implementation |
| Branch including local work | Resolve `git merge-base <base> HEAD`, then `git diff <merge-base>` plus relevant untracked files |

Use an explicit base first, then the PR's actual base or stacked parent. A feature PR normally targets dev; do not substitute main merely because it exists. If no PR context is available, inspect upstream and local branch history before choosing dev/main, and state the selected base.

For an unqualified "re 下 diff" with a dirty tree, review current uncommitted work and say so. Ask only when scope materially changes the conclusion and cannot be inferred. Capture head/base IDs, status, statistics and untracked paths before reading the diff. For staged review, read staged content rather than a newer working copy.

Large or mechanical diffs do not require permission merely because of their file count. Group by ownership, inspect generation inputs and consumers, and disclose any unreviewed area.

## Follow evidence through the application

Read callers and persistence consumers, not only the edited method. Trace a concrete trigger through the owning process, RPC/service, stored state and visible result. Compare with the previous implementation before calling an existing defect a regression.

Load the relevant review references:

- [Correctness, UI and architecture](references/correctness-and-ux.md): process lifecycle, async work, memory, layout, localization and simpler existing abstractions.
- [Security and packaging](references/security-and-packaging.md): file/Web boundaries, installed assets, installer/MSIX behavior and workflow gates.
- [Tests and documentation](references/tests-and-docs.md): behavioral assertions, report evidence, generated resources and user-facing documentation.

Consider simpler solutions using current owners such as FileUtil, metadata loaders/migrators, the wallpaper index and UIComponent. Recommend a shared abstraction when ownership or repeated behavior justifies it; do not demand a new service for a one-off layout edit.

Review directly by default. Additional agents or model checks are optional only when permitted by the host and the user's request; this skill does not authorize delegation or model overrides. Keep source evidence regardless of reviewer agreement.

## Verify proportionately

Follow [testing](../testing/SKILL.md) when execution is requested or needed. Existing fresh reports may already establish the relevant result; do not repeat a full regression solely to complete a review checklist.

Honor the session's restriction on real UI or foreground use. A solution build, XML parse, translation-key check or headless test cannot establish visual layout, focus, menu or tray acceptance. Report these limits explicitly.

## Report actionable findings

For each finding, give severity, a current file/line, the user-visible failure, its trigger and the evidence chain. Explain uncertainty instead of presenting a hypothesis as a demonstrated defect. Distinguish introduced regressions, existing defects and test/documentation gaps.

Prioritize data loss, broken startup/exit, release blockers and reproducible behavioral defects. Omit formatting preferences, speculative redesigns and restatements of code. Missing tests alone do not prove a product bug.

Lead with findings and concrete fixes. If none are found, say that no actionable issue was found in the reviewed scope, then summarize checks and material limits. Avoid a mandatory lengthy report for a small diff. Do not describe an unperformed review or unavailable second opinion as passed.
