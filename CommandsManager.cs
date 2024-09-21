using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using EnVoQbot.BotCommands;

namespace EnVoQbot
{
    internal static class CommandsManager
    {
        internal static async Task StartProcessing(Update update)
        {
            long userID, chatID;
            if (!CheckUpdateAndSetUserData(update, out userID, out chatID))
                return;//update doesn`t meet basic requirements.

            //command that`s executing for this user now
            var cmd = BotClient.CommandsCurrentlyExecuting.FirstOrDefault(cmd => cmd.UserID == userID);

            if (cmd == null &&
                update.Type == UpdateType.CallbackQuery)
                return;//inline button without command executing doesn`t need to work.

            if (update.Type == UpdateType.Message &&
                update.Message!.Entities != null)
                foreach (var entity in update.Message.Entities)
                    if (entity.Type == MessageEntityType.BotCommand)
                    {
                        var newCommandName = update!.Message.Text!.Substring(entity.Offset, entity.Length);

                        if (newCommandName == "/cancel")
                        {
                            await Cancel(update, cmd);
                            return;
                        }

                        if (cmd == null)// need new command
                        {
                            switch (newCommandName)
                            {
                                case "/start":
                                    await (new Start()).ExecuteAsync(update);
                                    break;

                                case "/help":
                                    await (new Help()).ExecuteAsync(update);
                                    break;

                                case "/newword":
                                    await (new NewWord(userID)).ExecuteAsync(update);
                                    break;

                                case "/editword":
                                    await (new EditWord(userID)).ExecuteAsync(update);
                                    break;

                                case "/deleteword":
                                    await (new DeleteWord(userID)).ExecuteAsync(update);
                                    break;

                                case "/settimezone":
                                    await (new SetTimeZone(userID, chatID)).ExecuteAsync(update);
                                    break;

                                case "/setquizzesschedule":
                                    await (new SetQuizzesSchedule(userID, chatID)).ExecuteAsync(update);
                                    break;

                                default:
                                    continue;
                            }
                            return;// we already found a new command, only one per message can be executed
                        }

                    }
            if (cmd != null)
                if (update.Type == cmd.NeededUpdateType)//if there is a command to execute and it hasn`t been canceled
                    await cmd.ExecuteAsync(update);
                else
                    await BotClient.Bot.SendTextMessageAsync(
                        chatId: chatID,
                        text:
                        "Can`t execute another commands because this one is already being executed.\n" +
                        "/cancel this one before choosing another."
                        );


        }
        #region auxiliary methods for commands manager
        private static bool CheckUpdateAndSetUserData(Update update, out long userID, out long chatID)
        {//check update for nulls and set user id if update is acceptable
         //returns true if acceptable, false otherwise.
            if (update.Type == UpdateType.Message)
            {
                if (update.Message == null ||
                    update.Message.From == null ||
                    update.Message.Text == null)
                {
                    userID = -1;
                    chatID = -1;
                    return false;
                }
                    
                userID = update.Message.From.Id;
                chatID = update.Message.Chat.Id;
            }
            else
            {//update.Type == UpdateType.CallbackQuery
                if (update.CallbackQuery == null ||
                    update.CallbackQuery.Message == null)
                    // => message is acceptable, because it`s bot`s message.
                {
                    userID = -1;
                    chatID = -1;
                    return false;
                }
                userID = update.CallbackQuery!.From.Id;
                chatID = update.CallbackQuery!.Message.Chat.Id;
            }
            return true;
        }
        #endregion

        //Cancel command has unique realisation and doesn`t need separate class.
        //Only multi-update command can be canceled because single-update one will finish executing immediately.
        internal static async Task Cancel(Update update, MultiUpdateCommand? cmd)
        {
            if(cmd != null)
            {
                var messageSending = BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
                    text:
                    "The command has been canceled. See /help for instructions."
                    );

                if (cmd is IUsesInlineKeyboards)
                    await ((IUsesInlineKeyboards)cmd).DeactivateInlineKeyboard("Cancelled");

                BotClient.CommandsCurrentlyExecuting.Remove(cmd);

                await messageSending;
            }
            else
                await BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
                    text:
                    "There is no command to cancel. See /help for instructions."
                    );

        }
    }
}
