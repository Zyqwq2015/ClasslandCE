using System.Collections.Generic;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Services;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class ClasslandCESettingsViewModel(
    SettingsService settingsService,
    ISpeechService speechService
) : ObservableRecipient
{
    public SettingsService SettingsService { get; } = settingsService;
    public ISpeechService SpeechService { get; } = speechService;

    public List<string> SpeechTemplatePresets { get; } =
    [
        "下节课是：{subject}，{teacher}{startTime}开始。",
        "距上课还有{startTime}分钟。下节课是：{subject}，{teacher}。",
        "接下来是{subject}课，{teacher}上课时间是{startTime}。",
        "第{subjectIndex}节：{subject}，{teacher}从{startTime}到{endTime}。",
    ];

    public List<string> SpeechTemplateNames { get; } =
    [
        "简洁版",
        "倒计时版",
        "详细版",
        "完整版",
    ];

    private bool _isSpeechTestEnabled = false;
    private string _testSpeechText = "测试语音播报，这里是Classland CE增强版。";

    public bool IsSpeechTestEnabled
    {
        get => _isSpeechTestEnabled;
        set
        {
            if (value == _isSpeechTestEnabled) return;
            _isSpeechTestEnabled = value;
            OnPropertyChanged();
        }
    }

    public string TestSpeechText
    {
        get => _testSpeechText;
        set
        {
            if (value == _testSpeechText) return;
            _testSpeechText = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void TestSpeech()
    {
        SpeechService.ClearSpeechQueue();
        SpeechService.EnqueueSpeechQueue(TestSpeechText);
    }

    [RelayCommand]
    private void ApplyTemplatePreset(int index)
    {
        if (index >= 0 && index < SpeechTemplatePresets.Count)
        {
            SettingsService.Settings.CeSpeechTemplate = SpeechTemplatePresets[index];
        }
    }

    [RelayCommand]
    private void GenerateQuickTimeLayout()
    {
        var settings = SettingsService.Settings;
        var profileService = App.GetService<IProfileService>();
        var profile = profileService.Profile;

        if (profile?.TimeLayouts == null) return;

        var timeLayout = new ClassIsland.Shared.Models.Profile.TimeLayout
        {
            Name = $"快速时间表（{settings.CeQuickTimeLayoutClassCount}节）"
        };

        var start = new System.TimeSpan(settings.CeQuickTimeLayoutStartHour, settings.CeQuickTimeLayoutStartMinute, 0);
        var duration = System.TimeSpan.FromMinutes(settings.CeQuickTimeLayoutDurationMinutes);
        var breakDuration = System.TimeSpan.FromMinutes(settings.CeQuickTimeLayoutBreakMinutes);

        for (int i = 0; i < settings.CeQuickTimeLayoutClassCount; i++)
        {
            var classEnd = start + duration;

            // 上课节
            timeLayout.Layouts.Add(new ClassIsland.Shared.Models.Profile.TimeLayoutItem
            {
                StartTime = start,
                EndTime = classEnd,
                TimeType = 0, // 上课
                BreakName = "课间休息"
            });

            if (i < settings.CeQuickTimeLayoutClassCount - 1)
            {
                var breakEnd = classEnd + breakDuration;
                // 课间休息
                timeLayout.Layouts.Add(new ClassIsland.Shared.Models.Profile.TimeLayoutItem
                {
                    StartTime = classEnd,
                    EndTime = breakEnd,
                    TimeType = 1, // 课间
                    BreakName = "课间休息"
                });
                start = breakEnd;
            }
        }

        var id = System.Guid.NewGuid();
        profile.TimeLayouts[id] = timeLayout;
        // 持久化到档案（普通模式下字典变更不会自动触发保存）
        if (profileService is ProfileService ps)
        {
            ps.SaveProfile();
        }
        System.Diagnostics.Debug.WriteLine($"[ClasslandCE] 快速生成时间表: {timeLayout.Name} ({timeLayout.Layouts.Count}个时间点)");
    }
}