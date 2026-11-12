# Tests and documentation

Use [testing](../../testing/SKILL.md) and [the testing guide](../../../../test/README.md) for current runners, job IDs and reporting contracts.

- Match the assertion to the failure: actual saved state, bytes/identity, selected value, process exit or visible behavior. A success flag, non-null result or unchanged fixture value can pass while the requested action never occurred.
- Cover relevant failures and compatibility transitions, including malformed/empty metadata, old paths/versions, cancellation, reinstall preservation and built-in content not overwriting user data when those areas change.
- Keep temporary fixtures isolated. Never write tests against a user's daily settings, installation or wallpaper library.
- Pure decision logic belongs in headless projects. Actual WinUI layout, focus, flyouts, input, theme synchronization and tray behavior require the real UI layer. Do not treat mocked backend responses as installed acceptance.
- Verify fixture registration and expected execution counts when adding real UI cases. Read fresh TRX/JSON outcomes and skipped/unhandled-exception fields, not only a zero process exit.
- Coverage percentages describe the instrumented source set. Keep exclusions honest; generated code and real UI behavior are different concerns. Prefer missing behavior coverage over chasing trivial lines.
- For a regression fix, look for an assertion that would fail before the fix. Use a bounded negative control when the oracle is uncertain; do not mutate the user's dirty production files just to manufacture a failing run.

Maintain translation text only in localization/Strings and language metadata in localization/LanguageCatalog.json. Review mappings and generated consumers, placeholders, completeness and live language updates. Do not hand-edit obj output or validate only the original two languages.

Update the English/Chinese documentation pair when the changed behavior affects an existing paired guide. Read the current file before editing; preserve human changes. User-facing release notes explain capabilities and behavior rather than implementation history.

Documentation-only and reversible visual edits do not need a full regression by default. If the user excluded tests, review their adequacy statically and report that execution was deferred.
