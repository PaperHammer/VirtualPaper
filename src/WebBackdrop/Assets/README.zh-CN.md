# Monaco 运行资源

[English](README.md) | [简体中文](README.zh-CN.md)

VirtualPaper 使用 Monaco 0.55.1，并将其内置 DOMPurify 更新到 3.4.17 后重新构建。编辑器 API 和 AMD 加载器保持 0.55.1。只修改 `package.json` 无法更新 JS 中已经打包的净化器。

在仓库根目录复现安全补丁（需要 PowerShell 7.2+、Node.js 22.12+ 和 npm）：

```powershell
pwsh -NoProfile -File .github/scripts/rebuild-monaco.ps1
```

脚本校验上游源码包和 DOMPurify 包的校验和，按上游锁文件还原依赖并禁用安装脚本，替换 core 包中的净化器源码，然后执行上游的 LSP 和 AMD 构建。结果暂存到新的、已被 Git 忽略的 `TestResults/monaco-rebuild-*/runtime/package` 目录，不会自动修改受版本控制的资源。

用暂存的 `min` 整体替换 `monaco-editor-0.55.1/package/min`，并复制暂存的 `package.json` 和 `DOMPurify.LICENSE`。清除旧哈希文件，避免混合两次构建。保留 Monaco 许可证及第三方声明。不要提交上游源码目录、`node_modules`、ESM 源码或压缩包。

重新构建完整 Release 解决方案，运行 Core、Web、包体项目测试，并验收真实 Web 编辑器的打开、编辑、保存和重新打开。确认包内 JS 实际包含 DOMPurify 3.4.17，不能只检查元数据。后续升级上游 Monaco 时，应先确认其实际内置净化器版本，再移除此补丁。
