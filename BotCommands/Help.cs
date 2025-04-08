using Telegram.Bot;
using Telegram.Bot.Types;

namespace EnVoQbot.BotCommands
{
    internal class Help : SingleUpdateCommand
    {
        internal override async  Task ExecuteAsync(Update update)
        {
            await BotClient.Bot.SendMessage(
                   chatId: update!.Message!.Chat.Id,
                   text: "Here's the list of detailed descriptions of my commands:\n\n" +

                   "/addword\n" +
                   "[not completed yet XD]"
                   );
        }
    }
}
