# CI/CD workflows

[English](README.md) | [简体中文](README.zh-CN.md)

All three entry workflows are manual `workflow_dispatch` only; `package-acceptance.yml` accepts only `workflow_call`. Pushing, editing labels and merging do not dispatch them. The selected PR or source branch fixes the source; the Run workflow branch selects the workflow definition. Deploy workflow changes to the default branch before using its manual entry.

| Workflow | Input | Channels |
| --- | --- | --- |
| Pre-merge Check | Open same-repository PR number; optional ML integration | Always EXE and MSIX |
| Pre-release | Merged `dev → main` PR number | Always EXE; also MSIX when PR has `channel:msstore` |
| Manual Package | Source branch; EXE, MSIX or Both | Selected formats only; no acceptance or release |

## Manual branch packages

Open **Actions → Manual Package → Run workflow**, enter `source_branch` (default `dev`) and choose `package_format`: `EXE`, `MSIX` or `Both`. The workflow checks out that branch once, records its exact SHA, builds the Release solution and uploads `package-exe` / `package-msix` artifacts for 30 days. Both formats share the build; a failed EXE package does not prevent the selected MSIX from running. Each artifact includes `package.json` with its source SHA, version and hashes. Product versions come from the source branch without a version bump.

MSIX reuses the signed CI sideload package: each run has an isolated test identity and package version `1.0.0.0`, with a public `acceptance.cer` included for certificate trust on the test machine. The product version is recorded separately. A later run uses a new identity; these are not Store submissions or cross-version upgrade packages. Only the public certificate is uploaded; the private signing key is deleted by the packaging script.

The target machine also needs `Microsoft.VCLibs.140.00.UWPDesktop` version `14.0.30704.0` or newer, matching the existing MSIX acceptance prerequisite.

This workflow builds and packages only. It needs no PR, labels, release credentials or Partner Center configuration, and does not execute project test suites or installed/UI acceptance, write PR statuses, synchronize versions, create tags or publish drafts. The packaging scripts retain their payload checks. Its success establishes package creation, not acceptance. The source branch must contain the current .NET 10 solution and packaging scripts.

## Build and acceptance

