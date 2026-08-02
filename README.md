# Screenshot Translation（截图翻译）

**简体中文** | [English](README.en.md)

Screenshot Translation 是一款面向外服游戏聊天场景的 Windows 托盘截图翻译工具。按下全局快捷键后，可以在鼠标所在显示器的冻结画面上框选文字，并将选区直接发送给 OpenAI 兼容的多模态模型。同一个结果面板还可以把快捷回复翻译回截图识别出的源语言。

## 下载最新版

[**下载 Screenshot Translation 1.0.11（Windows x64 MSI）**](https://github.com/SOIL1900/screenshot-translation/releases/latest/download/ScreenshotTranslation.Installer.msi)

安装包适用于 Windows 10/11 x64。历史版本和发布说明可以在 [GitHub Releases](https://github.com/SOIL1900/screenshot-translation/releases) 查看。

## 界面展示

英文网页截图翻译：

![英文网页截图翻译展示](assets/readme/translation-english.png)

塞尔维亚语截图翻译与源语言识别：

![塞尔维亚语截图翻译展示](assets/readme/translation-serbian.png)

## 支持环境

- Windows 10 或 Windows 11，x64 架构。
- 普通窗口应用和无边框窗口游戏。
- 不支持也不保证兼容独占全屏；截图前请将游戏切换为窗口或无边框窗口模式。
- 每次只处理一个显示器，不支持跨显示器框选。
- 需要 OpenAI 兼容的多模态接口和 API Key；默认预设面向阿里云百炼／DashScope。

## 安装

点击上方的 MSI 下载链接并运行。MSI 会把自包含应用安装到 `Program Files\ScreenshotTranslation`，创建开始菜单快捷方式，并注册标准 Windows 卸载项；安装完成后不会自动启动，也不会安装自动更新程序。

当前发布版本尚未进行代码签名，Windows SmartScreen 可能显示“无法识别的应用”警告。请先确认 MSI 来自本项目的 Release 页面，再选择 **更多信息** 和 **仍要运行**。以后可以加入签名流程；绕过该提示不需要输入 API Key 或任何其他密钥。

首次启动时需要完成模型设置。配置完整后，以后启动程序会直接常驻系统托盘。再次运行可执行文件只会激活已有实例，不会创建第二个托盘进程。

## 托盘与截图流程

- 左键点击托盘图标打开设置。
- 右键菜单只有三个操作：**开始截图翻译**、**设置**、**退出**。
- 关闭设置窗口只会隐藏窗口；需要选择 **退出** 才会结束程序。
- 默认全局截图快捷键是 `Ctrl + Alt + D`，可以在“常规”设置中修改。

截图翻译步骤：

1. 将目标程序切换为普通窗口或无边框窗口模式，并把鼠标移动到目标显示器。
2. 按 `Ctrl + Alt + D`，或在托盘菜单选择 **开始截图翻译**。程序只截取一次当前显示器，并把这一帧作为冻结覆盖层显示。
3. 拖拽创建选区。拖动选区内部可以移动，拖动四边和四角共八个控制点可以缩放。只有松开鼠标后才提交翻译，拖动过程中不会连续调用接口。
4. 查看识别出的源语言和截图译文。可以修改截图目标语言，也可以在不退出覆盖层的情况下重试可恢复错误。
5. 输入快捷回复并翻译。回复目标语言默认使用截图识别出的源语言，也可以手动修改；如果模型回退响应没有可靠语言信息，则必须手动选择。
6. 复制任意译文。复制成功后覆盖层会关闭，并恢复截图前处于活动状态的窗口，便于手动粘贴。

按 `Esc`、在尚未形成有效选区时右键，或点击选区和结果面板之外，都可以取消。程序不会向游戏自动注入文字。

## 模型配置

程序只维护一套当前 OpenAI 兼容模型配置，所有字段都可以在设置窗口中直接查看和编辑：

| 字段 | 默认值 | 用途 |
| --- | --- | --- |
| API 服务地址 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | OpenAI 兼容接口的基础地址。 |
| API Key | 空 | 调用当前接口时发送的凭据。 |
| 模型名称 | `qwen3.7-flash` | 用于截图和快捷回复的多模态模型。 |
| 思考模式 | 关闭（`enable_thinking: false`） | 控制请求是否启用模型思考。 |
| Temperature | `0.2` | 控制翻译随机性。 |
| 最大输出长度 | `2048` | 模型一次最多生成的 token 数。 |
| 请求超时 | `30` 秒 | 单次请求的超时时间。 |
| 额外请求参数 JSON | `{}` | 其他与当前服务兼容的请求参数。 |

默认模型严格使用 `qwen3.7-flash`，请求默认携带 `enable_thinking: false`。“测试连接”会发送一次很小的真实请求，用于验证服务地址、密钥、模型权限和响应结构，服务商可能对该请求计费。

截图上传前，程序会把选区 PNG 归一化到最长边不超过 2048 像素、编码后不超过 8 MiB。较小图片不会被放大；超限图片会等比缩小并保持 PNG 格式。归一化在可取消的后台任务中执行，不阻塞覆盖层交互。

## 隐私与本地数据

设置保存在 `%APPDATA%\ScreenshotTranslator\settings.json`。API Key 会以明文保存在该文件中，并在设置窗口的普通文本框中直接显示。程序不使用 Windows 凭据管理器，请妥善保护 Windows 账户和该文件。不要把包含真实配置的文件提交到仓库，也不要把内容粘贴到 Issue 中。

程序没有遥测，也不保存翻译历史。截图、快捷回复、完整模型请求、完整响应和译文都不会写入磁盘或诊断日志。不过，选区截图和快捷回复仍会通过网络发送给你配置的服务，请自行了解对应服务商的隐私和数据保留条款。

## 从源码构建、测试和打包

源码构建需要 Windows x64、.NET 8 SDK，以及用于恢复 NuGet 包（包括 WiX Toolset SDK）的网络连接。

```powershell
dotnet restore ScreenshotTranslation.sln
dotnet build ScreenshotTranslation.sln -c Release --no-restore
dotnet test ScreenshotTranslation.sln -c Release --no-build
```

生成自包含的 Windows x64 发布目录和 MSI：

```powershell
dotnet publish src/ScreenshotTranslation.App/ScreenshotTranslation.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
dotnet build installer/ScreenshotTranslation.Installer.wixproj -c Release
```

第一条命令把免安装发布文件写入 `artifacts\publish`；第二条命令把该目录的内容打包为 x64 MSI。两条命令都不需要 API Key。Windows GitHub Actions 工作流会在不配置仓库密钥、不调用真实模型接口的情况下执行恢复、Release 构建、自动化测试、自包含发布和 MSI 打包。

## 参与贡献

欢迎提交 Issue 和范围明确的 Pull Request：

1. Core 逻辑应保持独立于 WPF 和 Win32 适配器，网络与屏幕行为应保持可注入、可测试。
2. 修改行为时请增加或更新聚焦测试，并根据风险运行相应的构建和测试命令，避免无意义地重复全量验证。
3. 不要提交 API Key、包含真实数据的设置文件、截图、译文或其他用户隐私数据。

本项目使用 [MIT License](LICENSE) 开源。
