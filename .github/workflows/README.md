# CI/CD workflows

[English](README.md) | [简体中文](README.zh-CN.md)

The repository has two workflows, both triggered only by `workflow_dispatch`. Commits, PR creation or updates, label changes, and merges do not start runs automatically.

| File | Manual inputs | Purpose |
| --- | --- | --- |
| `pre-merge-check.yml` | Open PR number; whether to include model integration tests | Validate source and version label → build → parallel tests → required installer smoke + independent UI acceptance → report PR status |
| `pre-release.yml` | Merged `dev → main` PR number | Verify PR → set version/build → parallel tests → package/smoke + independent UI → summary gate → sync versions → draft |

Both workflows have independent installer-smoke and real-UI jobs. Installer smoke is mandatory: install → open/normal exit → same-package reinstall → preserved/fresh-profile startup/normal exit → uninstall. Every smoke stage must pass without skips. Only `test-ui-operation` uses job-level `continue-on-error`; its runner outcome and raw reports remain visible. Summaries use `-AllowUIFailure -UIJobResult` so UI failures/timeouts or missing reports do not block required checks, version synchronization or drafts. Local runners remain strict by default.

## Everyday workflow

1. Open a feature or fix PR targeting `dev`.
2. Select **Actions → Pre-merge Check → Run workflow** and enter the PR number.
3. Merge into `dev` after the check succeeds.
4. Open a `dev → main` PR, select one version label, and manually run **Pre-merge Check**.
5. Merge into `main` after the check succeeds.
6. Select **Actions → Pre-release → Run workflow** and enter that merged PR number.
7. Download and verify the draft release assets, then publish manually.

The branch selected in the Run workflow form chooses the workflow definition. The supplied PR determines the code being tested or released; the selected branch's HEAD is not substituted for it. Running from the default branch is recommended. The former `ci-check-approval` and `publish-approval` environments and deployment approvals are no longer used.

