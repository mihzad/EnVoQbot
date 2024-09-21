using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace EnVoQbot
{
    internal abstract class BotCommand
    {
        internal abstract Task ExecuteAsync(Update update);
    }
}
