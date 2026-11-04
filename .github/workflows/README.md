# CI/CD workflows

[English](README.md) | [简体中文](README.zh-CN.md)

The repository has two workflows, both triggered only by `workflow_dispatch`. Commits, PR creation or updates, label changes, and merges do not start runs automatically.

| File | Manual inputs | Purpose |
| --- | --- | --- |
| `pre-merge-check.yml` | Open PR number; whether to include model integration tests | Validate source and version label → build → installer acceptance → parallel tests → real UI acceptance → report PR status |
| `pre-release.yml` | Merged `dev → main` PR number | Verify checks → calculate versions → build and package → synchronize version fields → create a draft release |

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

The test phases run in sequence:

1. **Installer acceptance**: install the real package → launch installed Core and let first-run behavior open UI → normally close UI and Core → run the real uninstaller. Forced recovery or leftover files/processes fail acceptance. Desktop runs operate the actual installation/uninstallation wizards. Headless runs silently check files, versions, shortcuts, registration, and removal; opening UI is explicitly skipped.
2. **Parallel tests**: seven independent jobs for Core, UI, StaticImg, WebBackdrop, UIComponent, ML, and Shader. Artifact checks run in parallel; Web player script checks run with the UI job.
3. **Real UI interaction**: after both earlier phases pass, run `test/VirtualPaper.UIOperationTest` with real Core and UI processes. Without a usable desktop, report `Skipped` rather than a UI pass. Locking and registry-modification test cases remain skipped.

Failure blocks later test phases. Reports are still collected; missing, empty, failed, aborted, or exceptional test results fail the final check. The seven regular test projects retain their separate Cobertura coverage reports.

The eight project jobs share `test/actions/run-project` and `test/Run-ProjectTests.ps1`. `test/TestReportSupport.ps1` maps projects to reports, and `test/Summarize-TestResults.ps1` creates the summary. Project jobs and the summary use the same TRX validation logic.

`build-output` preserves the repository-root `src/` layout, shared test entry points, and Release binaries for the WinUI host. Consumers download it to the repository root. Real UI acceptance uses `-NoBuild -HostExe` to run those host and application binaries without rebuilding the solution.

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
3. Validate build output and create the installer. When requested, create the existing plugin patch packages and manifest. This workflow does not install, open, or uninstall the application; installer acceptance belongs to Pre-merge Check.
4. Recheck tag/release conflicts before version synchronization. For a regular release, update only these fields on `main` and `dev`, then push both atomically with `git push --atomic`. If either update is rejected, neither branch advances:
   - `src/VirtualPaper/VirtualPaper.csproj`: `AssemblyVersion`
   - `InnoSetup/setup.iss`: `MyAppVersion`, `MyAppVersionText`
   - `README.md`: release badge
5. Create a draft release using the PR description. Every release includes the installer and complete `app_comp_manifest.json`; plugin releases also include patch packages. The tag targets the synchronized `main` commit, or the original merge commit for plugin updates. Publishing remains manual.

The five jobs are `prepare-metadata → build-release → package-release → sync-versions → draft-release`. Plugin releases skip synchronization and continue to the draft after packaging. Installer and optional plugin packaging share one job. Keeping compilation separate allows a failed packaging job to be retried without rebuilding. The draft job writes the Actions summary.

The build artifact contains only the main application's Release output, including all plugins, rather than all of `src/`. Packaging downloads it to the same path. The component manifest is generated once and reused for release assets and plugin patches.

Component manifests are fetched with `GITHUB_TOKEN` authentication. Network, permission, download, or parsing errors stop the release. A regular release assigns a new build number to every bundled plugin. Plugin releases update only selected plugins and retain other entries from the previous published manifest.

If historical releases lack a manifest, including `v0.5.2.0` without recorded build numbers, one full release may establish a baseline. Every bundled plugin receives the new build number; old plugin versions are not guessed. Historical title/tag build numbers still reserve numbers. With no historical numbers, allocation starts at the current month's `R01`. Plugin patches require a valid baseline manifest. A corrupt existing manifest does not fall back to the no-baseline path.

The monthly sequence has two digits and stops at `R99`, avoiding older clients treating `R100` as older through string comparison. A new month starts at `R01`. A build-machine clock behind historical numbering also causes an error.

Version synchronization preserves business code and documentation added to `dev` during packaging. It does not merge all of `main`, force-reset, or overwrite branches. Concurrent remote updates, unexpected version fields, or insufficient push permissions stop synchronization and draft creation. The `.sln` contains no product version and needs no change. `app_comp_manifest.json` is generated output.

If synchronization succeeds but draft creation fails, use **Re-run failed jobs** on the original run. Restarting the whole workflow is blocked because `main` has advanced, preventing a second increment. Artifacts can overwrite earlier uploads in the same run. Manifests and release packages are retained for 30 days; full build output is retained for one day.

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

```powershell
pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1
```

This validates workflow dependencies, shared test entry points, TRX/coverage checks, retry report isolation, UI environment skips, installer reports, release metadata, and version-sync contracts. It uses isolated Git repositories and mocked GitHub responses. It does not install the application, lock the session, modify the registry, or push to a remote repository.
