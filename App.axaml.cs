using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace GodotManager;

public partial class App : Application
{
    private static bool _darkTheme;

    private static readonly (string Key, string Light, string Dark)[] Palette =
    {
        ("AppBackgroundBrush", "#F3F6FC", "#141820"),
        ("SurfaceBrush", "#FFFFFF", "#1D222C"),
        ("SidePanelBrush", "#F5F8FE", "#202634"),
        ("SubtleBrush", "#F2F5FA", "#272C38"),
        ("HoverBrush", "#F7F9FE", "#252B39"),
        ("SelectedBrush", "#EAF1FE", "#27324B"),
        ("SelectedHoverBrush", "#E3ECFD", "#2E3B58"),
        ("DividerBrush", "#EDF0F6", "#2C3341"),
        ("TextBrush", "#1F2735", "#E8ECF5"),
        ("MutedTextBrush", "#78859A", "#8B95A7"),
        ("AccentBrush", "#4B6EC6", "#5C7BD6"),
        ("AccentHoverBrush", "#3E5FB2", "#6B88E0"),
        ("AccentTextBrush", "#3562C8", "#93AEFF"),
        ("AccentSoftBrush", "#E7EEFC", "#273453"),
        ("AccentBorderBrush", "#DAE4F8", "#34415F")
    };

    private static readonly (string Alias, string Source)[] LegacyAliases =
    {
        ("BrandBrush", "AccentBrush"),
        ("BrandHoverBrush", "AccentHoverBrush"),
        ("BrandSurfaceBrush", "AccentSoftBrush"),
        ("BrandBorderBrush", "AccentBorderBrush"),
        ("BrandPanelBrush", "SidePanelBrush"),
        ("ListItemHoverBrush", "HoverBrush"),
        ("ListItemSelectedBrush", "SelectedBrush"),
        ("ListItemSelectedHoverBrush", "SelectedHoverBrush")
    };

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 其它窗口沿用的旧资源名指向同一画刷实例，主题切换时会一起变色。
        foreach (var (alias, source) in LegacyAliases)
        {
            if (Resources.TryGetValue(source, out var value))
            {
                Resources[alias] = value;
            }
        }
    }

    /// <summary>切换浅色 / 深色：Fluent 控件跟随主题变体，自定义画刷直接改颜色。</summary>
    public static void ApplyTheme(bool dark)
    {
        _darkTheme = dark;
        if (Current is App app)
        {
            app.ApplyThemeCore(dark);
        }
    }

    private void ApplyThemeCore(bool dark)
    {
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        foreach (var (key, light, darkColor) in Palette)
        {
            if (Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
            {
                brush.Color = Color.Parse(dark ? darkColor : light);
            }
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        // ViewModel 构造时 Current 可能尚未就绪，这里再应用一次保存的主题。
        ApplyThemeCore(_darkTheme);
        base.OnFrameworkInitializationCompleted();
    }
}
