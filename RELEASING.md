# 一键发布 GitHub Release

项目根目录中的 [`release.ps1`](release.ps1) 用于把 `package.ps1` 生成的 MSI 自动发布到 [GitHub Releases](https://github.com/SOIL1900/screenshot-translation/releases)。脚本不需要填写固定仓库路径或版本号。

## 首次准备

需要安装 Git 和 GitHub CLI，并完成一次 GitHub 登录：

```powershell
winget install --id GitHub.cli -e --source winget
gh auth login --web
```

如果 Windows 已启用系统代理，脚本会在没有设置 `HTTPS_PROXY` 时自动将该代理提供给 GitHub CLI。

## 推荐发布流程

在项目根目录依次执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\package.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\release.ps1" -DryRun
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\release.ps1"
```

`-DryRun` 只检查版本、安装包、Git 状态、GitHub 登录和远端 Release 状态，不修改文件、不提交、不推送，也不创建 Release。

正式执行时，脚本会自动：

1. 从应用项目读取当前版本，并确认 MSI 版本一致。
2. 查找 `artifacts\ScreenshotTranslation-版本号-x64.msi`。
3. 检查安装包是否晚于当前源码，阻止发布旧安装包。
4. 确认当前分支是 GitHub 默认分支，且没有无关的未提交修改。
5. 将中英文 README 的最新版显示同步为当前版本。
6. 提交允许的版本文件和 README，并推送当前分支。
7. 创建 GitHub Release 草稿并生成发布说明。
8. 使用稳定文件名 `ScreenshotTranslation.Installer.msi` 上传安装包。
9. 核对远端文件大小和 GitHub SHA-256 摘要。
10. 校验成功后才正式发布，并标记为 Latest。

如果上传中断，Release 会保留为草稿。重新执行同一条发布命令时，脚本会继续该草稿；已经完整上传且摘要一致的安装包不会重复上传。

## 自定义发布说明

准备一个已经提交到仓库的 Markdown 文件，或使用仓库外部的文件路径，然后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\release.ps1" -NotesFile ".\release-notes.md"
```

未传入 `-NotesFile` 时，脚本使用 GitHub 自动生成的发布说明，并附加 Windows x64 安装包及 SHA-256 信息。

## 安全限制

- 已正式发布的同版本 Release 不会重复创建。
- 分支落后于远端时停止，不自动合并或覆盖远端提交。
- 除版本文件和中英文 README 外存在未提交修改时停止。
- 安装包过旧、文件大小不一致或 SHA-256 不一致时不会发布草稿。
