# CI/CD workflows

[English](README.md) | [简体中文](README.zh-CN.md)

Both entry workflows are manual `workflow_dispatch` only; `package-acceptance.yml` accepts only `workflow_call`. Pushing, editing labels and merging do not dispatch them. The selected PR fixes the tested source; the Run workflow branch selects the workflow definition. Deploy workflow changes to the default branch before using its manual entry.

| Workflow | Input | Channels |
| --- | --- | --- |
| Pre-merge Check | Open same-repository PR number; optional ML integration | Always EXE and MSIX |
| Pre-release | Merged `dev → main` PR number | Always EXE; also MSIX when PR has `channel:msstore` |

## Build and acceptance

Shared stages use the same display names in both entry workflows, including `Build Release` and `Test Summary`. All run blocks default to PowerShell (`pwsh`) on both Windows and Ubuntu, including package barriers and summaries.

1. Validate the PR and freeze its source SHA/version/channel selection. Build the complete Release solution once with Visual Studio MSBuild and the solution's Any CPU mappings, including both test hosts.
2. Run `package-exe` and `package-msix` independently against that build. EXE uses Inno Setup 6.7.1; MSIX publishes isolated self-contained outputs without changing EXE binaries. `packages-ready` requires every selected package to succeed.
3. After all selected packages succeed, run the eight shared project test jobs once, in parallel. Both channel acceptance calls wait for all eight jobs to pass; a failure or skip blocks installation.
4. `test-exe-acceptance` and `test-msix-acceptance` call the same `package-acceptance.yml` template on separate fresh GitHub-hosted Windows VMs. Within each VM, run smoke through uninstall/cleanup, then independently reinstall the same artifact for extended UI and uninstall/cleanup. The channels may run in parallel; smoke and UI within a channel are sequential.
5. `Summarize-PipelineTests.ps1` validates selected channel results and their raw smoke/UI reports (two per channel), TRX/coverage, installer lifecycle, actual installed UI, source SHA and package SHA256. Missing/failed/skipped required checks block success. UI is required in both workflows. An unselected Store channel is `NotSelected`, never a passing test.
6. Pre-merge writes the final `pre-merge-check` status on the PR head. Pre-release commits the product version on main once after acceptance, merges that main into dev and atomically pushes both refs, then independently creates the GitHub EXE draft and selected Partner Center draft. A final job summarizes both draft results.

The reusable workflow fixes its single job to `windows-latest` and rejects a non-GitHub-hosted runner. Each channel therefore owns a separate Windows VM and desktop; distinct physical cloud hosts are not promised or required. Two runner processes on the same self-hosted desktop are not an allowed substitute. Smoke and UI use separate installations/profiles sequentially within each VM; smoke cleanup failure blocks UI. They require an active, unlocked Default desktop, Explorer, a usable display, elevation for installation/trust, runtime prerequisites and the preinstalled model assets. Unavailable desktop checks remain explicit skips and fail the gate. Do not turn skips into passes to work around runner limitations.

EXE UI calls `Run-InstallerTests.ps1 -ExtendedUIReportPath ...`: it installs its own copy, runs extended UI against that installed Core, completes lifecycle checks and uninstalls. It does not borrow the smoke installation or test unpackaged Release output. Existing daily-install/AppId/shortcut guards remain mandatory.

MSIX jobs call `Run-MsixTests.ps1 -PackageArtifact ... -Mode Smoke|UI`. They consume the signed package without rebuilding, install via AppX, activate Core through its AUMID, verify process package identities and normal exits, uninstall and remove only their own certificate trust. Smoke covers automatic first UI, same-package reinstall, retained/fresh profiles and built-in wallpaper preservation; UI runs the complete extended suite on an independent installation.

The MSIX runner reuses an installed x64 VC AppX framework. Only a disposable hosted CI VM downloads/installs the missing Microsoft-signed framework; local runs require this prerequisite in advance.

`package.json` binds channel, source SHA, identity/version and package hashes. Only the public test certificate crosses jobs; the packaging job deletes its non-exportable private key. WAP signs upload/side-load packages separately: their complete application payloads must match, allowing differences only in `AppxSignature.p7x` and `AppxMetadata/CodeIntegrity.cat`. Both archive hashes are retained. Formal identity installation is permitted only on disposable GitHub-hosted CI with newly-created registration/ownership evidence; local daily Store installations remain protected.

## PR and release rules

Pre-merge accepts feature/fix branches into `dev`, and `dev` into `main`, only within this repository. `dev → main` requires exactly one of `version:major`, `version:minor`, `version:build`, `version:revision`. It verifies the current merge snapshot and rechecks PR head/base/version/channel before reporting success. Changes require a new manual check.

Pre-release accepts only the latest merged same-repository `dev → main` PR. Its head must have a successful matching Pre-merge status for the same PR, version label and Store channel choice; `main` must still point to its merge commit. Channel/version/plugin label changes during the run block version writes/drafts. Existing successful statuses from the earlier workflow format must be refreshed.

Product labels increment the corresponding component of four-part `AssemblyVersion` and reset components to its right. `update-plugins` and `update-plugin-ui/playerweb/scrsaver/ml/shaders` retain the product version, use existing plugin package/tag conventions and skip product-version synchronization. A selected Store channel still ships the complete MSIX for plugin updates.

