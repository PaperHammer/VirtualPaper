# VirtualPaper developer documentation

[English](README.md) | [简体中文](README.zh-CN.md)

These guides are for contributors who want to read, debug, or maintain VirtualPaper. They describe the current source code and CI configuration. For user-facing features, see the root [README](../README.md).

## Guides

The detailed guides linked below are currently in Chinese.

- [Project overview](project-overview.md): product scope, technology stack, main processes, and source modules.
- [Architecture](architecture.md): Core, dependency direction, wallpaper runtimes, and data flows.
- [Development and builds](development-guide.md): prerequisites, builds, tests, and a suggested reading order.
- [WebBackdrop](webbackdrop.md): Web wallpaper project formats, the editor, file tree, preview, and saving.

## Source entry points

| Topic | Start here |
| --- | --- |
| Core startup and service registration | [VirtualPaper/App.xaml.cs](../src/VirtualPaper/App.xaml.cs) |
| WinUI startup | [VirtualPaper.UI/App.xaml.cs](../src/VirtualPaper.UI/App.xaml.cs) |
| Wallpaper runtime selection | [RuntimeFactory.cs](../src/Workloads.Entry/RuntimeFactory.cs) |
| Web project editor | [WebEditor.xaml.cs](../src/WebBackdrop/Views/Components/WebEditor.xaml.cs) |
| Web project file tree | [WebFileTreeControl.xaml.cs](../src/WebBackdrop/Views/Tools/WebFileTreeControl.xaml.cs) |
| Web project manifest | [WebDesignFileUtil.cs](../src/WebBackdrop/Models/SerializableData/WebDesignFileUtil.cs) |
| Shared project system and file watching | [ProjectSystem](../src/VirtualPaper.Common/Utils/ProjectSystem/) |
| Source translations and generated XAML resources | [Localization resources](../src/VirtualPaper.UIComponent/Localization/README.md) |
| CI, versioning, and releases | [Workflow guide](../.github/workflows/README.md) |
| Complete local acceptance and focused tests | [Testing guide](../test/README.md) |
| AI project orientation and testing instructions | [AGENTS.md](../AGENTS.md), [testing skill](../.agents/skills/virtualpaper-testing/SKILL.md) |

## Keeping these guides up to date

When adding or restructuring a module, update its responsibilities, dependencies, and key entry points. Describe behavior that can be confirmed in the source. Label assumptions about deployment environments, external services, and planned designs explicitly.
