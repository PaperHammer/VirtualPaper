<p align="center">
  <img src="resources/readme/hero.svg" alt="VirtualPaper with its real library in light and dark themes" width="100%" />
</p>

<p align="center">
  <strong>VirtualPaper is a free, open-source wallpaper application for Windows.</strong><br />
  Supports images, videos, animated images, and interactive Web wallpapers.<br />
  Includes atmosphere effects, 3D parallax, and built-in image editing.
</p>

<p align="center">
  <a href="https://github.com/PaperHammer/VirtualPaper/releases"><img src="https://img.shields.io/badge/release-v0.6.0.0-blue?style=flat-square" alt="Release" /></a>
  <a href="#download"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-5865B8?style=flat-square" alt="Windows 10 and 11" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-7862C8?style=flat-square" alt="GPL-3.0 license" /></a>
  <a href="https://github.com/PaperHammer/VirtualPaper/stargazers"><img src="https://img.shields.io/github/stars/PaperHammer/VirtualPaper?style=flat-square&amp;color=7862C8" alt="GitHub stars" /></a>
</p>

<p align="center">
  <a href="#features">Features</a> ·
  <a href="#download">Download</a> ·
  <a href="https://github.com/PaperHammer/VirtualPaper/wiki">Documentation</a> ·
  <a href="#support">Support</a>
</p>

## Features

### Live wallpapers

Use your own images, GIFs, videos, and Web wallpapers. Keep them together in your library, preview a wallpaper, and apply it to your desktop.

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>🖼️ Images, videos, and more</h3>
      Choose a still image, play an animated wallpaper, or use an interactive Web wallpaper.
    </td>
    <td width="50%" valign="top">
      <h3>🌌 3D wallpapers</h3>
      Give a picture a sense of depth. Let the view follow your mouse or move slowly on its own, and adjust how much it moves.
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <h3>🖥️ Multiple displays</h3>
      Use a different wallpaper on each display, show the same one on every display, or stretch one wallpaper across them all.
    </td>
    <td width="50%" valign="top">
      <h3>✨ Fit your screen</h3>
      Preview how a picture fills your display. Keep the whole image or crop to fill, and optionally enhance small images before applying them.
    </td>
  </tr>
</table>

### Supported formats

| Content | Formats |
| --- | --- |
| Still images | `.jpg`, `.jpeg`, `.bmp`, `.png`, `.svg`, `.webp` |
| Animated images | `.gif`, `.apng` |
| Video | `.mp4`, `.webm` |
| Web wallpaper packages | `.zip`, `.rar`, `.7z` containing a supported Web wallpaper project |
| Editable projects | `.vpd` for images; `.vpw` for Web projects |

Import wallpaper packages through the library. Open editable `.vpd` image projects and `.vpw` Web projects in the creation workspace.

### Parallax effects

Choose **regular parallax** to tilt the whole picture with your mouse, or **depth parallax** to move foreground and background by different amounts. Both views below use the same image and mouse movement; depth parallax uses the Standard preset.

<a href="resources/readme/parallax.gif"><img src="resources/readme/parallax.gif" alt="Synchronized comparison of regular whole-image tilt on the left and Standard depth parallax on the right, using the same green hills photograph" width="960" /></a>

Depth is estimated locally. Depth wallpapers also support slow automatic movement.

### Atmosphere effects

Add **stardust, snow, fireflies, meteors, rain, petals, or bokeh** to your wallpaper. Choose a style and adjust the intensity. The effects work with images, videos, 3D wallpapers, and Web wallpapers.

<table>
  <tr>
    <td width="50%" align="center">
      <img src="resources/readme/snow.gif" alt="Snowflakes drifting across an illustrated mountain lake" width="400" /><br />
      <h3>Snow</h3>
    </td>
    <td width="50%" align="center">
      <img src="resources/readme/fireflies.gif" alt="Glowing fireflies moving and fading over the same landscape" width="400" /><br />
      <h3>Fireflies</h3>
    </td>
  </tr>
  <tr>
    <td width="50%" align="center">
      <img src="resources/readme/meteors.gif" alt="Meteors with glowing trails crossing the night sky" width="400" /><br />
      <h3>Meteors</h3>
    </td>
    <td width="50%" align="center">
      <img src="resources/readme/petals.gif" alt="Pink petals gently falling and turning in front of the landscape" width="400" /><br />
      <h3>Petals</h3>
    </td>
  </tr>
</table>

### Screen saver

Keep your wallpaper on screen when you step away. Use your primary display's wallpaper as a screen saver, choose when it starts, and exclude apps that should keep it from appearing.

### Playback controls

- Choose whether wallpapers keep playing or pause when a fullscreen or focused app is running.
- Set separate playback rules for battery power, power saving, and Remote Desktop.
- Adjust wallpaper settings and effects from the library.

### Create and customize

