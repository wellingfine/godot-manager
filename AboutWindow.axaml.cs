using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using GodotManager.Services;
using GodotManager.ViewModels;

namespace GodotManager;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    public AboutWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        VersionTextBlock.Text = AppInfo.Version;
        AuthorTextBlock.Text = AppInfo.Author;
        ProjectLink.Content = AppInfo.RepositoryDisplay;
    }

    private void ProjectLink_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            ShellService.OpenUrl(AppInfo.RepositoryUrl);
        }
        catch (Exception)
        {
            // 打开链接失败时不影响关于窗口。
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e) => Close();
}
