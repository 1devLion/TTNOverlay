using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using TTNOverlay.Twitch;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: in-memory state for the moderation panel's chatter and banned-user lists.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private const float ModerationSectionSpacing = 12f;
    private const float ModerationLineSpacing = 4f;

    private string _moderationStatusText = "";
    private string _moderationCountText = "";
    private List<(string Id, string Login)> _moderationChatters = new();
    private List<(string Id, string Login, DateTime? ExpiresAt, string Reason)>? _moderationBanned;

    private float _moderationScrollOffset;
    private float _moderationScrollOverflow;

    private ID2D1SolidColorBrush? _moderationTextBrush;
    private ID2D1SolidColorBrush? _moderationSecondaryBrush;
    private IDWriteTextFormat? _moderationHeaderFormat;
    private IDWriteTextFormat? _moderationBodyFormat;

    private readonly List<(Rect Bounds, string Id, string Login)> _moderationChatterRowRects = new();
    private readonly List<(Rect Bounds, string Login, bool IsPermanent)> _moderationBannedRowRects = new();

    private Rect? _moderationLoginActionRect;
    private bool _moderationLoginActionIsLogin;
    private bool _moderationLoginButtonHovered;

    private Rect? _moderationRefreshActionRect;

    private HelixClient.ChatSettings? _moderationChatSettings;

    private enum ModerationChatSettingField
    {
        Subscriber,
        Emote,
        Unique,
        Follower,
        Slow,
    }

    private readonly List<(Rect Bounds, ModerationChatSettingField Field)> _moderationChatSettingRowRects =
        new();

    private readonly List<(Rect Bounds, ModerationChatSettingField Field)> _moderationChatSettingButtonRects =
        new();

    private const float ChatSettingMenuButtonWidth = 26f;

    private ModerationChatSettingField? _hoveredChatSettingButton;
    private ModerationChatSettingField? _dropdownOwnerChatSettingField;
    private float _followerButtonHoverProgress;
    private float _slowButtonHoverProgress;
    private ID2D1SolidColorBrush? _moderationPillBrush;
    private const float ChatSettingPillPaddingX = 2f;
    private const float ChatSettingPillPaddingY = 3f;
    private const float ChatSettingPillCornerRadius = 6f;

    private const float ChatterActionButtonSize = 26f;

    private static readonly (int Minutes, string LabelKey)[] ModerationFollowerDurations =
    {
        (10, "MainWindow_10Minutes"),
        (30, "MainWindow_30Minutes"),
        (60, "MainWindow_1Hour"),
        (1440, "MainWindow_1Day"),
        (10080, "MainWindow_1Week"),
        (43200, "MainWindow_1Month"),
    };

    private static readonly (int Seconds, string LabelKey)[] ModerationSlowDurations =
    {
        (3, "MainWindow_3Seconds"),
        (5, "MainWindow_5Seconds"),
        (10, "MainWindow_10Seconds"),
        (30, "MainWindow_30Seconds"),
        (60, "MainWindow_60Seconds"),
        (120, "MainWindow_120Seconds"),
    };

}

