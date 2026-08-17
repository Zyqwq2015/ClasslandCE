# Classland CE — ClassIsland Community Edition

> 🚧 **开发中 · 实验性版本，不建议用于生产环境**
>
> 本项目仍处于早期开发阶段（v0.0.1），功能可能不稳定、配置可能变动、部分特性尚未完成。**请谨慎使用，生产环境请勿部署。**

| | |
| --- | --- |
| 当前版本 | **v0.0.1**（开发中） |
| 维护者 | [Zyqwq2015](https://github.com/Zyqwq2015) |
| 开源仓库 | <https://github.com/Zyqwq2015/ClasslandCE> |
| 许可协议 | GNU General Public License v3 |

基于 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 的社区增强版（GPLv3）。ClassIsland 原项目版权归其贡献者所有，本仓库为其在 GPL-3.0 下的**修改版 / 派生作品**。

## 新增功能

### 1. 语音助手（离线文本指令操控）
- 按 **Ctrl+Alt+C** 打开命令输入框，输入指令后由 TTS 语音播报结果
- 全程离线运行（无需麦克风语音识别，规避 .NET 8 缺失的 System.Speech.Recognition）
- 支持的指令：
  - 查课表：`下一节课` / `今天课表` / `现在上什么课`
  - 查时间：`现在几点` / `今天星期几`
  - 打开应用：`打开浏览器` / `打开记事本` / `打开计算器`
  - 界面控制：`显示课表` / `隐藏课表` / `打开设置`
  - 其它：`谢谢`（礼貌回应）

### 2. 智能播报模板
- 自定义播报文案模板，支持占位符：
  - `{subject}` — 科目名（如"数学"）
  - `{teacher}` — 老师姓氏（如"张老师"）
  - `{startTime}` — 上课时间（如"8:00"）
  - `{endTime}` — 下课时间（如"8:40"）
  - `{location}` — 上课地点
  - `{subjectIndex}` — 第几节课（如"1"）
- 内置预设模板：简洁版、倒计时版、详细版、完整版
- 支持重复播报（间隔秒数、最大次数可配置）
- 设置页提供「试听」按钮

### 3. 快速时间表生成
- 一键生成标准时间表：设置第一节课时间、每节时长、课间休息、每天节数
- 自动添加到课表档案，无需手动逐条添加

### 4. 免装 .NET 便携版
- 一键构建自包含（self-contained）版本，无需安装 .NET 8 运行时
- 解压即用，适合 U 盘携带、机房/教室大屏部署

## 快速开始

### 前置要求
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 或 VS Code（可选）

### 编译运行

```bash
# 标准版（需要 .NET 8 运行时）
dotnet run --project ClassIsland/ClassIsland.csproj -c Debug

# 自包含便携版（免装运行时，~200MB）
./build-ce.ps1 -SelfContained
```

### 打包
```bash
# 构建完成后，输出在 ./out/ce/，直接运行 ClassIsland.exe
```

## GPLv3 合规说明（重要）

Classland CE 是 [ClassIsland](https://github.com/ClassIsland/ClassIsland)（GPL-3.0）的**修改版 / 派生作品**，依照 GNU General Public License v3 发布。所有新增与修改代码（离线语音唤醒/指令控制、播报模板、快速时间表、CE 设置页、构建脚本等）同样基于 GPLv3 发布。

- **上游项目：** [ClassIsland](https://github.com/ClassIsland/ClassIsland)（GPL-3.0），版权归其原贡献者所有，本仓库完整保留其版权声明与许可文本（见 [LICENSE.txt](./LICENSE.txt)）。
- **本仓库（修改版）：** [Zyqwq2015/ClasslandCE](https://github.com/Zyqwq2015/ClasslandCE)（GPL-3.0）
- **修改声明（GPL-3.0 第 5 条）：** 本仓库在原始 ClassIsland 基础上进行了大量修改（新增离线语音唤醒/指令控制、播报模板、快速时间表、CE 设置页、构建脚本等），并变更了版本号（v0.0.1）与维护者信息（Zyqwq2015）。所有修改均在提交记录与本文档中标注。
- **许可一致性：** 如您分发或修改本仓库的源码/二进制，须同样以 GPL-3.0 提供源码并保留本声明。

## 文件结构

```
ClassIsland/                         # 主项目（修改文件）
  Services/
    VoiceAssistantService.cs          # [CE] 语音助手
  Helpers/
    SpeechTemplateHelper.cs           # [CE] 播报模板引擎
  ViewModels/SettingsPages/
    ClasslandCESettingsViewModel.cs   # [CE] 设置页 ViewModel
  Views/SettingPages/
    ClasslandCESettingsPage.axaml     # [CE] 设置页 XAML
    ClasslandCESettingsPage.axaml.cs  # [CE] 设置页代码
  Models/Settings.cs                  # [CE] 新增设置字段
  App.Services.xaml.cs               # [CE] DI 注册
build-ce.ps1                         # [CE] 构建脚本
README.md                            # 本文件
```

## 许可

本项目（Classland CE，v0.0.1，开发中）基于 **GNU General Public License v3** 发布，是 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 的修改版。完整许可证文本见 [LICENSE.txt](./LICENSE.txt)。