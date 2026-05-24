using System.Windows;

namespace brightnessControl;

public partial class CloseChoiceWindow : Window
{
    public CloseChoiceWindow()
    {
        InitializeComponent();
    }

    public CloseChoice Choice { get; private set; } = CloseChoice.Cancel;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.MinimizeToTray;
        DialogResult = true;
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.Exit;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseChoice.Cancel;
        DialogResult = false;
    }
}
