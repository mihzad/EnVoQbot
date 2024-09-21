using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EnVoQbot.BotCommands
{
    internal class Help : SingleUpdateCommand
    {
        internal override async  Task ExecuteAsync(Update update)
        {
            await BotClient.Bot.SendTextMessageAsync(
                   chatId: update!.Message!.Chat.Id,
                   text: "So, here's the list of detailed descriptions of my commands:\n\n" +
                   "/start\n" +
                   "Just an introduction. It makes sense only the first time you use it,\n" +
                   "lets me to remember you.\n\n" +

                   "/newword\n" +
                   "trarara"
                   );
        }
    }
}
