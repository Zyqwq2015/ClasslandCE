Whisper 离线语音模型（唤醒 / 命令识别用）
================================================

引擎默认读取内置模型：
    <程序目录>/Models/ggml-model.bin

本模型已随发布包内置，运行时不再自动下载——用户拿到安装包即可直接使用，
无需任何联网步骤。这对校园网等低速网络环境尤其重要。

模型如何进入发布包
------------------
模型文件较大（ggml-small-q5_0 约 150MB），超过 GitHub 单文件 100MB 限制，因此不纳入 git 仓库，
仅由打包环境本地携带、随发布包（publish）一起分发。打包前请确保模型已就位：

  1. 把 ggml 模型文件重命名为 ggml-model.bin，放到：
       ClassIsland/Assets/VoiceWake/Models/ggml-model.bin
     （该路径被 .gitignore 排除，不会提交到 GitHub，但会随 publish 自动拷贝到发布包的 Models/）

  2. 执行发布：
       dotnet publish -c Release -r win-x64 --self-contained true
     该文件会自动出现在发布包的 Models/ggml-model.bin，引擎启动时直接加载。

获取模型的几种方式
------------------
1. 从 HuggingFace 官方仓库下载（需能访问 HF 的网络 / 代理）：
     https://huggingface.co/ggerganov/whisper.cpp/tree/main
   推荐：ggml-small-q5_0.bin（约 150MB，体积与精度折中）或 ggml-small.bin（约 240MB，精度更高）。
   仓库内 Scripts/download-whisper-model.ps1 可一键下载到正确位置。
2. 国内镜像（如 hf-mirror.com）可尝试，但部分镜像不代理 LFS 大文件，可能失败。
3. 也可不改名，转而在「设置 -> 语音控制 -> Whisper 模型路径」里指定完整路径。

说明
----
- 纯离线识别，无需联网、无需 Windows 语言包、无需训练。
- 唤醒词按文字匹配：只要 Whisper 转写结果里包含唤醒词（默认「小课小课」）即触发，中文词开箱即用。
- 灵敏度（设置项）仅影响 VAD（语音活动检测）的判定阈值，不影响识别本身。
- 若启动后状态栏提示「未找到 Whisper 模型」，说明发布包未携带模型或路径不对，请按上文放入后重启。
