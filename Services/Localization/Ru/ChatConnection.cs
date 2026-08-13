namespace TTNOverlay.Services;

internal static partial class RuStrings
{
    private static readonly Dictionary<string, string> ChatConnectionEntries = new()
    {
        ["MainWindow_ChannelConnected"] = "#{0} (подключено)",
        ["MainWindow_ErrorLabel"] = "Ошибка: {0}",
        ["MainWindow_Disconnected"] = "Отключено: {0}",
        ["MainWindow_Connecting"] = "Подключение к #{0}...",
        ["MainWindow_FirstTime"] = "Канал не настроен",
        ["MainWindow_ConnectFailedTitle"] = "Ошибка подключения",
        ["MainWindow_ConnectFailedBody"] = "Не удалось подключиться к каналу '{0}':\n{1}\n\nПолный журнал:\n{2}",
    };
}