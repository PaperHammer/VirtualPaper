# VirtualPaper 开发文档

本目录面向准备阅读、调试和维护 VirtualPaper 的开发者。内容依据当前仓库源码与 CI 配置整理；面向用户的功能介绍仍以根目录 [`README.md`](../README.md) 为准。

## 文档索引

- [项目总览](project-overview.md)：产品边界、技术栈、主要进程与源码模块。
- [架构说明](architecture.md)：核心进程、依赖方向、壁纸运行时和数据流。
- [开发与构建](development-guide.md)：环境要求、构建、测试以及推荐阅读顺序。
- [WebBackdrop 模块](webbackdrop.md)：Web 壁纸工程格式、编辑器、文件树、预览与保存机制。

## 快速定位

| 想了解的内容 | 建议入口 |
| --- | --- |
| 主程序如何启动和注册服务 | `src/VirtualPaper/App.xaml.cs` |
| WinUI 管理界面如何启动 | `src/VirtualPaper.UI/App.xaml.cs` |
| 壁纸运行时如何选择 | `src/Workloads.Entry/RuntimeFactory.cs` |
| Web 项目编辑器 | `src/WebBackdrop/Views/Components/WebEditor.xaml.cs` |
| Web 项目文件树 | `src/WebBackdrop/Views/Tools/WebFileTreeControl.xaml.cs` |
| Web 项目清单 | `src/WebBackdrop/Models/SerializableData/WebDesignFileUtil.cs` |
| 公共项目系统与文件监听 | `src/VirtualPaper.Common/Utils/ProjectSystem/` |
| CI、版本与发布流程 | `.github/workflows/README.md` |

## 文档维护约定

新增或重构模块时，请同步更新模块职责、依赖方向和关键入口。文档只描述源码中可以确认的行为；部署环境、外部服务或尚未落地的设计应明确标注为假设或规划。
