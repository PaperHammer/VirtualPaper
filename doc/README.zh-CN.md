# VirtualPaper 开发文档

[English](README.md) | [简体中文](README.zh-CN.md)

这些文档面向需要阅读、调试或维护 VirtualPaper 的贡献者，介绍当前源码和 CI 配置。产品功能请参阅根目录的 [README](../README.md)。

## 指南

以下详细指南目前使用中文编写。

- [项目概览](project-overview.md)：产品范围、技术栈、主要进程和源码模块。
- [架构](architecture.md)：Core、依赖方向、壁纸运行时和数据流。
- [开发与构建](development-guide.md)：环境要求、构建、测试和建议的阅读顺序。
- [WebBackdrop](webbackdrop.md)：Web 壁纸项目格式、编辑器、文件树、预览和保存。

## 源码入口

| 主题 | 入口 |
| --- | --- |
| Core 启动与服务注册 | [VirtualPaper/App.xaml.cs](../src/VirtualPaper/App.xaml.cs) |
| WinUI 启动 | [VirtualPaper.UI/App.xaml.cs](../src/VirtualPaper.UI/App.xaml.cs) |
| 壁纸运行时选择 | [RuntimeFactory.cs](../src/Workloads.Entry/RuntimeFactory.cs) |
| Web 项目编辑器 | [WebEditor.xaml.cs](../src/WebBackdrop/Views/Components/WebEditor.xaml.cs) |
| Web 项目文件树 | [WebFileTreeControl.xaml.cs](../src/WebBackdrop/Views/Tools/WebFileTreeControl.xaml.cs) |
| Web 项目清单 | [WebDesignFileUtil.cs](../src/WebBackdrop/Models/SerializableData/WebDesignFileUtil.cs) |
| 共用项目系统与文件监视 | [ProjectSystem](../src/VirtualPaper.Common/Utils/ProjectSystem/) |
| 源文案与 XAML 资源生成 | [语言资源](../src/VirtualPaper.UIComponent/Localization/README.zh-CN.md) |
| CI、版本管理与发布 | [工作流指南](../.github/workflows/README.zh-CN.md) |

## 文档维护

新增或调整模块结构时，请更新模块职责、依赖关系和关键入口。描述应以源码中可确认的行为为依据；涉及部署环境、外部服务或计划中的设计时，请明确标注假设。
