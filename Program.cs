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
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        WaitForPreviousInstance(args);

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
            DebugLog.Shutdown();
        }
    }

    /// <summary>
    /// After "Restart now" the new process starts while the old one is still closing: wait for it, so the two never
    /// share the settings file or the debug log.
    /// </summary>
    private static void WaitForPreviousInstance(string[] args)
    {
        int i = Array.IndexOf(args, "--restarted-from");
        if (i < 0 || i + 1 >= args.Length || !int.TryParse(args[i + 1], out int pid))
            return;

        try
        {
            using var previous = System.Diagnostics.Process.GetProcessById(pid);
            previous.WaitForExit(15000);
        }
        catch
        {
            // It is already gone.
        }
    }

    /// <summary>
    /// Runs the startup update check and reveals the overlay window afterward via
    /// <see cref="OverlayWindowBase.ShowWindow"/> (which itself hops to the UI thread, so this is safe to
    /// await from here). "Afterward" here means once the user has actually answered the update prompt --
    /// CheckForUpdateAndPromptAsync only completes once that answer comes in, not just once the dialog is
    /// shown -- so the overlay never appears behind/alongside it. If the user confirms and the update applies
    /// successfully, the app restarts before this ever runs, so revealing the (about-to-be-replaced) window is
    /// moot in that path; if it applies but fails, the overlay is revealed same as a decline.
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