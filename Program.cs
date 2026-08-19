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
                (int)settings.WindowHeight,
                visible: false
            );
            UpdateService.CheckForPendingReleaseNotesAndShow(chatRenderWindow.ShowReleaseNotesDialog);

            // The overlay stays hidden (created above with visible: false) until the update prompt
            // below has been shown (or skipped, if there's nothing to update). This way the update
            // dialog is always the first thing the user sees on startup instead of appearing on top
            // of an already-visible overlay a moment later.
            _ = RunStartupUpdateCheckAsync(chatRenderWindow);

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

    /// <summary>
    /// Runs the startup update check and, whether or not an update prompt ends up being shown,
    /// reveals the overlay window afterward via <see cref="OverlayWindowBase.ShowWindow"/> (which
    /// itself hops to the UI thread, so this is safe to await from here). If an update is confirmed
    /// and applied, the app restarts before this ever runs, so revealing the (about-to-be-replaced)
    /// window is moot in that path.
    /// </summary>
    private static async Task RunStartupUpdateCheckAsync(ChatRenderWindow chatRenderWindow)
    {
        try
        {
            await UpdateService.CheckForUpdateAndPromptAsync(chatRenderWindow.ShowConfirmDialog, chatRenderWindow.ShowUpdateProgressDialog);
        }
        finally
        {
            chatRenderWindow.ShowWindow();
        }
    }
}