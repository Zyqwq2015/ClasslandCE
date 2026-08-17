Whisper 离线语音模型（唤醒 / 命令识别用）
================================================

本目录用于存放 Whisper 的 ggml 模型文件。引擎默认读取：
    <程序目录>/Models/ggml-model.bin

模型文件体积较大（几十~几百 MB），不包含在仓库与发布包中，需要单独下载一次。

下载步骤（自动 / 手动）
------------------------
★ 自动（推荐，最简单）：在「设置 → 语音控制」开启开关并重启应用后，
  若本地没有模型，App 会自动从 HuggingFace / hf-mirror 下载默认模型
  （ggml-small-q5_0.bin，约 77MB）到本目录，状态栏会显示下载进度，
  无需任何手动操作。若所在网络直连 HuggingFace 不通，可改用能访问的镜像或走手动。

1. 手动下载：从 HuggingFace 下载下列任一 ggml 模型（中文识别建议 small 及以上规模）：
   - ggml-small.bin            （约 240 MB，精度高）
   - ggml-small-q5_0.bin       （约 77 MB，体积与精度折中，推荐）
   - ggml-base.bin / ggml-base-q5_0.bin （约 70/25 MB，速度最快但中文弱）
   官方模型仓库： https://huggingface.co/ggerganov/whisper.cpp/tree/main
   Whisper.net 说明： https://github.com/sandrohanea/whisper.net

2. 把下载到的文件重命名为 ggml-model.bin，放到本目录（即 <程序目录>/Models/ 下）。
   也可以不改名，转而在「设置 → 语音控制 → Whisper 模型路径」里指定完整路径。

3. 重新打开语音控制开关，状态栏应显示「监听中 · 唤醒词「…」· Whisper 离线识别」，
   如果仍显示「Whisper 模型未就绪…」，说明路径/文件名不对或文件损坏。

说明
----
- 纯离线识别，无需联网、无需 Windows 语言包、无需训练。
- 唤醒词按文字匹配：只要 Whisper 转写结果里包含唤醒词（默认「小课小课」）即触发，中文词开箱即用。
- 灵敏度（设置项）仅影响 VAD（语音活动检测）的判定阈值，不影响识别本身。
