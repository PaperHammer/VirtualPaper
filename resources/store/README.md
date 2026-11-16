# Microsoft Store materials

[English](README.md) | [简体中文](README.zh-CN.md)

This directory is the source of reviewed Store copy and screenshots. Pre-release consumes the committed materials from its fixed source snapshot and uploads them with the accepted MSIX to an **uncommitted** Partner Center draft.

## Where to edit

| Content | Source | Maintenance rule |
| --- | --- | --- |
| Current descriptions, features, keywords, screenshot order/captions | `listings/<locale>.json` | Edit the relevant languages; keep every catalog language present. |
| Version-specific release notes | `releases/<product-version>.json` | Add the exact four-part product version calculated by the release label, with every catalog language. This is not the MSIX package version. |
| Actual UI images | `screenshots/<locale>/<NN-scene>.png` | Replace the existing scene file when its appearance changes. Keep unchanged images. |
| App icon variants | [`artwork/`](artwork/README.md) | Text-free transparent/tile SVG and 300/512/1024 px PNG exports from the shared application vector icon. |
| Store Hero background | `artwork/store-hero-3840x2160.png` | Text-free, soft-focus 4K 16:9 background, with its SVG composition source. Separate from the screenshot set. |
| Scene references, explicit English sharing, capture provenance | `screenshots/index.json` | Register new scenes and localized overrides here. Add matching scene IDs/captions to each listing. |
| Supported languages | [`localization/LanguageCatalog.json`](../../localization/LanguageCatalog.json) | A new language also needs listing copy, release notes and image references or shared images. |
| Public Store identity | [`packaging/msix/StoreIdentity.json`](../../packaging/msix/StoreIdentity.json) | Authentication secrets stay outside the repository. |

Before a release PR is merged, prepare notes for the version that Pre-release will calculate. A plugin-only release keeps the product version and uses that version's notes. Review translations and images in the PR with the code they describe; CI does not invent translations or capture screenshots.

The current `0.6.1.0` set has ten localized listings and release notes, seven captions per language and 21 unique screenshot PNGs. The first image in every listing is `00-overview`, rendered at 1920 × 1200 from [`resources/readme/hero.svg`](../readme/hero.svg). It retains that existing composition's branding and real English light/dark library captures. The six following scenes remain unchanged: six scenes each in English and Simplified Chinese, plus the library in the other eight languages. Shared English images do not claim a translated interface.

## Image standard and capture

Use actual Release-build windows for feature screenshots, PNG, at least 1366 × 768, no more than 50 MB per image. Our inventory requires 4–10 scenes per listing (currently seven). Captions are limited to 200 characters. Keep original pixels; do not enlarge a small capture. No pointer, pointer highlight, desktop notification, user library, account, private path or additional marketing overlay may appear in these feature captures. The first overview is the separately identified existing README composition; its source/hash and original capture date are recorded per scene in `screenshots/index.json`.

Feature captures and the new Hero background use bundled VirtualPaper-generated or CC0 media, with notices in [`resources/BuiltInWallpapers`](../BuiltInWallpapers). The first overview retains the existing README captures; their Unsplash image sources and rights are documented in the [README artwork guide](../readme/README.md#screenshot-artwork-sources). Capture provenance is recorded in `screenshots/index.json`.

Regenerate the Hero and overview without taking the foreground:

```powershell
python resources/tools/Generate-StoreArtwork.py
```

Requires Python 3.10+, Pillow, websocket-client and Chrome or Edge. The tool uses a private headless profile under `TestResults`, renders the overview without changing `hero.svg`, and updates its recorded source hash. Run it again after changing the README hero or regenerating branding. The Hero has no text or application UI and uses blue-violet haze, bundled wallpapers and translucent paper folds. It is stored alongside the icons for the Store's **16:9 Super hero art** field; the current draft automation continues to preserve existing Store artwork. See the [official image guidance](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images).

Build the complete Release solution, then start a disposable profile on an unlocked desktop:

```powershell
$preview = & resources/tools/Start-StorePreview.ps1 -Locale en-US
# Wait for the UI child process to appear before resolving its PID.
$uiId = (Get-CimInstance Win32_Process -Filter "ParentProcessId=$($preview.CoreId)" |
    Where-Object Name -EQ 'VirtualPaper.UI.exe').ProcessId
# Arrange the actual UI and wait for loading/animations, then:
& resources/tools/Capture-StoreScreenshot.ps1 -ProcessId $uiId `
    -Destination resources/store/screenshots/en-US/01-library.png
# Secondary window example: add -Title 'Alpine Stillness (Preview)'.
```

The preview uses real Core/UI, isolated data/named pipes, bundled wallpapers and sample projects. It does not install or uninstall. Close only that preview and its Core when finished. The capture tool reuses the test `PrintWindow` exporter without the pointer and trims only the invisible DWM frame, without rescaling or image repair. Review every output, especially WebView/GPU content. Screen-capture exports containing a pointer/highlight are unsuitable as final assets.

## Offline review and handoff

```powershell
pwsh -NoProfile -File resources/tools/Export-StoreMaterials.ps1 -Version 0.6.1.0 -InitialRelease
```

Output under `TestResults/store-materials/` contains readable Markdown per locale, `listings.json`, images and a hash manifest. This command performs no login/upload. For later updates omit `-InitialRelease`; the switch leaves Store release notes blank on the first submission but retains versioned notes in readable documents.

`listings.json` is a partial metadata patch, not a complete submission or a Partner Center CSV import. Exported image names contain their content hash; repo scene names remain stable. `SourceSha` identifies the release input snapshot; `ScreenshotSourceSha` identifies the build used for the reviewed images. Old images can be reused while their UI still represents the release.

## Pre-release behavior

With the checked PR's exact `channel:msstore` label, preparation validates the selected product-version notes, all catalog listings and image inputs. Without that label, Store configuration validation, MSIX packaging/installed acceptance and the Store draft job are skipped. After all required package/test acceptance and version synchronization passes, `Draft Release (GitHub)` and `Draft Release (Microsoft Store)` execute in parallel without cross-channel dependencies. `draft-msstore` exports those same committed inputs and uploads a ZIP containing the accepted `.msixupload` plus referenced images. The readable export is retained as the `store-materials` artifact.

For each managed locale the draft updates descriptions, features, keywords, release notes and the ordered desktop screenshot set. This screenshot set is authoritative: obsolete desktop screenshots are removed from that locale's draft. Unchanged uploaded image IDs are reused. Reserved titles, other locales, platform overrides, logos, legal fields, declarations, pricing and other package architectures remain preserved. New locales use the existing reserved product title.

The ownership marker binds the workflow run, source, package hash and material hash. An unchanged failed job can retry its own `PendingCommit` draft; changed materials are rejected before updating it. Changing copy/images requires a new reviewed source snapshot and release run rather than rerunning an old snapshot. A final API read verifies managed text and image order. `store-draft.json` records hashes and `Committed=false` without credentials/upload URLs.

The workflow never commits a Store submission, requests certification or publishes. Keep an API-owned draft managed through the API rather than editing the same submission in the portal. Age ratings, markets, price, support/contact details and the public privacy-policy URL remain separate Partner Center configuration.

Run `pwsh -NoProfile -File .github/scripts/validate-test-workflow.ps1` for offline material, workflow and HTTP-mock contracts. These checks do not contact Partner Center, capture UI or install applications.

Sources: [listing fields](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info), [screenshot requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images), [submission API](https://learn.microsoft.com/windows/uwp/monetize/manage-app-submissions).