- **Edit pictures.** Work with layers, brushes, selections, and image effects.
- **Make Web wallpapers.** Edit your project and see the result in a live preview.
- **Improve image quality.** Enlarge small images, restore details, or try a different look with reference-image style transfer.
- **Match your desktop.** Choose a light or dark interface and set a library image as your Windows lock screen background.

<img src="resources/readme/editor-light-mica.jpg" alt="VirtualPaper image editor with an original blue mountain and lake drawing, brush settings, palette, and layers" width="760" />

Save your work as an editable project. Export images as PNG, BMP, JPEG, or JPEG XR, and Web wallpapers as packages for your library.

### Image enhancement

AI image tools process pictures locally on your PC. Results and processing time depend on the image and your hardware.

**Style transfer**

<table>
  <tr><th width="33%">Original</th><th width="33%">Style reference</th><th width="33%">Result</th></tr>
  <tr>
    <td align="center"><img src="src/VirtualPaper.UI/Assets/intelligent/demo/style_transfer/origin.jpg" alt="Original landscape" width="240" /></td>
    <td align="center"><img src="src/VirtualPaper.UI/Assets/intelligent/preset_styles/watercolor.jpg" alt="Watercolor reference used to generate the result" width="240" /></td>
    <td align="center"><img src="resources/readme/style-watercolor-result.jpg" alt="Landscape processed by the application's style transfer model using the watercolor reference" width="240" /></td>
  </tr>
</table>

**Super resolution**

<a href="resources/readme/upscale-result-4k.jpg"><img src="resources/readme/upscale-result-4k.jpg" alt="Sunrise beach and ocean waves upscaled from 960 by 540 to 3840 by 2160 pixels using VirtualPaper" width="760" /></a>

[960 × 540 input](resources/readme/upscale-input-960.jpg) → [3840 × 2160 result](resources/readme/upscale-result-4k.jpg). The input was prepared by downsampling a photograph.

<table>
  <tr><th width="50%">Bicubic 4×</th><th width="50%">Super resolution 4×</th></tr>
  <tr>
    <td align="center"><img src="resources/readme/upscale-detail-bicubic.png" alt="Wave and sea foam detail from bicubic enlargement of the 960 by 540 input" width="360" /></td>
    <td align="center"><img src="resources/readme/upscale-detail-model.png" alt="The same wave and sea foam detail from the model's 4K result" width="360" /></td>
  </tr>
</table>

The detail crops show the same region at the same display size, without additional sharpening.

## Download

<p>
  <a href="https://github.com/PaperHammer/VirtualPaper/releases/latest"><img src="resources/readme/download.svg" alt="Download for Windows" width="246" height="48" /></a>
</p>

**Windows 10 version 2004 (build 19041) or later · Windows 11 · x64**

1. Download the installer above, install, and open VirtualPaper.
2. Add an image, video, animated image, or Web wallpaper to your library.
3. Preview it, apply it to your desktop, and adjust its effects.

See the [user guide](https://github.com/PaperHammer/VirtualPaper/wiki) for more help getting started.

The installer checks for the **.NET 8 Desktop Runtime** and can download it when needed. The **Microsoft Edge WebView2 Runtime** is also required.

> [!NOTE]
> See the [release notes](https://github.com/PaperHammer/VirtualPaper/releases) for the features available in each version.

## Support

Found a problem or have an idea for VirtualPaper? Let us know.

[Report a bug](https://github.com/PaperHammer/VirtualPaper/issues/new?template=bug_report.yml) · [Suggest a feature](https://github.com/PaperHammer/VirtualPaper/issues/new?template=feature_request.yml) · [Improve a translation](https://github.com/PaperHammer/VirtualPaper/issues/new?template=translation_issue.yml)

For bug reports, include your VirtualPaper version, Windows version, and the steps to reproduce the problem. Screenshots and relevant logs help us investigate.

You can also help by improving translations, contributing fixes, or sharing VirtualPaper with others.

### Contribute

See the [developer documentation](doc/README.md) for source setup and builds, and the [testing guide](test/README.md) for running tests. Some detailed developer guides are in Chinese.

For complete local acceptance, run `pwsh -NoProfile -File test/Run-Tests.ps1` from elevated PowerShell 7.2+ on a clean Windows test installation. See the testing guide for required tools, models and the install/UI/uninstall stages.

AI contributors can start with [AGENTS.md](AGENTS.md) and the [repository testing skill](.agents/skills/virtualpaper-testing/SKILL.md). CI and release behavior is documented in the [workflow guide](.github/workflows/README.md).

Open pull requests against `dev`. Please discuss larger changes in an issue first.

## Made possible by open source

With thanks to [Lively Wallpaper](https://github.com/lively-community/lively), [XamlNexus](https://github.com/PaperHammer/XamlNexus), [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml), and [Monaco Editor](https://github.com/microsoft/monaco-editor).

VirtualPaper is free and open source under the [GNU General Public License v3.0](LICENSE). Third-party components and models retain their respective licenses.
