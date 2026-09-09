using System.Runtime.Versioning;
using MicHelper.Client.Config;
using MicHelper.Shared.Common;

namespace MicHelper.Client;

[SupportedOSPlatform("windows")]
static class Program
{
    [STAThread]
    static void Main()
    {
        var settings = ClientSettings.Load();
        AppLogger.Initialize("mic-helper-client", debugEnabled: settings.DebugLogging);

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            AppLogger.LogUnhandledException("Application.ThreadException", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLogger.LogUnhandledException("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        using var singleInstance = SingleInstanceMutex.TryAcquire("MicHelperClient");
        if (singleInstance == null)
        {
            AppLogger.Warn("Another instance is already running; exiting.");
            return;
        }

        Application.Run(new ClientTrayApplicationContext());
    }
}