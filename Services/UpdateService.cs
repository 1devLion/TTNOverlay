using TTNOverlay.Native;
using TTNOverlay.Overlay;
using Velopack;
using Velopack.Sources;

namespace TTNOverlay.Services;

/// <summary>
/// Checks GitHub Releases for a newer version and, if found, asks the user
/// (via a native message box) whether to update now.
/// </summary>
internal static class UpdateService
{
    private const string RepoUrl = "https://github.com/1devLion/TTNOverlay";

    public static async Task CheckForUpdateAndPromptAsync(Action<string, string, string?, Action<bool>> showConfirmDialog, Func<string, UpdateProgressDialogWindow> showProgressDialog)
    {
        try
        {
            var localOverride = Environment.GetEnvironmentVariable("TTNOVERLAY_UPDATE_SOURCE");
            var mgr = string.IsNullOrEmpty(localOverride)
            ? new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false))
            : new UpdateManager(localOverride);

            if (!mgr.IsInstalled)
                return;

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion is null)
                return;

            string notes = string.IsNullOrWhiteSpace(newVersion.TargetFullRelease.NotesMarkdown)
                ? ""
                : "\n\n" + newVersion.TargetFullRelease.NotesMarkdown;

            showConfirmDialog(
                LocalizationService.T("Update_AvailableTitle"),
                $"{LocalizationService.T("Update_AvailableMessage")} {newVersion.TargetFullRelease.Version}",
                LocalizationService.T("Update_ConfirmButton"),
                confirmed =>
                {
                    if (!confirmed)
                        return;
                    _ = ApplyUpdateAsync(mgr, newVersion, showProgressDialog);
                });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("UpdateService.CheckForUpdateAndPromptAsync", ex);
        }
    }

    private static readonly string PendingNotesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TTNOverlay", "pending_update_notes.txt");

    private static async Task ApplyUpdateAsync(UpdateManager mgr, UpdateInfo newVersion, Func<string, UpdateProgressDialogWindow> showProgressDialog)
    {
        UpdateProgressDialogWindow? progressDialog = null;
        try
        {
            progressDialog = showProgressDialog(LocalizationService.T("Update_DownloadingTitle"));
            await mgr.DownloadUpdatesAsync(
                newVersion,
                percent => progressDialog?.ReportProgress(percent, newVersion.TargetFullRelease.Size));
            progressDialog?.Close();
            SavePendingReleaseNotes(newVersion.TargetFullRelease.Version.ToString(), newVersion.TargetFullRelease.NotesMarkdown ?? "");
            mgr.ApplyUpdatesAndRestart(newVersion);

        }
        catch (Exception ex)
        {
            progressDialog?.Close();
            DebugLog.WriteException("UpdateService.ApplyUpdateAsync", ex);
        }
    }

    private static void SavePendingReleaseNotes(string version, string notes)
    {
        try
        {
            var dir = Path.GetDirectoryName(PendingNotesPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(PendingNotesPath, version + "\n" + notes);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("UpdateService.SavePendingReleaseNotes", ex);
        }
    }

    /// <summary>Call once at startup. If an update was just applied, shows the "what's new" dialog once.</summary>
    public static void CheckForPendingReleaseNotesAndShow(Action<string, string> showReleaseNotes)
    {
        try
        {
            if (!File.Exists(PendingNotesPath))
                return;

            var content = File.ReadAllText(PendingNotesPath);
            File.Delete(PendingNotesPath);

            var parts = content.Split('\n', 2);
            string version = parts[0];
            string notes = parts.Length > 1 ? parts[1] : "";
            if (string.IsNullOrWhiteSpace(notes))
                return;

            showReleaseNotes($"{LocalizationService.T("Update_WhatsNewTitle")} v{version}", notes);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("UpdateService.CheckForPendingReleaseNotesAndShow", ex);
        }
    }

}