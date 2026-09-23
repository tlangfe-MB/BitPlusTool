using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace BitPlusTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    // Fehler auf dem UI-Thread: App am Leben halten statt abstuerzen zu lassen.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Es ist ein unerwarteter Fehler aufgetreten:\n\n" + e.Exception.Message +
            "\n\nDie Anwendung wird fortgesetzt, deine Eingaben bleiben erhalten.",
            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    // Fehler auf anderen Threads: kann nicht verhindert werden, aber wenigstens sichtbar machen.
    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        MessageBox.Show(
            "Es ist ein schwerwiegender Fehler aufgetreten:\n\n" + ex?.Message,
            "Kritischer Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}

