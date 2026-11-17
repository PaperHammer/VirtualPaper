# VirtualPaper contributor skills

These repository skills support development and review of VirtualPaper in a user-requested conversation. They are not installed into the product or shipped as wallpaper assets. Invoke a skill by name when needed; adding these documents does not start a background task or change CI.

| Skill | Use |
| --- | --- |
| [testing](testing/SKILL.md) | Existing build, project-test, real UI, installer/MSIX and workflow-contract entry points |
| [code-review](code-review/SKILL.md) | Review working/staged/branch/PR changes against actual application ownership and behavior |
| [coverage-uplift](coverage-uplift/SKILL.md) | Add meaningful tests and inspect comparable coverage gaps using current runners |
| [performance](performance/SKILL.md) | Source-based performance/memory assessment and reproducible comparisons when measurement is requested |

Existing user instructions about test scope, foreground use, commits and external actions take precedence. Review or skill installation does not itself authorize publishing, PR comments, UI interaction or release execution.

## Adaptation notes

Three workflows were adapted from the contributor skills in microsoft/microsoft-ui-reactor, using the local reactor-flyout-callbacks/.github/skills checkout at commit 3ea33f4acd6fe8627129f6280fe24d96ab45c6ca:

| Original | VirtualPaper adaptation |
| --- | --- |
| pr-review | Replace reconciler/hooks/DSL lenses with Core/UI/RPC ownership, localization, wallpaper data and EXE/MSIX review |
| coverage-uplift | Replace Reactor xUnit/MTP/selftest commands with the current MSTest/project/UI acceptance layers |
| perf-compare | Keep same-machine A/B methodology; omit Reactor's StocksGrid harness, metrics and noise threshold because this project has no equivalent committed benchmark runner |
| pr-lifecycle | Not migrated; its automated review/CI follow-up loop is outside the requested manual skill collection |
| analyzer-dym | Not migrated; it depends on Reactor diagnostics, analyzers and mur check, which VirtualPaper does not implement |

Reactor's model-evaluation results and mandatory multi-model orchestration were not carried over. Model choice, delegation and availability follow the current host and user instructions.

Examples: `$code-review`, `$coverage-uplift`, `$performance`. The testing procedure remains unchanged.

Upstream source: [Microsoft.UI.Reactor contributor skills](https://github.com/microsoft/microsoft-ui-reactor/tree/3ea33f4acd6fe8627129f6280fe24d96ab45c6ca/.github/skills). The upstream MIT notice is retained in [LICENSE-Reactor.txt](LICENSE-Reactor.txt).
