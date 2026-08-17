using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AForge.Video;
using AForge.Video.DirectShow;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;

namespace ClassIsland.Controls.Components;

/// <summary>
/// 摄像头画面采集组件：实时显示摄像头画面
/// </summary>
[ComponentInfo("FEEF4CA4-9A0D-4718-909A-618A8C2848CF", "摄像头画面", "\uE71E", "实时采集并显示摄像头画面。")]
public partial class CameraFeedComponent : ComponentBase<CameraFeedComponentSettings>
{
    private VideoCaptureDevice? _device;
    private bool _deviceOk;

    public CameraFeedComponent()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartCamera();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopCamera();
    }

    private void StartCamera()
    {
        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
            {
                ShowState("未检测到摄像头");
                return;
            }

            var idx = Math.Clamp(Settings.DeviceIndex, 0, devices.Count - 1);
            _device = new VideoCaptureDevice(devices[idx].MonikerString);
            _device.NewFrame += OnNewFrame;
            _device.VideoSourceError += (_, args) =>
            {
                Dispatcher.UIThread.Post(() => ShowState("摄像头错误"));
                _deviceOk = false;
            };
            _device.Start();
            _deviceOk = true;
            StateText.IsVisible = false;
        }
        catch (Exception)
        {
            ShowState("摄像头启动失败");
        }
    }

    private void StopCamera()
    {
        try
        {
            if (_device != null)
            {
                _device.NewFrame -= OnNewFrame;
                if (_device.IsRunning)
                    _device.SignalToStop();
                _device = null;
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        if (!_deviceOk) return;
        try
        {
            var frame = (System.Drawing.Bitmap)eventArgs.Frame.Clone();
            // 镜像
            if (Settings.Mirrored)
                frame.RotateFlip(RotateFlipType.RotateNoneFlipX);

            using var ms = new MemoryStream();
            frame.Save(ms, ImageFormat.Jpeg);
            ms.Position = 0;

            var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
            Dispatcher.UIThread.Post(() =>
            {
                VideoImage.Source = avaloniaBitmap;
                StateText.IsVisible = false;
            });

            frame.Dispose();
        }
        catch (Exception)
        {
            _deviceOk = false;
            Dispatcher.UIThread.Post(() => ShowState("画面渲染错误"));
        }
    }

    private void ShowState(string text)
    {
        StateText.Text = text;
        StateText.IsVisible = true;
    }
}