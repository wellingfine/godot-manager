using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GodotManager.ViewModels;

namespace GodotManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(PickFolderAsync, ConfirmAsync);
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 Godot 安装目录",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private Task<bool> ConfirmAsync(string title, string message) =>
        new ConfirmWindow(title, message).ShowDialog<bool>(this);

    private void InstalledList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.SelectedInstalled is not null)
        {
            viewModel.LaunchCommand.Execute(viewModel.SelectedInstalled);
        }
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            _ = new SettingsWindow(viewModel).ShowDialog(this);
        }
    }
}
