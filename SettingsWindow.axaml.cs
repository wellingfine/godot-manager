using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using GodotManager.Services;
using GodotManager.ViewModels;

namespace GodotManager;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        VersionTextBlock.Text = AppInfo.Version;
        AuthorTextBlock.Text = AppInfo.Author;
        RepositoryLink.Content = AppInfo.RepositoryDisplay;
    }

    private void About_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            _ = new AboutWindow(viewModel).ShowDialog(this);
        }
    }

    private void RepositoryLink_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            ShellService.OpenUrl(AppInfo.RepositoryUrl);
        }
        catch (Exception)
        {
            // 打开链接失败时不影响设置窗口。
        }
    }
}
