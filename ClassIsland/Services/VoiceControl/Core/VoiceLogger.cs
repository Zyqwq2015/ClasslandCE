using System;
using System.IO;
using System.Text;
using System.Threading;
using ClassIsland.Services.VoiceControl.Abstractions;

namespace ClassIsland.Services.VoiceControl.Core;

/// <summary>
/// 轻量文件 + 控制台日志实现。滚动写入，单文件上限 1MB 后自动轮转。
/// 所有异常都经过此处落盘，方便排查麦克风/识别/执行失败。
/// </summary>
public sealed class VoiceLogger : IVoiceLogger, IDisposable
{
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private readonly string _logDir = string.Empty;
    private int _index;
    private const long MaxBytes = 1L * 1024 * 1024;

    public VoiceLogger(string? logDirectory = null)
    {
        try
        {
            _logDir = logDirectory
                      ?? Path.Combine(
                          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                          "ClassIsland.CE", "Logs", "Voice");
            Directory.CreateDirectory(_logDir);
            OpenNewFile();
        }
        catch (Exception ex)
        {
            // 日志初始化失败时降级为仅控制台，绝不向外抛异常阻断主流程。
            Console.Error.WriteLine($"[VoiceLogger] 初始化失败，仅使用控制台输出: {ex.Message}");
        }
    }

    private void OpenNewFile()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        var path = Path.Combine(_logDir, $"voice-{stamp}-{_index}.log");
        while (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
        {
            _index++;
            path = Path.Combine(_logDir, $"voice-{stamp}-{_index}.log");
        }
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    private void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        if (ex != null)
            line += $"{Environment.NewLine}    -> {ex.GetType().Name}: {ex.Message}{Environment.NewLine}    {ex.StackTrace}";

        try
        {
            Console.WriteLine(line);
        }
        catch { /* 忽略控制台写入异常 */ }

        if (_writer == null) return;
        lock (_lock)
        {
            try
            {
                _writer.WriteLine(line);
                if (_writer.BaseStream.Length > MaxBytes)
                {
                    _writer.Dispose();
                    _index++;
                    OpenNewFile();
                }
            }
            catch { /* 日志写入失败不能影响主流程 */ }
        }
    }

    public void Trace(string message) => Write("TRACE", message, null);
    public void Info(string message) => Write("INFO", message, null);
    public void Warning(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public void Dispose()
    {
        lock (_lock)
        {
            try { _writer?.Dispose(); } catch { /* ignore */ }
            _writer = null;
        }
        GC.SuppressFinalize(this);
    }
}
