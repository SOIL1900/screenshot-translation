# 一键迭代打包

项目根目录中的 [`package.ps1`](package.ps1) 用于自动生成下一补丁版本的 Windows x64 MSI 安装包。

例如，当前项目版本是 `1.0.9`，执行脚本后会自动打包为 `1.0.10`。不需要在脚本中填写仓库路径或版本号。

## 准备环境

需要安装：

- Windows 10 或 Windows 11 x64
- .NET 8 SDK
- 可访问 NuGet 的网络连接

## 执行方法

在项目根目录打开 PowerShell，然后复制执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\package.ps1"
```

也可以在已经允许执行本地 PowerShell 脚本的环境中直接运行：

```powershell
.\package.ps1
```

## 脚本执行内容

脚本会依次完成以下操作：

1. 使用脚本所在目录作为项目根目录。
2. 从应用项目文件读取当前版本号。
3. 自动将补丁版本加一，例如 `1.0.9 → 1.0.10`。
4. 同步更新应用版本和 MSI 版本。
5. 清理旧的发布目录及 WiX 构建目录。
6. 恢复 NuGet 依赖。
7. 编译 Release 版本。
8. 运行全部自动化测试。
9. 发布自包含的 Windows x64 程序。
10. 构建 MSI 安装包。
11. 将安装包复制到 `artifacts`，并在文件名中加入版本号。
12. 输出安装包完整路径、文件大小和 SHA-256。

如果任意编译、测试或打包步骤失败，脚本会停止执行，并自动还原本次修改的应用版本和 MSI 版本。

## 输出文件

成功后，安装包位于：

```text
artifacts\ScreenshotTranslation-自动生成的版本号-x64.msi
```

例如：

```text
artifacts\ScreenshotTranslation-1.0.10-x64.msi
```

自包含程序位于：

```text
artifacts\publish\ScreenshotTranslation.exe
```

该脚本只负责本地迭代打包，不执行 Git 提交、标签创建、推送或 GitHub Release 上传。