Release metadata allocates `YYMMRxx` build numbers from published releases, drafts and component manifests. Draft numbers remain reserved; R99 is the monthly limit. A standalone `app_comp_manifest.json` remains attached to GitHub releases, including plugin updates. Legacy releases without a manifest can bootstrap a full release; plugin-only releases need a published baseline.

The Store package version is independent of `AssemblyVersion`: build `2611R02` becomes `1.2611.2.0`. Thus product revision/plugin updates also advance the Store version, whose major must be nonzero and fourth component zero. Pre-release checks the published Store baseline before packaging and again before creating its draft. A higher existing Store version blocks this mapping; do not silently downgrade or reuse it. See [Microsoft package version rules](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements).

Version synchronization writes the version fields only on `main`, creates its bump commit, then merges that updated `main` into the latest `dev` with `--no-ff`. This brings the exact main version commit and any main-only changes back to dev while preserving dev history. Only after both local histories are ready does one atomic push advance `main` and `dev`; no force push or protection bypass is used. Conflicting text hunks favor main (`-X theirs`); remaining conflicted paths use the main file version or its deletion. Nonconflicting dev changes remain, including separate hunks in the same text file. This does not replace the entire dev tree with main. Git-operation failures abort the pending merge, and push rejection leaves both remote refs unchanged and blocks drafts. Retries reuse the main bump and do not duplicate an already completed merge. `RELEASE_PAT` must have permission under repository rules.

## Separate release drafts

GitHub retains the existing EXE/plugin release tag and update assets. No additional MSIX-only GitHub Release is created, so existing clients' release selection remains unchanged. Drafts are not public and are not automatically marked latest.

For `channel:msstore`, `Invoke-StoreDraft.ps1` creates/updates a Partner Center MSIX submission, preserves listing/pricing/declarations and other architectures, replaces the previous x64 package and uploads the validated Store archive. It leaves `targetPublishMode=Manual` and `status=PendingCommit`: no submission commit, certification request or publication occurs. Upload success is not certification success.

Store retries reuse only an uncommitted draft owned by the same repository/workflow run/source SHA and package hash. Unrelated/manual submissions and already committed submissions are rejected, never deleted. If creation succeeds but ownership cannot be recorded, resolve the pending draft explicitly before retrying. Once an API-owned submission exists, continue managing it through the API; Microsoft's API warns against editing it in Partner Center. See [submission API prerequisites](https://learn.microsoft.com/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services).

If version synchronization or one draft succeeds and a later draft fails, use **Re-run failed jobs** in the original run. Matching GitHub drafts/Store drafts are reusable; the workflow does not roll back external state. Do not restart the complete workflow after version synchronization advanced `main`.

## Repository configuration

Configure these before dispatching Pre-release with `channel:msstore`. Missing values fail preparation before any package/version/draft writes; Pre-merge needs none of them and uses a unique test identity.

| Setting | Type | Value |
| --- | --- | --- |
| `MSSTORE_APPLICATION_ID` | Repository variable | Product Store ID |
| `MSSTORE_IDENTITY_NAME` | Repository variable | Package/Identity/Name |
| `MSSTORE_PUBLISHER` | Repository variable | Package/Identity/Publisher |
| `MSSTORE_PUBLISHER_DISPLAY_NAME` | Repository variable | PublisherDisplayName |
| `MSSTORE_TENANT_ID` | Repository variable | Entra tenant GUID |
| `MSSTORE_CLIENT_ID` | Repository variable | Associated application GUID |
| `MSSTORE_CLIENT_SECRET` | Repository secret | Application key with Partner Center Manager permissions |

Reserve the product and complete the initial Partner Center submission setup, including age ratings, before using the submission API. Product creation itself is manual. Complete WACK, clean-machine ordinary-user prerequisites, cross-version upgrades and other Store requirements before actual submission; automated draft creation does not establish those checks.

Keep the four `version:*` labels and add `channel:msstore`. Branch protection must require `pre-merge-check` (remove obsolete status names separately). Workflows do not change repository labels/rules remotely. Do not modify/release matching tags while a workflow is running.

## Reports and local checks

Raw reports use `test-results-*`; bundles use `test-summary-*`/`release-test-summary`. Summary JSON/Markdown retain channel outcomes and source SHA; package manifests retain versions/hashes. `store-draft.json` records the submission ID, uploaded archive/package hashes and `Committed=false`, without access tokens or SAS URLs. Package/draft/summary artifacts last 30 days; build/raw reports last 7 days. Artifact uploads use overwrite on retries; downloads validate digests.

Run `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1` for the DAG (packages → all project jobs → channel calls), fixed hosted-VM isolation, sequential smoke/UI, strict missing/failed/skipped reports, artifact payloads, metadata/version synchronization, plugin package and Store HTTP-mock/retry contracts. It does not install applications or contact Partner Center/GitHub. Local complete acceptance remains `test/Run-Tests.ps1` for EXE and `test/Run-MsixTests.ps1` for MSIX; see the [testing guide](../../test/README.md).
