---
name: performance
description: Analyze VirtualPaper responsiveness, memory, library queries, image/Web work or compare a performance change with a baseline. Use for 性能评估, 内存泄漏, 高频调用 or measured before/after comparisons. Separates source-based analysis from measurements and uses an existing relevant harness when available.
---

# VirtualPaper performance analysis and comparison

Read [AGENTS.md](../../../AGENTS.md) and the affected process/module. Begin with the user's workload and whether they asked for source analysis, profiling or a measured comparison. A source-level review does not authorize occupying the desktop.

## Locate the actual workload

VirtualPaper has no Reactor reconciler/data-grid benchmark. Search the current test/project scripts for a relevant measurement entry point before offering a benchmark command. Functional test duration, a performance-settings test and a coverage report are not standardized performance benchmarks.

Useful investigation surfaces:

| Symptom | Trace |
| --- | --- |
| Slow library/filter/scroll | Folder scanning, metadata reads, index/query snapshots, sorting, cache invalidation, observable collection updates and virtualization |
| Slow image/depth/AI processing | Size metadata versus full decode, CPU/GPU backend, inference model, image size, file I/O and temporary outputs |
| Web/player lag | Script queue dispatch, coalescing of state changes, WebView2 messages and player lifecycle |
| Memory retained after leaving a page | Events, timers, tasks/CTS, cached views, WebView2, composition and native/model objects |
| Startup/exit or install slowness | Core/UI/player readiness and normal teardown; separate package/install cost from runtime startup |

Identify the hot call path and its frequency before optimizing. Locks and try/catch syntax alone do not establish a bottleneck; distinguish lock hold time, contention and thrown-exception frequency. Prefer a bounded change in the existing owner.

## Source-based assessment

State the input scale and cost model. Look for repeated full scans/sorts/decodes, unbounded queues, redundant dispatch, retained closures, synchronous UI-thread I/O and cache invalidation that affects every page.

Trace ownership and release paths before claiming a leak. A stable cache, temporary peak and monotonically retained object are different observations. Describe evidence, likely impact and what a measurement would resolve; label unmeasured conclusions as estimates.

Do not change product code for an analysis-only request. A separately requested optimization can follow the assessment within the authorized scope.

## Measured comparisons

Use an existing appropriate harness when it provides the requested metrics. If none exists, say so. When a measurement request reasonably includes a small headless reproducer, create only the necessary harness in the requested scope; otherwise provide the concrete missing measurement setup. Never invent a stock VirtualPaper benchmark or copy Reactor's runner/metrics.

For an A/B run:

1. Pin the intended base and candidate, including uncommitted inputs when requested. Use the PR's real base or an explicit ref rather than assuming main.
2. Preserve the dirty tree. Use a separate clean worktree or isolated snapshot for the baseline; do not checkout/reset the user's working directory. Remove only clean worktrees created for this experiment.
3. Match Release configuration, architecture, runtime, machine, power settings, input files/library size, model/backend and cache conditions. Use isolated data, not daily user settings or wallpapers.
4. Separate build/install/warmup from timed work. Alternate baseline/candidate runs on the same machine, retain raw samples, and compare cold and warm behavior separately where relevant.
5. Report sample counts, median and spread. Lower latency/memory is usually better; throughput is higher-is-better. Distinguish percentage change from direction of improvement. Use measured variability rather than importing an arbitrary noise threshold from another harness.

Count the relevant process group: Core, UI, players and WebView2 children where the workload owns them. Identify what the memory counter measures and when it was sampled; process working set alone does not prove managed retention.

For queue work, measure completed dispatch/processing, not just successful enqueue. For library work, use equivalent inventory/filter/order and avoid timing a cached result against a cold full scan without saying so.

## Execution and result limits

Follow [testing](../testing/SKILL.md) for build/test/installed-session entry points. Respect deferred real UI, input and foreground use; do not start the daily application or an inference model merely to make a source review look measured.

A window activation crash or a missing GPU/model is an observed failure to investigate, not proof that the application is fine. Preserve logs and report the incomplete comparison. Do not substitute hosted-runner numbers for the missing local side or force an unrelated workflow.

Return a concise assessment or a baseline/candidate/delta table with source IDs, workload, environment, samples and limitations. Keep measurements, hypotheses and proposed changes distinct. Do not post benchmark comments or push fixes unless requested.
