# 项目总览

VirtualPaper 是面向 Windows 10 及以上系统的开源动态壁纸管理软件。它支持静态图、动图、视频和 Web 交互壁纸，并包含壁纸创作、屏保、锁屏设置、图像风格迁移和超分辨率等能力。

## 技术栈

- .NET 8，主要目标框架为 `net8.0-windows10.0.19041.0`。
- 主后台程序使用 WPF；管理与创作界面主要使用 WinUI 3 / Windows App SDK。
- Web 壁纸播放和编辑基于 WebView2，代码编辑器内置 Monaco Editor 0.55.1。
- 进程通信使用 gRPC、命名管道及 `GrpcDotNetNamedPipes`。
- 图像处理使用 OpenCvSharp、Win2D；机器学习推理使用 ONNX Runtime。
- 依赖注入使用 `Microsoft.Extensions.DependencyInjection`。
- 测试项目使用 MSTest，并在部分测试中使用 Moq。

## 解决方案组成

解决方案入口为 `src/VirtualPaper.sln`，项目可按职责分为以下几组。

| 分组 | 主要项目 | 职责 |
| --- | --- | --- |
| 核心进程 | `VirtualPaper` | WPF 后台主程序，负责壁纸控制、播放策略、系统交互、更新和 gRPC 服务 |
| 管理 UI | `VirtualPaper.UI` | WinUI 3 前端进程，组合设置、壁纸库、创作和智能处理面板 |
| 共享层 | `VirtualPaper.Common`, `VirtualPaper.Models`, `VirtualPaper.UIComponent` | 通用工具、模型/协议、可复用 WinUI 控件与基础设施 |
| 通信层 | `VirtualPaper.Grpc.Service`, `VirtualPaper.Grpc.Client`, `VirtualPaper.DataAssistor` | RPC 契约、服务端和客户端适配 |
| 创作运行时 | `StaticImg`, `WebBackdrop`, `Workloads.Entry`, `Workloads.Utils` | 静态图设计器、Web 工程编辑器以及运行时选择与公共能力 |
| 播放器 | `VirtualPaper.PlayerWeb`, `VirtualPaper.PlayerWeb.Core`, `VirtualPaper.Shader` | Web 壁纸进程、Web 播放界面和着色器处理 |
| 功能面板 | `VirtualPaper.*Panel` | 应用设置、壁纸设置、草稿空间和智能图像功能 |
| 辅助程序 | `VirtualPaper.ScreenSaver`, `VirtualPaper.ML` | 独立屏保与 ML 推理实现 |
| 验证 | `VirtualPaper.*.Test`, `VirtualPaper.SmokeTest` | Core、UI、ML、Shader、发布产物和安装后的冒烟验证 |

## 源码目录

```text
VirtualPaper/
├─ src/                 解决方案与全部 C# 项目
├─ resources/           README 图片、Logo 等仓库级资源
├─ InnoSetup/           安装包定义
├─ scripts/             构建/发布辅助脚本
├─ .github/workflows/   CI、版本和发布流水线
└─ doc/                 开发者文档
```

## 主要功能到模块的映射

| 功能 | 主要实现位置 |
| --- | --- |
| 壁纸生命周期与显示器管理 | `VirtualPaper/Cores/`, `VirtualPaper/Factories/` |
| 壁纸库和配置 | `VirtualPaper.WpSettingsPanel/` |
| 静态图创作 | `StaticImg/` |
| Web 壁纸创作 | `WebBackdrop/` |
| Web 壁纸播放 | `VirtualPaper.PlayerWeb/`, `VirtualPaper.PlayerWeb.Core/` |
| 草稿工作区 | `VirtualPaper.EditPanel/` |
| 风格迁移、超分辨率、深度估计 | `VirtualPaper.ML/`, `VirtualPaper.IntelligentPanel/` |
| 屏保 | `VirtualPaper.ScreenSaver/` |
| 应用更新与安装 | `VirtualPaper/Cores/AppUpdate/`, `InnoSetup/`, `.github/workflows/` |

## 设计特点

项目采用“后台核心进程 + 独立 WinUI 管理进程 + 按类型选择的播放器/创作运行时”结构。核心进程掌握系统级壁纸状态，UI 通过客户端接口调用核心服务。创作模块作为可组合的 WinUI 控件库接入工作区，具体文件类型由 `Workloads.Entry` 统一分派。
