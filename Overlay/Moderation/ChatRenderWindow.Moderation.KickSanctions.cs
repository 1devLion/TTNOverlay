using TTNOverlay.Kick;
using TTNOverlay.Models;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: Kick's "sanctions" list and the permission (scope) gating of the Kick moderation tools.
/// </summary>
/// <remarks>
/// Kick's public API can ban, time out and unban, but it has no call that lists who is currently banned (Twitch has
/// one). So the list is built from what this app sees: bans/timeouts it applies itself and the ban/unban events Kick
/// pushes on the chat channel while the panel is connected. Timeouts drop off when they expire.
/// Kick also lets the user untick permissions on its consent screen, so each tool checks the scope it needs and is
/// greyed out, with the missing permission named, when it wasn't granted.
/// </remarks>
internal sealed partial class ChatRenderWindow
{
    internal const string KickScopeBan = "moderation:ban";
    internal const string KickScopeDeleteMessage = "moderation:chat_message:manage";
    internal const string KickScopeChannelRead = "channel:read";

    private sealed class KickSanction
    {
        public string UserId = "";
        public string Username = "";
        public bool Permanent;
        public DateTime? ExpiresAtUtc;
    }

    private readonly Dictionary<string, KickSanction> _kickSanctions = new();

    /// <summary>One line of the Sanctions tab, from either platform.</summary>
    private sealed class SanctionRow
    {
        public Platform Platform;
        public string Name = "";
        public bool Permanent;
        public DateTime? ExpiresAtUtc;
        public KickSanction? Kick;
    }

    private readonly List<(Rect Bounds, SanctionRow Row)> _sanctionActionRects = new();

    // ------------------------------------------------------------------ permissions

    /// <summary>True only when Kick answered that the session lacks the scope; unknown (not asked yet, offline) counts as allowed.</summary>
    private bool KickScopeMissing(string scope) => _kickSession?.HasScope(scope) == false;

    /// <summary>Banning/timing out/unbanning needs the ban scope, and channel:read to find the broadcaster's id.</summary>
    private string? KickMissingBanScope =>
        KickScopeMissing(KickScopeBan) ? KickScopeBan
        : KickScopeMissing(KickScopeChannelRead) ? KickScopeChannelRead
        : null;

    private string? KickMissingDeleteScope => KickScopeMissing(KickScopeDeleteMessage) ? KickScopeDeleteMessage : null;

    private IEnumerable<string> KickMissingModerationScopes()
    {
        if (KickScopeMissing(KickScopeDeleteMessage))
            yield return KickScopeDeleteMessage;
        if (KickScopeMissing(KickScopeBan))
            yield return KickScopeBan;
        if (KickScopeMissing(KickScopeChannelRead))
            yield return KickScopeChannelRead;
    }

    /// <summary>
    /// Greys out (and names the missing permission on) the Kick menu items whose scope wasn't granted. Items of other
    /// platforms are untouched.
    /// </summary>
    private void ApplyKickScopeGate(List<ModerationDropdownItem> items, Platform platform)
    {
        if (platform != Platform.Kick)
            return;

        foreach (var item in items)
        {
            string? missing = null;
            if (item.NeedsKickDelete)
                missing = KickMissingDeleteScope;
            if (missing is null && item.NeedsKickBan)
                missing = KickMissingBanScope;
            if (missing is null)
                continue;

            item.Enabled = false;
            item.Label = item.Label + "  \u00b7  " + missing;
        }
    }

    /// <summary>Asks Kick which permissions the session has, then redraws (panel open, login, startup).</summary>
    private async Task RefreshKickGrantedScopesAsync()
    {
        try
        {
            if (!KickModerationReady)
                return;

            _kickSession ??= new KickSessionService(_settings);
            await _kickSession.RefreshGrantedScopesAsync();
            PostToUiThread(RequestRender);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("RefreshKickGrantedScopesAsync", ex);
        }
    }

    // ------------------------------------------------------------------ sanctions list

