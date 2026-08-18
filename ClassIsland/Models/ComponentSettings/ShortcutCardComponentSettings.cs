using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.ComponentSettings;

/// <summary>
/// 桌面卡片（自定义快捷方式）组件设置
/// </summary>
public class ShortcutCardComponentSettings : ObservableRecipient
{
    private string _title = "快捷方式";
    private string _iconPath = "";
    private string _targetPath = "";
    private string _arguments = "";

    /// <summary>卡片标题（显示在图标下方）</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (value == _title) return;
            _title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>图标文件路径（支持 jpg / png / ico / bmp），留空则使用默认图标</summary>
    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (value == _iconPath) return;
            _iconPath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>点击目标：文件 / 程序 / 网址，支持 %USERPROFILE% 等环境变量</summary>
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

    /// <summary>可选的启动命令行参数</summary>
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
