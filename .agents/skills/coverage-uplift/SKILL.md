---
name: coverage-uplift
description: Add or improve meaningful VirtualPaper tests and inspect coverage gaps. Use for 补齐单测, improve test coverage, cover storage/migration branches or assess test completeness. Uses existing MSTest, artifact and real-UI layers; does not rewrite product architecture merely to raise a percentage.
---

# Improve behavioral coverage

Read [AGENTS.md](../../../AGENTS.md), [testing](../testing/SKILL.md) and the affected implementation/tests. Use this skill to add or improve tests; use [change review](../code-review/SKILL.md) for a report-only review.

## Establish the target and baseline

Identify the requested behavior, relevant source files and coverage goal. "100%" means a stated file/module and instrumentation scope, not all product behavior. Preserve the existing coverage include/exclude configuration in [src/test.runsettings](../../../src/test.runsettings).

Select the current project job from [test/TestReportSupport.ps1](../../../test/TestReportSupport.ps1), not remembered test counts:

| Area | Job |
| --- | --- |
| Core services, settings and metadata | test-core |
| View-model decisions, library and AI task logic | test-ui-logic |
| Image / Web project logic | test-images / test-web-backdrop |
| Reusable component logic | test-ui-components |
| ML / shader logic | test-ml / test-shader |
| Shipped resources and files | test-artifacts |

Complete the normal Release solution build before the no-build project runner, following the testing skill. Use a fresh result directory for each baseline or revised run. For example, after that build:

```powershell
$coverageResults = "TestResults/coverage-review-$([guid]::NewGuid().ToString('N'))"
pwsh -NoProfile -File test/Run-ProjectTests.ps1 -JobId test-ui-logic -ResultsDirectory $coverageResults -RequireCompleteExecution
```

Read the fresh TRX and the run's *.cobertura.xml attachments. Artifact tests have no coverage collector. Missing reports or zero executed tests do not establish a baseline.

For per-file gaps, normalize source paths and inspect uncovered lines/branches against the current source. When a file appears in multiple classes/reports, deduplicate source lines and combine hits correctly; do not add duplicate line denominators. Keep before/after source scope, configuration and exclusions comparable.

## Choose the layer by observability

- Use headless MSTest projects for parsing, persistence, migration, filters, queues and close-decision coordination without constructing live WinUI objects.
- Use ReleaseBuildData tests for actual packaged output/resources, not screenshots.
- Use test/VirtualPaper.UIOperationTest for production controls, menus, language/theme, focus, tray and real user interactions. Follow its existing registration and session-isolation pattern.
- Use installed acceptance only for install/reinstall/startup/uninstall behavior. Keep its scripts and report validation.

Honor the user's restriction on foreground/input and daily installations. If real UI execution is deferred, implement/review the relevant cases where in scope, mark execution pending, and continue permitted headless work. Do not report UI acceptance from a unit test or resource-key check.

## Add assertions that expose the defect

Use concrete outcomes: changed persisted values after reloading, preserved IDs/media bytes, cancellation leaving originals intact, an exact filtered result/order, normal exit, or a specific UI state after the real action.

Cover materially different branches: absent/empty/corrupt data, supported old versions, existing user content, partial failure, cancellation and concurrency where the implementation promises it. Assert failure behavior as well as the happy path.

Avoid assertions that mirror setters, test only "no throw", or can pass on stale fixture data. Avoid new tests for trivial reversible styling. Do not broaden public APIs, relocate production ownership or add broad catch blocks solely for coverage.

When an oracle is doubtful, demonstrate that it rejects a realistic wrong result. A pre-fix run or a small isolated negative/mutation control can help. Keep such experiments outside the user's dirty tree and restore only experiment-owned changes. Mutation testing is evidence for the important assertion, not a required ceremony for every test.

## Re-run and report

Rebuild when source/tests changed, then run the selected jobs with fresh reports. For test-runner/registration/workflow-contract changes, also follow the testing skill's contract checks.

Report the target, added behavioral cases, executed/passed/skipped counts and comparable coverage delta. State remaining real-UI, hardware/model or unobservable branches honestly. Do not change exclusions or remove guards to manufacture 100%.

Independent oracle review is optional when permitted and useful; do not require another model family or start agents to complete an ordinary test change. Preserve explicit instructions about commits and execution.