    private void RecordKickSanction(string userId, string username, bool permanent, DateTime? expiresAtUtc)
    {
        if (string.IsNullOrEmpty(username) && _kickSanctions.TryGetValue(userId, out var previous))
            username = previous.Username;

        _kickSanctions[userId] = new KickSanction
        {
            UserId = userId,
            Username = string.IsNullOrEmpty(username) ? userId : username,
            Permanent = permanent,
            // A timeout whose end Kick didn't tell us stays listed until it is lifted or the panel closes.
            ExpiresAtUtc = permanent ? null : expiresAtUtc,
        };
    }

    private void PruneKickSanctions()
    {
        List<string>? expired = null;
        var now = DateTime.UtcNow;
        foreach (var (id, sanction) in _kickSanctions)
            if (!sanction.Permanent && sanction.ExpiresAtUtc is { } end && end <= now)
                (expired ??= new List<string>()).Add(id);

        if (expired is not null)
            foreach (var id in expired)
                _kickSanctions.Remove(id);
    }

    private void HandleSanctionsTabClick(int clientX, int clientY)
    {
        foreach (var (bounds, row) in _sanctionActionRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                OpenSanctionActionsDropdown(bounds, row);
                return;
            }
        }
    }

    /// <summary>The three-dots menu of a sanction row (same pattern as the Chatters and Messages rows).</summary>
    private void OpenSanctionActionsDropdown(Rect anchor, SanctionRow row)
    {
        var item = new ModerationDropdownItem
        {
            Label = LocalizationService.T(row.Permanent ? "Moderation_UnbanLabel" : "Moderation_UnmuteLabel"),
            NeedsKickBan = true,
            OnSelect = () =>
            {
                if (row.Platform == Platform.Kick && row.Kick is { } kickSanction)
                    ConfirmKickUnban(kickSanction);
                else
                    _ = UnbanRowAsync(row.Name, row.Permanent);
            },
        };

        var items = new List<ModerationDropdownItem> { item };
        ApplyKickScopeGate(items, row.Platform); // greyed out, naming the permission, when Kick's ban scope is missing
        OpenModerationDropdown(anchor, items);
    }

    private void ConfirmKickUnban(KickSanction sanction)
    {
        bool permanent = sanction.Permanent;
        ConfirmDialogWindow.Show(
            Hwnd,
            PostToUiThread,
            LocalizationService.T(permanent ? "Moderation_ConfirmUnbanTitle" : "Moderation_ConfirmUnmuteTitle"),
            string.Format(
                LocalizationService.T(permanent ? "Moderation_ConfirmUnbanMessage" : "Moderation_ConfirmUnmuteMessage"),
                sanction.Username
            ),
            LocalizationService.T(permanent ? "Moderation_UnbanLabel" : "Moderation_UnmuteLabel"),
            confirmed =>
            {
                if (!confirmed)
                    return;

                _moderationStatusText = string.Format(
                    LocalizationService.T(permanent ? "Moderation_Unbanning" : "Moderation_UnmutingRow"),
                    sanction.Username
                );
                RequestRender();
                _ = UnbanKickAsync(sanction);
            }
        );
    }

    private async Task UnbanKickAsync(KickSanction sanction)
    {
        bool ok = false;
        if (TryGetModerationActions(Platform.Kick, out _, out var channel) && _kickModeration is not null)
            ok = await _kickModeration.UnbanAsync(channel, sanction.UserId);

        PostToUiThread(() =>
        {
            if (ok)
            {
                _kickSanctions.Remove(sanction.UserId);
                _moderationLog.MarkUserState(sanction.UserId, ModerationUserState.None, Platform.Kick);
            }

            ReportModerationResult(
                string.Format(
                    LocalizationService.T(
                        ok
                            ? (sanction.Permanent ? "Moderation_Unbanned" : "Moderation_UnmutedRow")
                            : (sanction.Permanent ? "Moderation_UnbanFailed" : "Moderation_UnmuteRowFailed")
                    ),
                    sanction.Username
                ),
                ok
            );
            RequestRender();
        });
    }
}
