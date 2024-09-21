using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EnVoQbot
{
    internal abstract class MultiUpdateCommand : BotCommand
    {
        internal long UserID { get; set; }

        //here we specify what update type we expect. Default: message.
        internal UpdateType NeededUpdateType { get; set; } = UpdateType.Message;
    }
}
