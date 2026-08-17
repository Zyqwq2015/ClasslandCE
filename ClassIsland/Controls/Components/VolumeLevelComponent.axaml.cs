using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;
using NAudio.Wave;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 音量检测组件：实时显示麦克风音量电平
/// </summary>
[ComponentInfo("BCF2EC30-4B22-4ACB-9EE1-76A704056A59", "音量检测", "\uE767", "实时显示麦克风音量电平。")]
public partial class VolumeLevelComponent : ComponentBase<VolumeLevelComponentSettings>
{
    private WaveInEvent? _waveIn;
    private float _level; // 0-1
    private readonly object _levelLock = new();
    private DispatcherTimer? _uiTimer;

    public VolumeLevelComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartCapture();
        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _uiTimer.Tick += (_, _) => UpdateUi();
        _uiTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _uiTimer?.Stop();
        _uiTimer = null;
        StopCapture();
    }

    private void StartCapture()
    {
        try
        {
            var deviceCount = WaveInEvent.DeviceCount;
            if (deviceCount <= 0) return;

            var deviceIndex = Math.Clamp(Settings.DeviceIndex, 0, deviceCount - 1);
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }
        catch (Exception)
        {
            // 无麦克风或设备被占用时静默
            _waveIn = null;
        }
    }

    private void StopCapture()
    {
        try
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // 计算 RMS 电平
        float sum = 0;
        var samples = e.BytesRecorded / 2;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
            sum += sample * sample;
        }
        var rms = samples > 0 ? (float)Math.Sqrt(sum / samples) : 0f;
        // 灵敏度：0-100 映射到阈值 0.5 -> 0.01（越灵敏阈值越低）
        var threshold = 0.5f - (Settings.Sensitivity / 100f) * 0.49f;
        var level = Math.Clamp((rms - threshold) * 8f, 0f, 1f);
        lock (_levelLock)
        {
            // 平滑
            _level = _level * 0.55f + level * 0.45f;
        }
    }

    private void UpdateUi()
    {
        float level;
        lock (_levelLock)
        {
            level = _level;
            // 缓慢回落
            _level *= 0.96f;
        }

        var pct = (int)Math.Round(level * 100);
        PercentText.Text = $"{pct}%";
        IconText.Text = pct > 60 ? "\uE768" : pct > 25 ? "\uE767" : "\uE74D";

        // 更新 10 段电平条
        var barCount = 10;
        if (Bars.Items.Count == 0)
        {
            for (int i = 0; i < barCount; i++) Bars.Items.Add(i);
        }
        var activeBars = (int)Math.Ceiling(level * barCount);
        for (int i = 0; i < Bars.Items.Count; i++)
        {
            // ContentPresenter.Child 即模板根 Border
            var presenter = Bars.ItemContainerGenerator.ContainerFromIndex(i);
            var border = (presenter as ContentPresenter)?.Child as Border;
            if (border == null) continue;
            border.Background = i < activeBars
                ? (i >= 7 ? new SolidColorBrush(Color.FromRgb(255, 80, 80))
                    : i >= 4 ? new SolidColorBrush(Color.FromRgb(255, 180, 0))
                    : new SolidColorBrush(Color.FromRgb(80, 220, 120)))
                : (IBrush)new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        }
        IconText.Opacity = 0.6 + 0.4 * level;
    }
}