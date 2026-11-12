# Security and packaging

Apply these checks only to changed surfaces or their direct consumers; do not turn a local UI review into an unrelated hardening project.

## File and Web boundaries

- Trace imported wallpaper/archive entries, selected directories, update payloads and relative metadata paths to the actual filesystem operations.
- Check path containment, volume changes, archive traversal and user-data overwrite rules. Bundled content must follow the current install/scan contract without replacing user-owned wallpaper data.
- Review WebView2 navigation, host-object exposure, message parsing and script construction when content is untrusted. Distinguish isolated user-data folders from executable/application output.
- Review named-pipe/RPC endpoints, command/process arguments, elevated install paths and external downloads for the actual trust boundary. Redact credentials and user secrets from diagnostics.

## Installed products

- Follow both EXE/Inno and MSIX paths when affected: payload creation, startup entry, plugin lookup, package identity, first-run language, same-package reinstall and uninstall.
- Keep Core as the production application entry. A process existing is weaker evidence than a successful normal lifecycle.
- Keep UI/Core/Common, installer and manifest language declarations consistent with the shared catalog. Preserve saved app language on upgrades under the current initialization contract.
- Check that generated assets are shipped, runtime assets remain available, and PDBs, test output or WebView2 profile caches do not enter release payloads.

## Workflow and release changes

Read [the current workflow guide](../../../../.github/workflows/README.md) and the edited YAML/scripts together. Do not inherit obsolete assumptions about automatic push/PR triggers or allowed UI failures.

Inspect source head/base/merge snapshot checks, required reports, skipped tests, package hashes, version/channel labels, permissions and cleanup. A green outer job is insufficient if its required raw report is missing or stale.

Version synchronization belongs to the release flow: one version commit on main, merge the updated main into dev, then the guarded atomic push. Review source contracts and retries; do not independently bump dev or run synchronization as a review action.

Build or test unfamiliar PR code only under the host's execution policy. Review text and CI evidence first; untrusted MSBuild targets and scripts execute code.
