namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "Kick API",
        ["Settings_KickApi_LoginInfo"] = "Войдите через Kick, чтобы использовать инструменты модерации Kick в чате.",
        ["Settings_KickApi_Connected"] = "Подключено как {0}",
        ["Settings_KickApi_NotLoggedIn"] = "Вы не вошли через Kick.",
        ["Settings_KickApi_OpeningBrowser"] = "Открываем браузер для входа...",
        ["Settings_KickApi_LoginFailed"] = "Не удалось войти. Попробуйте ещё раз.",
        ["Common_LoginWithKick"] = "Войти через Kick",
        ["Kick_ScopesChangedTitle"] = "Новые разрешения Kick",
        ["Kick_ScopesChangedMessage"] = "В этом обновлении появились функции, которым нужны новые разрешения Kick, поэтому ваш сеанс Kick был завершён. Войдите снова, чтобы включить их.",
        ["Kick_DeleteNeedsRelogin"] = "Чтобы модерировать чат Kick, выйдите и снова войдите через Kick (нужны новые разрешения).",
        ["Kick_MsgNeedLogin"] = "Чтобы модерировать сообщения Kick, войдите через Kick в Настройки > Kick API.",
        ["Moderation_TabSanctions"] = "Санкции",
        ["Kick_PermissionMissing"] = "Нет разрешения Kick: {0}. Выйдите и снова войдите в Kick, отметив его на экране разрешений.",
        ["Kick_SanctionsNote"] = "В Kick нет списка наказанных пользователей: здесь показаны баны и таймауты, замеченные с момента открытия этой панели.",
        ["Kick_SanctionsEmpty"] = "Санкций нет.",
        ["Auth_BrowserSuccess"] = "Готово, можете закрыть эту вкладку.",
        ["Auth_BrowserFailed"] = "Не удалось войти. Можете закрыть эту вкладку.",
        ["Auth_BrowserSuccessTitle"] = "Вход выполнен",
    };
}
