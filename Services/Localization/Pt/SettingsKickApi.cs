namespace TTNOverlay.Services;

internal static partial class PtStrings
{
    private static readonly Dictionary<string, string> SettingsKickApiEntries = new()
    {
        ["Settings_Section_KickApi"] = "API do Kick",
        ["Settings_KickApi_LoginInfo"] = "Entre com o Kick para usar as ferramentas de moderação do Kick no seu chat.",
        ["Settings_KickApi_Connected"] = "Conectado como {0}",
        ["Settings_KickApi_NotLoggedIn"] = "Você não está conectado com o Kick.",
        ["Settings_KickApi_OpeningBrowser"] = "Abrindo o navegador para entrar...",
        ["Settings_KickApi_LoginFailed"] = "Falha ao entrar. Tente novamente.",
        ["Common_LoginWithKick"] = "Entrar com Kick",
        ["Kick_ScopesChangedTitle"] = "Novas permissões do Kick",
        ["Kick_ScopesChangedMessage"] = "Esta atualização adiciona recursos que precisam de novas permissões do Kick, então sua sessão do Kick foi encerrada. Entre novamente para ativá-los.",
        ["Kick_DeleteNeedsRelogin"] = "Para moderar o chat do Kick, saia e entre novamente com o Kick (são necessárias novas permissões).",
        ["Kick_MsgNeedLogin"] = "Para moderar mensagens do Kick, entre com o Kick em Configurações > API do Kick.",
        ["Moderation_TabSanctions"] = "Sanções",
        ["Kick_PermissionMissing"] = "Falta a permissão {0} do Kick. Saia e entre novamente no Kick marcando-a na tela de permissões.",
        ["Kick_SanctionsNote"] = "O Kick não oferece uma lista de punidos: aqui aparecem os banimentos e silenciamentos vistos desde que este painel foi aberto.",
        ["Kick_SanctionsEmpty"] = "Nenhuma sanção registrada.",
        ["Auth_BrowserSuccess"] = "Pronto, você já pode fechar esta aba.",
        ["Auth_BrowserFailed"] = "Falha ao entrar. Você pode fechar esta aba.",
        ["Auth_BrowserSuccessTitle"] = "Login concluído",
    };
}
