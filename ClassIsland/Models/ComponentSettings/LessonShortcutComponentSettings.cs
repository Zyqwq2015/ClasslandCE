using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 课目课件快捷方式组件设置
///</summary>
public class LessonShortcutComponentSettings : ObservableRecipient
{
    private string _buttonLabel = "语文";
    private string _icon = "\uE8A5"; // Fluent UI 书本图标
    private string _targetPath = "";
    private string _arguments = "";

    /// <summary>按钮标签（语/数/英/科等）</summary>
    public string ButtonLabel
    {
        get => _buttonLabel;
        set
        {
            if (value == _buttonLabel) return;
            _buttonLabel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>图标（Segoe Fluent Icons 字符码）</summary>
    public string Icon
    {
        get => _icon;
        set
        {
            if (value == _icon) return;
            _icon = value;
            OnPropertyChanged();
        }
    }

    /// <summary>目标路径（exe/网址/任意文件）</summary>
    public string TargetPath
    {
        get => _targetPath;
        set
        {
            if (value == _targetPath) return;
            _targetPath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>命令行参数</summary>
    public string Arguments
    {
        get => _arguments;
        set
        {
            if (value == _arguments) return;
            _arguments = value;
            OnPropertyChanged();
        }
    }
}