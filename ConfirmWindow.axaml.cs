using Avalonia.Controls;
using Avalonia.Interactivity;
using GodotManager.Services;

namespace GodotManager;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
    {
        InitializeComponent();
        Title = LocalizationService.Instance.T("ConfirmTitle");
        OkButton.Content = LocalizationService.Instance.T("Ok");
        CancelButton.Content = LocalizationService.Instance.T("Cancel");
    }

    public ConfirmWindow(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