Installed UI runners prepare a 1920×1080 session display only on disposable GitHub-hosted Windows VMs, retaining before/after evidence. Display preparation failures produce failed reports; local/self-hosted display settings and desktop eligibility rules remain unchanged. Picker submission/closure diagnostics and realized-list binding waits are described in the [testing guide](../../test/README.md#installer-smoke-and-independent-ui-acceptance).

Coverage is reported by authored product module. Canonical reports from all seven regular project suites are merged by normalized source path and line number, taking the union of hits and counting each source line once. Missing or malformed reports still block the gate. This covers instrumented product source; installed UI, script contracts and uncollected processes remain separate checks.

The shared build and test jobs use .NET 10. EXE setup provisions the x64 Desktop and ASP.NET Core runtimes for both fresh installation and silent upgrades; MSIX publishes a serviced, self-contained .NET 10 runtime for each process. Existing installer names, update routing and package identities remain unchanged.

The first release migrating from .NET 8 must be a full product release with a `version:*` label, without `update-plugins` or `update-plugin-*` labels. A plugin patch does not install the new system runtime or replace Core.

Workflow contracts validate project-file activation declarations and icon frames. MSIX packaging also checks the actual association parameters and icons; installed Explorer activation remains a desktop acceptance item.

Installed extended UI reports that claim success must contain every selected host case exactly once, including localization, organization, presets, feedback and hardware checks. Contracts compare the required policy with host registration and reject each missing or duplicate case for both EXE and MSIX. Opt-in idle tests and explicit MSIX profile stages remain separate. Controlled feedback fixtures do not replace real inference, installed application cases or clean-machine runtime provisioning.

The shared UI suite requires `ProjectActivation`, which exercises the real Core/UI project-opening path without changing daily file associations.

The shared real UI suite also registers library organization and effect-preset manager cases (see the [test inventory](../../test/README.md#wallpaper-library)). Standard/extended selection includes both; adding them does not change channel isolation or required smoke gates. Local source/compile validation is separate from executing those UI cases.

Shared stages use the same display names in both entry workflows, including `Build Release` and `Test Summary`. All run blocks default to PowerShell (`pwsh`) on both Windows and Ubuntu, including package barriers and summaries.

1. Validate the PR and freeze its source SHA/version. Pre-release also freezes its own channel selection. Build the complete Release solution once with Visual Studio MSBuild and the solution's Any CPU mappings, including both test hosts.
2. Run `package-exe` and `package-msix` independently against that build, alongside the shared project tests. EXE uses Inno Setup 6.7.1; MSIX publishes isolated self-contained outputs without changing EXE binaries. `packages-ready` requires every selected package to succeed for the final summary; it does not gate channel acceptance.
3. The eight shared project test jobs each run once after the Release build, in parallel with packaging; they do not wait for `packages-ready`. Artifact Tests consumes the build output without checking out source; expected translations and mappings are embedded in the test assembly. `tests-passed` (`Tests Passed`) exports all eight outcomes as `project_results` JSON before checking them, without checkout, artifact downloads or reruns. Summary reads this output instead of depending directly on the eight jobs, and still validates the raw reports. Each channel acceptance call waits for its own package and `tests-passed`, without waiting for the other package; failed, cancelled, skipped or missing project results block installation.
4. `test-exe-acceptance` and `test-msix-acceptance` call the same `package-acceptance.yml` template. Each call starts independent Smoke and UI Acceptance jobs without a dependency between them, giving four parallel jobs on separate fresh GitHub-hosted Windows VMs. Each job checks out the same frozen source, downloads/verifies the matching build and package, installs its own copy and uninstalls it, retaining its own reports.
5. `Summarize-PipelineTests.ps1` validates both aggregate gates, all individual project/channel results and their raw smoke/UI reports (two per channel), TRX/coverage, installer lifecycle, actual installed UI, source SHA and package SHA256. Package/project/smoke failures or skips, invalid identities and missing uploads/reports block success. Both workflows tolerate only extended UI run failures and valid environment skips, preserve the actual step outcome, and retain Failed/Skipped results instead of reporting them as passed. An unselected Store channel is `NotSelected`, never a passing test.
6. Pre-merge writes the final `pre-merge-check` status on the PR head. Pre-release commits the product version on main once after acceptance, merges that main into dev and atomically pushes both refs. `Draft Release (GitHub)` and the selected `Draft Release (Microsoft Store)` then run in parallel without depending on each other; a failed draft does not prevent the other job from executing. A final job summarizes both results.

MSIX packaging restores `~/.nuget/packages` after source checkout and before downloading build output, so generated `bin/obj` cannot affect its key. The `nuget-msix` cache includes dependency files, the SDK selection and MSIX publication settings; it falls back to prior MSIX caches and then the shared build cache. Normal restore still runs on cache misses.

Only the UI job uses job-level `continue-on-error`; UI run steps retain their failure/check status. The parent summary requires `ui_upload_result=success`, validates uploaded reports and preserves the actual `ui_run_result`; failed/missing uploads still block. If the host crashes or times out before writing a report, the runner records a Failed report with the error/available exit code and no invented case results. Existing reports are never replaced; local acceptance still fails.

The reusable workflow fixes both independent jobs to `windows-latest` and rejects a non-GitHub-hosted runner in each. Every smoke/UI job therefore owns a separate Windows VM and desktop; distinct physical cloud hosts are not promised or required. Two runner processes on the same self-hosted desktop are not an allowed substitute. Smoke and UI have separate installations/profiles and do not wait for or reuse each other. Smoke or its cleanup failure still blocks overall acceptance, while the independent UI job can finish and retain diagnostic reports. They require an active, unlocked Default desktop, Explorer, a usable display, elevation for installation/trust, runtime prerequisites and the preinstalled model assets. Unavailable desktop checks remain explicit skips: smoke blocks the gate, while an uploaded extended UI skip is non-blocking. Do not turn skips into passes to work around runner limitations.

EXE UI calls `Run-InstallerTests.ps1 -ExtendedUIReportPath ...`: it installs its own copy, runs extended UI against that installed Core, completes lifecycle checks and uninstalls. It does not borrow the smoke installation or test unpackaged Release output. Existing daily-install/AppId/shortcut guards remain mandatory.

MSIX jobs call `Run-MsixTests.ps1 -PackageArtifact ... -Mode Smoke|UI`. They consume the signed package without rebuilding, install via AppX, activate Core through its AUMID, verify process package identities and normal exits, uninstall and remove only their own certificate trust. Smoke covers automatic first UI, same-package reinstall, retained/fresh profiles and built-in wallpaper preservation; UI runs the complete extended suite on an independent installation.

The MSIX runner reuses an installed x64 VC AppX framework. Only a disposable hosted CI VM downloads/installs the missing Microsoft-signed framework; local runs require this prerequisite in advance.

`package.json` binds channel, source SHA, identity/version and package hashes. Only the public test certificate crosses jobs; the packaging job deletes its non-exportable private key. WAP signs upload/side-load packages separately: their complete application payloads must match, allowing differences only in `AppxSignature.p7x` and `AppxMetadata/CodeIntegrity.cat`. Both archive hashes are retained. Formal identity installation is permitted only on disposable GitHub-hosted CI with newly-created registration/ownership evidence; local daily Store installations remain protected.

## PR and release rules

Pre-merge accepts feature/fix branches into `dev`, and `dev` into `main`, only within this repository. `dev → main` requires exactly one of `version:major`, `version:minor`, `version:build`, `version:revision`. It verifies the current merge snapshot and rechecks PR head/base/version before reporting success. Changes to those values require a new manual check. It always validates both package channels; changing `channel:msstore` does not affect its result.

Pre-release accepts only the latest merged same-repository `dev → main` PR with exactly one version label; `main` must still point to its merge commit. It runs its own complete acceptance and does not read or require a Pre-merge workflow status. `channel:msstore` selects the channels for this release when it starts. Channel/version/plugin label changes during this release run still block version writes/drafts.

Product labels increment the corresponding component of four-part `AssemblyVersion` and reset components to its right. `update-plugins` and `update-plugin-ui/playerweb/scrsaver/ml/shaders` retain the product version, use existing plugin package/tag conventions and skip product-version synchronization. A selected Store channel still ships the complete MSIX for plugin updates.

Release metadata allocates `YYMMRxx` build numbers from published releases, drafts and component manifests. Draft numbers remain reserved; R99 is the monthly limit. A standalone `app_comp_manifest.json` remains attached to GitHub releases, including plugin updates. Legacy releases without a manifest can bootstrap a full release; plugin-only releases need a published baseline.

The Store package version is independent of `AssemblyVersion`: build `2611R02` becomes `1.2611.2.0`. Thus product revision/plugin updates also advance the Store version, whose major must be nonzero and fourth component zero. Pre-release checks the published Store baseline before packaging and again before creating its draft. A higher existing Store version blocks this mapping; do not silently downgrade or reuse it. See [Microsoft package version rules](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements).

Version synchronization writes the version fields only on `main`, creates its bump commit, then merges that updated `main` into the latest `dev` with `--no-ff`. This brings the exact main version commit and any main-only changes back to dev while preserving dev history. Only after both local histories are ready does one atomic push advance `main` and `dev`; no force push or protection bypass is used. Conflicting text hunks favor main (`-X theirs`); remaining conflicted paths use the main file version or its deletion. Nonconflicting dev changes remain, including separate hunks in the same text file. This does not replace the entire dev tree with main. Git-operation failures abort the pending merge, and push rejection leaves both remote refs unchanged and blocks drafts. Retries reuse the main bump and do not duplicate an already completed merge. `RELEASE_PAT` must have permission under repository rules.

## Separate release drafts

Pre-release selects all Store operations using the merged PR's exact `channel:msstore` label at the start of this release, not a Git tag. Without it, Partner Center validation, MSIX packaging/installed acceptance and the Store draft job are skipped; no Store credentials are required. The two draft jobs share the acceptance/version-sync barrier and have no cross-channel dependency. Selected package/acceptance failures still block that common barrier.

GitHub retains the existing EXE/plugin release tag and update assets. No additional MSIX-only GitHub Release is created, so existing clients' release selection remains unchanged. Drafts are not public and are not automatically marked latest.

For `channel:msstore`, `Invoke-StoreDraft.ps1` creates/updates a Partner Center MSIX submission, merges reviewed local copy/release notes/desktop screenshots, preserves reserved titles, unmanaged listing data, pricing/declarations and other architectures, replaces the previous x64 package and uploads the validated Store archive with referenced images. It leaves `targetPublishMode=Manual` and `status=PendingCommit`: no submission commit, certification request or publication occurs. Upload success is not certification success.

Store retries reuse only an uncommitted draft owned by the same repository/workflow run/source SHA, package hash and material hash. Unrelated/manual submissions and already committed submissions are rejected, never deleted. If creation succeeds but ownership cannot be recorded, resolve the pending draft explicitly before retrying. Once an API-owned submission exists, continue managing it through the API; Microsoft's API warns against editing it in Partner Center. See [submission API guidance](https://learn.microsoft.com/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services).

The unified maintenance standard is [resources/store](../../resources/store/README.md): current copy in `listings/<locale>.json`, notes in `releases/<calculated-product-version>.json`, images and sharing/provenance in `screenshots/`. Preparation validates them early; the draft job exports the same committed snapshot and retains the readable `store-materials` artifact. First-submission release notes are automatically blank. `Export-StoreMaterials.ps1 -Version <product-version> -InitialRelease` also provides an offline handoff without remote writes. Updated copy/images need a new reviewed source snapshot; rerunning an old job keeps its original materials.

## Additional checks and version labels

Pre-merge excludes ML tests categorized as `Integration` by default; select **Also run real model inference tests** to include them. Pre-release always includes them. These regular ML tests require model files and explicitly skip when files are missing; they do not download models. Selected integration cases must execute without skips or the workflow fails; the extended UI ModelInference case fails directly if its model is missing. To run the regular integration category locally:

```powershell
dotnet test src/VirtualPaper.ML.Test/VirtualPaper.ML.Test.csproj --configuration Release --filter "TestCategory=Integration" --settings src/test.runsettings
```

Both package channels also require all four `HardwareAcceleration` UI cases in extended acceptance: deferred startup-policy changes, software Web/drawing flows, and real CPU/GPU-requested super-resolution plus style enhancement. The models must be packaged, including AdaIN external weights. Reports distinguish successful GPU completion from CPU fallback; fallback is valid behavior on unsupported hardware and must not be presented as successful GPU execution. See the [hardware UI acceptance guide](../../test/README.md#hardware-acceleration).

Closed, merged, or fork PR inputs are rejected without altering prior check status. For a valid open PR, the workflow sets `pre-merge-check: pending` on its head SHA. Before setting success/failure, it rechecks the PR commit, base, and version label. The status belongs to the PR commit, not the branch selected in the dispatch form. See the [GitHub commit status API](https://docs.github.com/en/rest/commits/statuses).

After a new PR commit, rerun manually: success on an earlier SHA does not satisfy the new commit's required check. Label changes do not start runs; rerun Pre-merge after changing the version label. Release channel/plugin labels are selected independently by Pre-release and do not require rerunning Pre-merge.

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

Product version synchronization changes only `AssemblyVersion` in `src/VirtualPaper/VirtualPaper.csproj`, `MyAppVersion`/`MyAppVersionText` in `packaging/inno/setup.iss`, and the release badge in `README.md`. The `.sln` has no product version; `app_comp_manifest.json` is generated output. The main bump is merged into dev; no independent dev bump is created. The next dev-to-main merge retains that shared release history.

The component manifest is generated once and reused for release assets and plugin patches. Fetch it with `GITHUB_TOKEN`; network, permission, download and parsing errors stop the release. A full release may establish a baseline when historical manifests are absent. Plugin patches require a valid published baseline; a corrupt existing manifest must not use the no-baseline path. A regular release advances every bundled plugin build number, while a plugin release advances only selected plugins. Monthly numbering stops at R99, resets at a new month and rejects a build clock behind historical numbering.

If version synchronization or one draft succeeds and a later draft fails, use **Re-run failed jobs** in the original run. Matching GitHub drafts/Store drafts are reusable; the workflow does not roll back external state. Do not restart the complete workflow after version synchronization advanced `main`.

## Repository configuration

Configure these before dispatching Pre-release with `channel:msstore`. Missing values fail preparation before any package/version/draft writes; Pre-merge needs none of them and uses a unique test identity.

Fill the four public identifiers in [packaging/msix/StoreIdentity.json](../../packaging/msix/StoreIdentity.json). The local Store packager, CI packager and submission API share this default. Existing `MSSTORE_*` environment variables remain optional overrides; keep authentication credentials outside the JSON.

| Setting | Type | Value |
| --- | --- | --- |
| `StoreId` / `MSSTORE_APPLICATION_ID` | JSON / optional repository variable override | Product Store ID |
| `IdentityName` / `MSSTORE_IDENTITY_NAME` | JSON / optional repository variable override | Package/Identity/Name |
| `Publisher` / `MSSTORE_PUBLISHER` | JSON / optional repository variable override | Package/Identity/Publisher |
| `PublisherDisplayName` / `MSSTORE_PUBLISHER_DISPLAY_NAME` | JSON / optional repository variable override | PublisherDisplayName |
| `MSSTORE_TENANT_ID` | Repository variable | Entra tenant GUID |
| `MSSTORE_CLIENT_ID` | Repository variable | Associated application GUID |
| `MSSTORE_CLIENT_SECRET` | Repository secret | Application key with Partner Center Manager permissions |

- For `main`, require a PR and the `pre-merge-check` status. If `dev` should also require PRs, configure the same protection there. If direct pushes to `dev` are allowed, they do not trigger these manual workflows; Pre-merge Check still requires an open PR.
- Remove obsolete required checks such as `Check Source Branch` and `ci-check/pre-publish-tests`; the new workflows do not provide them.
- Consider requiring the PR branch to be up to date with its base, and rerun checks after base changes.
- `RELEASE_PAT` must allow the version-sync bot to update both `main` and `dev` under the repository's rules. Rules for `dev` must allow the main-to-dev merge commit; a linear-history rule needs an appropriate bot exemption or adjustment. Insufficient permissions fail synchronization; the workflow does not force-push around protection.
- Create the four fixed `version:*` labels once.
- Local workflow changes do not delete old remote branches such as `release` or `bugfix`. Clean them up separately after checking for unmerged work.

Keep the four `version:*` labels and add `channel:msstore`. Branch protection must require `pre-merge-check` (remove obsolete status names separately). Workflows do not change repository labels/rules remotely. Do not modify/release matching tags while a workflow is running.

## Reports and local checks

Workflow contracts also run `test/Test-LegalDocuments.ps1` for all supported offline legal translations, section IDs, links, UI labels and original notices. Artifact tests verify the shipped document bytes; MSIX validation requires the complete legal payload. Configure the public privacy-policy URL separately in Partner Center after publishing the documents.

Cross-version upgrades use the independent local `test/Run-UpgradeTests.ps1` entry with two source/package snapshots and phase-matched test tools. They do not replace mandatory same-package smoke or dispatch either workflow. Regional-format/font cases join the existing real UI inventory; see the [testing guide](../../test/README.md) for preparation and screenshot review.

Raw reports use `test-results-*`; bundles use `test-summary-*`/`release-test-summary`. Summary JSON/Markdown retain channel outcomes and source SHA; package manifests retain versions/hashes. `store-draft.json` records the submission ID, uploaded archive/package/material hashes, product version, listing locales and `Committed=false`, without access tokens or SAS URLs. Package/draft/summary artifacts last 30 days; build/raw reports last 7 days. Artifact uploads use overwrite on retries; downloads validate digests.

Run `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1` for the DAG (packages → all project jobs → channel calls), fixed hosted-VM isolation, sequential smoke/UI, strict missing/failed/skipped reports, artifact payloads, metadata/version synchronization, plugin package and Store HTTP-mock/retry contracts. It does not install applications or contact Partner Center/GitHub. Local complete acceptance remains `test/Run-Tests.ps1` for EXE and `test/Run-MsixTests.ps1` for MSIX; see the [testing guide](../../test/README.md).

The same validator runs localization-generation contracts: catalog/key/placeholder consistency, UI/Core/Common/installer/MSIX outputs, a synthetic third language, stale-output detection and source protection. Both packaging channels consume the [shared language catalog and translations](../../localization/README.md). Actual package checks inspect application translations and MSIX language declarations; framework satellite folders alone do not establish product translation coverage.

Localization payload contracts also reject incomplete/stale UI dictionaries and missing Core/Common resources. The artifact runner checks actual Release resources before TRX execution; installed EXE and final MSIX checks use the same strict validator. Real UI cases traverse every catalog language and add German/French/Russian layout samples. The optional French installer run is local/manual; it does not change the required default English/Chinese CI smoke sequence.
