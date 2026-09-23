using System.Windows;

namespace BitPlusTool;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
