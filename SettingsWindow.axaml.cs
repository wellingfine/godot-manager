using Avalonia.Controls;
using Avalonia.Interactivity;
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
    }

    private void About_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            _ = new AboutWindow(viewModel).ShowDialog(this);
        }
    }

    private void Done_Click(object? sender, RoutedEventArgs e) => Close();
}