Workflow files must reach the default branch before GitHub shows the manual entry point. During initial migration, an administrator must coordinate replacing required checks so PRs do not wait for removed checks. See [GitHub's manual workflow instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow).

## Pre-merge Check

Only same-repository PRs are supported:

- Into `main`: the source must be `dev`, with exactly one of the four version labels.
- Into `dev`: feature/fix branches other than `main` or `dev` are accepted; no version label is required.
- Other base branches and fork PRs are rejected.

Each manual run reads the current source, labels, PR head, and base branch, then verifies that GitHub's merge snapshot matches both current commits. All builds and tests use that fixed snapshot.

After the shared build, eight project jobs run in parallel. When they pass, two independent jobs run:

1. **Installer Smoke** compiles ISS, installs in English, opens the installed Core/UI, retains test settings and a wallpaper, and verifies normal exit. The same package reinstalls in Chinese; the original profile must preserve its language/settings/wallpaper, while a separate fresh profile initializes in Chinese. Both sessions must exit normally, then the real uninstaller runs. Package/file hashes, shortcuts, registration and residue are checked.
2. **Real UI Acceptance** runs the extended suite against the Release build in its own runner, starts real Core/UI/player processes with isolated profiles, and cleans up its own sessions. It does not use the smoke installation or installer. Failures are non-blocking warnings; unavailable desktops produce explicit UI skips.

Desktop smoke runs operate the install/reinstall/uninstall wizards. Without a desktop, silent install/reinstall/uninstall diagnostics are retained, but skipped startup/profile checks **fail smoke in both workflows**. Forced recovery never counts as normal exit.

Required-stage failures block later phases. Reports are still collected; missing, empty, failed, aborted, or exceptional project/installer results fail the final check. Extended UI failures are non-blocking warnings and retain their raw errors. The seven regular test projects retain separate Cobertura coverage reports.

The eight project jobs share `test/actions/run-project` and `test/Run-ProjectTests.ps1`. `test/TestReportSupport.ps1` maps projects to reports, and `test/Summarize-TestResults.ps1` creates the summary. Project jobs and the summary use the same TRX validation logic.

`build-output` preserves the repository-root `src/` layout and both test hosts. Smoke packages this build; independent UI runs its Release executables directly without rebuilding. The two reports are validated independently. The actual UI runner outcome takes precedence over a nominally passing report if the host or cleanup failed.

Artifact transfers use `actions/upload-artifact@v7.0.2` and `actions/download-artifact@v8.0.2`, both with a bundled Node.js 24 runtime (minimum Actions Runner `2.327.1`). This runtime is separate from the Node version selected for Web player script tests. Uploads retain ZIP archives and `overwrite: true`; downloads retain extraction and reject digest mismatches by default. See the upstream [upload release](https://github.com/actions/upload-artifact/releases/tag/v7.0.2) and [download release](https://github.com/actions/download-artifact/releases/tag/v8.0.2). Changes to workflow action versions require a new **Run workflow**; rerunning an old run retains its original workflow version.

Raw reports use `test-results-*`; combined summaries use `test-summary-*`, so raw-report downloads do not include earlier summaries. Uploads set `overwrite: true` to support rerunning jobs within the same run. The summary reads designated project report paths rather than recursively including other packages.

ML tests categorized as `Integration` are excluded by default. Select **Also run real model inference tests** to include them. These regular ML tests require model files and explicitly skip when files are missing; they do not download models. This differs from the opt-in UI ModelInference case, which fails if its model is missing. To run the regular integration category locally:

```powershell
dotnet test src/VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj --configuration Release --filter "TestCategory=Integration" --settings src/test.runsettings
```

Closed, merged, or fork PR inputs are rejected without altering prior check status. For a valid open PR, the workflow sets `pre-merge-check: pending` on its head SHA. Before setting success/failure, it rechecks the PR commit, base, and version label. The status belongs to the PR commit, not the branch selected in the dispatch form. See the [GitHub commit status API](https://docs.github.com/en/rest/commits/statuses).

After a new PR commit, rerun manually: success on an earlier SHA does not satisfy the new commit's required check. Label changes neither start runs nor invalidate existing success on the same SHA, so rerun manually after changing labels too. The release entry point checks labels again.

Changes to the PR, target commit, or version choice during a run prevent success. A manually cancelled run may leave a pending status; rerun it before merging.

## Version labels

| Label | Increment, resetting fields to its right | Result from `0.5.2.0` |
| --- | --- | --- |
| `version:major` | First field | `1.0.0.0` |
| `version:minor` | Second field | `0.6.0.0` |
| `version:build` | Third field | `0.5.3.0` |
| `version:revision` | Fourth field | `0.5.2.1` |

These labels control the four-part product version. Missing or multiple version labels block release; the source branch no longer determines the increment.

Independent plugin updates use `update-plugins` or `update-plugin-ui/playerweb/scrsaver/ml/shaders`, retaining the existing plugin build-number and client-compatible tag formats. With a plugin-update label, the product version is unchanged and no product-version synchronization commit is made. A `dev → main` PR still requires exactly one of the four version labels.

## Pre-release

Only a merged, same-repository `dev → main` PR is accepted. Its head must have a successful `pre-merge-check` status corresponding to this PR and its current version label, and `main` must still point to the PR's merge commit. Unmerged, older, unchecked, or incorrectly labeled PRs are rejected.

1. Calculate the target product version from `AssemblyVersion` at the fixed merge commit and the selected label. Plugin updates keep the product version. Read published component manifests and historical release titles/plugin tags to allocate the next `YYMMRxx` build number; numbers used by drafts are reserved too. Check target tag/release conflicts before building.
2. Temporarily update `VirtualPaper.csproj` before compilation and `InnoSetup/setup.iss` before packaging so the binaries and installer use the target version.
3. Retest the new build through the same eight project jobs/actions as Pre-merge Check: artifact checks plus seven regular test projects. Release includes ML integration tests and Web player script checks. No test job rebuilds the application.
4. Compile ISS and run mandatory installer smoke, then create requested plugin packages. Independently, `test-ui-operation` runs extended real UI acceptance against the release build. `summarize-tests` downloads both sets of reports and uses `Summarize-TestResults.ps1 -RequireFullAcceptance -AllowUIFailure -UIJobResult <outcome>`: failed/missing/skipped smoke or project checks block progress; independent UI failures and skips remain explicit non-blocking results.
5. Recheck tag/release conflicts before version synchronization. For a regular release, update only these fields on `main` and `dev`, then push both atomically with `git push --atomic`. If either update is rejected, neither branch advances:
   - `src/VirtualPaper/VirtualPaper.csproj`: `AssemblyVersion`
   - `InnoSetup/setup.iss`: `MyAppVersion`, `MyAppVersionText`
   - `README.md`: release badge
6. Create a draft release using the PR description. Every release includes the installer and complete `app_comp_manifest.json`; plugin releases also include patch packages. The tag targets the synchronized `main` commit, or the original merge commit for plugin updates. Publishing remains manual.

The stages are `prepare-metadata → build-release → parallel project tests → package-release + test-ui-operation → summarize-tests → sync-versions → draft-release`. Packaging/smoke and optional UI run independently. Version synchronization and draft creation require successful packaging and the validated summary. Plugin releases use the same gates but skip product-version synchronization. Required smoke and selected release project tests cannot be skipped; only independent UI may fail or skip.

One root-layout build artifact contains application/plugin output, all project tests, and both test hosts. Parallel tests and packaging download that same build; only the application output is packaged. The component manifest is generated once and reused for release assets and plugin patches. Locking, registry-modification UI cases and real idle screen-saver tests remain excluded under the existing test policy.

Component manifests are fetched with `GITHUB_TOKEN` authentication. Network, permission, download, or parsing errors stop the release. A regular release assigns a new build number to every bundled plugin. Plugin releases update only selected plugins and retain other entries from the previous published manifest.

If historical releases lack a manifest, including `v0.5.2.0` without recorded build numbers, one full release may establish a baseline. Every bundled plugin receives the new build number; old plugin versions are not guessed. Historical title/tag build numbers still reserve numbers. With no historical numbers, allocation starts at the current month's `R01`. Plugin patches require a valid baseline manifest. A corrupt existing manifest does not fall back to the no-baseline path.

The monthly sequence has two digits and stops at `R99`, avoiding older clients treating `R100` as older through string comparison. A new month starts at `R01`. A build-machine clock behind historical numbering also causes an error.

Version synchronization preserves business code and documentation added to `dev` during packaging. It does not merge all of `main`, force-reset, or overwrite branches. Concurrent remote updates, unexpected version fields, or insufficient push permissions stop synchronization and draft creation. The `.sln` contains no product version and needs no change. `app_comp_manifest.json` is generated output.

If synchronization succeeds but draft creation fails, use **Re-run failed jobs** on the original run. Restarting the whole workflow is blocked because `main` has advanced, preventing a second increment. Artifacts can overwrite earlier uploads in the same run. Manifests, release packages and the release test summary are retained for 30 days; build output and raw test reports are retained for seven days.

Existing tags or incompatible releases block progress. An interrupted draft upload may resume only when tag, title, and target commit match. Remote checks and writes are not one atomic transaction; do not manually create the same tag or publish that draft during preparation. There is no automatic forced rollback or separate manual rollback workflow.

## Repository configuration

These settings must be configured on GitHub; editing local YAML does not change remote branch rules:

- For `main`, require a PR and the `pre-merge-check` status. If `dev` should also require PRs, configure the same protection there. If direct pushes to `dev` are allowed, they do not trigger these manual workflows; Pre-merge Check still requires an open PR.
- Remove obsolete required checks such as `Check Source Branch` and `ci-check/pre-publish-tests`; the new workflows do not provide them.
- Consider requiring the PR branch to be up to date with its base, and rerun checks after base changes.
- `RELEASE_PAT` must allow the version-sync bot to update both `main` and `dev` under the repository's rules. Insufficient permissions fail synchronization; the workflow does not force-push around protection.
- Create the four fixed `version:*` labels once.
- Local workflow changes do not delete old remote branches such as `release` or `bugfix`. Clean them up separately after checking for unmerged work.

## Local validation

The complete local counterpart to the test/package stages is:

```powershell
# Elevated PowerShell 7.2+, clean Windows test installation:
pwsh -NoProfile -File test/Run-Tests.ps1
# Include the regular ML integration category too:
pwsh -NoProfile -File test/Run-Tests.ps1 -IncludeMLIntegration
```

The local chain uses the working tree and does not validate PRs, post status, synchronize versions or create drafts. It builds, runs project tests, packages and completes smoke including uninstall, then runs independent extended UI against Release output. It downloads no models. Smoke skips/failures and local UI failures remain blocking; CI explicitly opts into non-blocking UI at the job/summary level.

See the [testing guide](../../test/README.md) for prerequisites, focused runners and report paths, and the [repository test skill](../../.agents/skills/virtualpaper-testing/SKILL.md) for AI instructions. Changes to testing behavior should update these guides and their Chinese versions together.

For workflow/script contracts alone, without an installation or UI run:

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

This validates workflow dependencies, shared test entry points, TRX/coverage checks, retry report isolation, UI environment skips, installer reports, release metadata, and version-sync contracts. It uses isolated Git repositories and mocked GitHub responses. It does not install the application, lock the session, modify the registry, or push to a remote repository.
