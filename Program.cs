using Velopack;
using TTNOverlay.Overlay;
using TTNOverlay.Services;
using TTNOverlay.Native;
namespace TTNOverlay;

/// <summary>
/// Application entry point: loads settings, applies theme/language, and starts the native overlay window.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().Run();

        DebugLog.Write("App starting. Log in: " + DebugLog.FilePath);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                DebugLog.WriteException("AppDomain.UnhandledException", ex);
            DebugLog.FlushNow();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DebugLog.WriteException("UnobservedTaskException", args.Exception);
            DebugLog.FlushNow();
            args.SetObserved();
        };

        var settings = SettingsService.Load();
        DebugLog.Enabled = settings.EnableDebugMode;
        ThemeService.Apply(settings.Theme);
        LocalizationService.Instance.SetLanguage(AppLanguageExtensions.FromSettingsLabel(settings.Language));

        try
        {
            using var chatRenderWindow = new ChatRenderWindow();
            chatRenderWindow.Create(
                "TTNOverlay",
                (int)settings.WindowLeft,
                (int)settings.WindowTop,
                (int)settings.WindowWidth,
                (int)settings.WindowHeight
            );
            UpdateService.CheckForPendingReleaseNotesAndShow(chatRenderWindow.ShowReleaseNotesDialog);
            _ = UpdateService.CheckForUpdateAndPromptAsync(chatRenderWindow.ShowConfirmDialog, chatRenderWindow.ShowUpdateProgressDialog);
            chatRenderWindow.RunMessageLoop();
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("UnhandledException", ex);
            Win32.MessageBoxW(
                IntPtr.Zero,
                $"Unhandled error:\n{ex.Message}\n\nDetail in:\n{DebugLog.FilePath}",
                "TTNOverlay - Error",
                Win32.MB_OK | Win32.MB_ICONERROR
            );
        }
        finally
        {
            SharedGraphicsResources.Shutdown();
        }
    }
}