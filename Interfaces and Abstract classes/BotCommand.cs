using Telegram.Bot.Types;

namespace EnVoQbot
{
    internal abstract class BotCommand
    {
        internal abstract Task ExecuteAsync(Update update);
    }
}
