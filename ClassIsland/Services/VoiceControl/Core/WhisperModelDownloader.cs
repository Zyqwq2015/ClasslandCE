using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 从 HuggingFace / 国内镜像 下载 Whisper 的 ggml 模型文件。
/// 依次尝试多个镜像，任一成功即流式写入目标路径；全部失败则抛出异常。
/// 这样用户在「设置 → 语音控制」开启开关后，若本地没有模型，App 会自动拉取，
/// 无需手动下载。
/// </summary>
public sealed class WhisperModelDownloader : IDisposable
{
    // 依次尝试的镜像（baseUrl 末尾带 /）。HuggingFace 官方优先；
    // hf-mirror 作为国内备选（若所在网络直连 HF 不通时可走它）。
    private static readonly string[] MirrorBaseUrls =
    {
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/",
        "https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main/"
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(20) };

    /// <summary>下载进度：已下载字节 / 总字节（-1 表示未知）。</summary>
    public event Action<long, long>? Progress;

    /// <summary>状态变化（如「正在从 xxx 下载…」）。</summary>
    public event Action<string>? StatusChanged;

    public async Task DownloadAsync(string modelFileName, string destinationPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        var tmp = destinationPath + ".part";
        Exception? lastEx = null;

        foreach (var baseUrl in MirrorBaseUrls)
        {
            var url = baseUrl + modelFileName;
            var host = SafeHost(url);
            StatusChanged?.Invoke($"正在从 {host} 下载模型「{modelFileName}」…");
            try
            {
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    lastEx = new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    continue;
                }

                var total = resp.Content.Headers.ContentLength ?? -1;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(tmp);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    Progress?.Invoke(done, total);
                }

                File.Move(tmp, destinationPath, true);
                StatusChanged?.Invoke("模型下载完成。");
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lastEx = ex;
                StatusChanged?.Invoke($"{host} 下载失败：{ex.Message}，尝试下一个镜像…");
            }
        }

        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
        throw new InvalidOperationException(
            $"所有镜像均无法下载模型（{lastEx?.Message}）。请手动下载 ggml 模型并放入：{destinationPath}", lastEx);
    }

    private static string SafeHost(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }

    public void Dispose() => _http.Dispose();
}
