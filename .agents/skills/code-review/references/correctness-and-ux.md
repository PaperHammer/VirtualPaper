# Correctness, UI and architecture

Use the concrete entry points in AGENTS.md and the changed module's implementation.

## Ownership and lifecycle

- Core owns services, tray and player lifecycle. UI startup and installed acceptance use its production launch path; directly opening the UI can bypass required initialization.
- Trace closing from the main window, tray and editor/AI dialogs. Check cancellation versus confirmation, unsaved work, completed temporary AI outputs, pending RPCs and normal process exit.
- Distinguish application shutdown, hiding a window, child-window disposal and navigation. An Unloaded event may represent temporary detachment; do not release shared services as if the application ended.
- Check event subscriptions, timers, cancellation registrations, WebView2/composition objects and queued continuations for bounded lifetime. A high memory reading alone is not proof of a leak.

## Async work and state

- Check UI-thread access, cancellation propagation, async initialization reuse and late completion after navigation or disposal.
- Verify serialization where settings, metadata, index maps or queued scripts share mutable state. Keep expensive I/O outside locks; inspect whether high-frequency state updates can be coalesced without losing required operations.
- Review cache invalidation with actual filters, culture, organization and directory changes. Preserve visible selection, scroll and existing item instances where the requested refresh allows it.
- Trace failure paths in migrations and multi-step writes. Originals must survive copy/switch failures; cleanup after a committed migration must not restore already-removed sources. Check metadata versions and absolute/relative path compatibility against current loaders.

## Layout and usability

- Use the user's current layout intent. Fixed-row task actions, compact navigation or a requested description height must not be silently replaced by a different responsive interaction.
- Inspect real measure constraints: horizontal StackPanel and Auto columns can leave text unbounded; wrapping and ellipsis need a bounded text area. Inspect button padding, min/max size, alignment, scrolling and text scaling together.
- Check Russian/German/French or another long translation, smaller windows, light/dark theme and dynamic language changes. Preserve preview image aspect ratio.
- Keep short navigation captions separate from full tooltips and accessibility names when shortening is needed. Use established local abbreviations, not arbitrary truncation of translation source text.
- Confirm labels, commands, disabled state and automation IDs still identify the same action. Do not expose internal implementation details as product text.

## Existing boundaries

Prefer current MVVM/service contracts, storage-picker adapters, FileUtil operations and metadata migration/index owners. Shared UI behavior belongs in UIComponent or the relevant feature according to its dependencies; core logic should not acquire WinUI control dependencies just to make a test convenient.
