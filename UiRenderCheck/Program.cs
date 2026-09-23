using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using BitPlusTool;

namespace UiRenderCheck;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();

        var window = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false
        };
        window.Show();
        window.UpdateLayout();

        void Snapshot(string path)
        {
            window.UpdateLayout();
            var w = (int)window.ActualWidth;
            var h = (int)window.ActualHeight;
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            encoder.Save(fs);
            Console.WriteLine("Saved " + path + " (" + w + "x" + h + ")");
        }

        Snapshot(@"C:\Users\tlangfe\source\UiRenderCheck\out_light.png");

        // Direkt per vollem Pack-URI laden (relative URI im Klick-Handler loest nur relativ zur
        // BitPlusTool-Assembly auf, nicht relativ zu diesem Test-Host).
        var darkDict = new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/BitPlusTool;component/Themes/DarkTheme.xaml", UriKind.Absolute)
        };
        Application.Current.Resources.MergedDictionaries[0] = darkDict;
        var moonIcon = (System.Windows.Shapes.Path)window.FindName("MoonIcon")!;
        var sunIcon = (System.Windows.Shapes.Ellipse)window.FindName("SunIcon")!;
        var sunRays = (System.Windows.Shapes.Path)window.FindName("SunRays")!;
        moonIcon.Visibility = Visibility.Visible;
        sunIcon.Visibility = Visibility.Collapsed;
        sunRays.Visibility = Visibility.Collapsed;
        Snapshot(@"C:\Users\tlangfe\source\UiRenderCheck\out_dark.png");

        var codeBox = (System.Windows.Controls.TextBox)window.FindName("CodeBox")!;
        codeBox.Text = "((A08+M254+1U2+460)/(A08+M254+1U2+494)/(A08+M254+1U2+835)/(A12+M254+1U2))+-M256+-(A08+S43+460)+-(A08+S43+494)+-(A08+S43+835)+-ME10;";
        var runButton = (System.Windows.Controls.Button)window.FindName("RunButton")!;
        runButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Snapshot(@"C:\Users\tlangfe\source\UiRenderCheck\out_blocks.png");

        var help = new HelpWindow
        {
            Owner = window,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false
        };
        help.Show();
        help.UpdateLayout();
        var hw = (int)help.ActualWidth;
        var hh = (int)help.ActualHeight;
        var helpRtb = new RenderTargetBitmap(hw, hh, 96, 96, PixelFormats.Pbgra32);
        helpRtb.Render(help);
        var helpEncoder = new PngBitmapEncoder();
        helpEncoder.Frames.Add(BitmapFrame.Create(helpRtb));
        using (var fs = File.Create(@"C:\Users\tlangfe\source\UiRenderCheck\out_help.png"))
            helpEncoder.Save(fs);
        Console.WriteLine("Saved out_help.png (" + hw + "x" + hh + ")");
        help.Close();

        window.Close();
        app.Shutdown();
        Console.WriteLine("DONE");
    }
}
