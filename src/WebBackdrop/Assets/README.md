# Monaco runtime

[English](README.md) | [简体中文](README.zh-CN.md)

VirtualPaper ships Monaco 0.55.1 with its vendored DOMPurify rebuilt at 3.4.17. The editor API and AMD loader stay on 0.55.1. Updating only `package.json` does not update the sanitizer embedded in the JavaScript bundles.

To reproduce the security patch from the repository root (PowerShell 7.2+, Node.js 22.12+ and npm):

```powershell
pwsh -NoProfile -File .github/scripts/rebuild-monaco.ps1
```

The script verifies the upstream source archive and DOMPurify package checksums, restores upstream lockfiles with installation scripts disabled, replaces the core package's vendored sanitizer input, and uses upstream's LSP and AMD builds. It stages the runtime under a new ignored `TestResults/monaco-rebuild-*/runtime/package` directory; it does not change tracked assets automatically.

Replace the tracked `monaco-editor-0.55.1/package/min` directory with the staged `min` directory, and copy the staged `package.json` and `DOMPurify.LICENSE`. Remove old hashed bundles instead of combining two builds. Keep the Monaco license and third-party notices. Do not commit the upstream checkout, `node_modules`, ESM sources or archives.

Rebuild the complete Release solution, run Core/Web/artifact project tests, and verify the real Web editor opens, edits, saves and reopens a project. Confirm the packaged bundle contains DOMPurify 3.4.17, rather than checking metadata alone. When adopting a newer upstream Monaco, inspect its actual bundled sanitizer version before removing this patch.
