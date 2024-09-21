using EnVoQbot.AdditionalObjects;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EnVoQbot.BotCommands
{
    internal class SetTimeZone : MultiUpdateCommand, IUsesInlineKeyboards
    {
        internal SetTimeZone(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }

        public bool HasInlineKeyboardActivated
        {
            get { return hasInlineKeyboardActivated; }
            set { hasInlineKeyboardActivated = value; }
        }
        private bool hasInlineKeyboardActivated = false;

        public Message? MessageWithInlineKeyboardToDelete
        {
            get { return messageWithInlineKeyboardToDelete; }
            set { messageWithInlineKeyboardToDelete = value; }
        }
        private Message? messageWithInlineKeyboardToDelete = null;

        private long ChatID { get; set; }

        private SetTimeZoneStages currentStage = SetTimeZoneStages.ChooseTimeZone;

        internal override async Task ExecuteAsync(Update update)
        {
            switch(currentStage)
            {
                case SetTimeZoneStages.ChooseTimeZone:
                    {
                        await ChooseTimeZone(update);
                        break;
                    }
                case SetTimeZoneStages.SaveTimeZone:
                    {
                        await ((IUsesInlineKeyboards)this).DeactivateInlineKeyboard("Done");
                        await SaveTimeZone(update);
                        break;
                    }
            }

        }

        private async Task ChooseTimeZone(Update update)
        {
            var inlineButtons = new List<List<InlineKeyboardButton>>();

            foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
            {
                inlineButtons.Add(new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(zone.DisplayName, zone.Id) }
                );
            }

            var sending = BotClient.Bot.SendTextMessageAsync(
                chatId: ChatID,
                text: "Choose your time zone:",
                replyMarkup: new InlineKeyboardMarkup(inlineButtons)
                );

            HasInlineKeyboardActivated = true;
            currentStage = SetTimeZoneStages.SaveTimeZone;
            NeededUpdateType = UpdateType.CallbackQuery;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            MessageWithInlineKeyboardToDelete = await sending;
        }

        private async Task SaveTimeZone(Update update)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var setTimeZoneCommand = new SqlCommand(
                    cmdText:
                        "UPDATE UserData \n" +
                       $"SET TimeZoneID = '{update.CallbackQuery!.Data!}'\n" +
                       $"WHERE UserTelegramID = '{UserID}'\n",

                    connection: connection
                    );

                await connectionOpening;
                var executing = setTimeZoneCommand.ExecuteNonQueryAsync();

                string response = await ChangeScheduleAccordingToTimeZone(update);
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await BotClient.Bot.SendTextMessageAsync(
                chatId: ChatID,
                text: "New time zone was successfully set.\n" +
                      response +
                      "See /help for instructions."
                );

                await executing;
            }
        }

        private async Task<string> ChangeScheduleAccordingToTimeZone(Update update)
        {
            try
            {
                string userKey = $"user#{UserID}";

                var triggersToChange = await BotClient.QuizzesScheduler!.GetTriggersOfJob(new JobKey(userKey));

                if (triggersToChange == null || triggersToChange.Count() == 0)
                    return "Now you can /setquizzesschedule.\n";

            
                ITrigger[] newTriggers = new ITrigger[triggersToChange.Count];
                for (int i = 0; i < triggersToChange.Count; i++)
                {
                    var tr = triggersToChange.ElementAt(i);

                    var trFireDateTimeOffset = tr.GetFireTimeAfter(DateTimeOffset.UtcNow)!.Value;
                    var trBuilder = tr.GetTriggerBuilder();

                    newTriggers[i] = trBuilder.WithSchedule(
                        CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(trFireDateTimeOffset.DayOfWeek,
                                                                        trFireDateTimeOffset.Hour, trFireDateTimeOffset.Minute)
                                            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(update.CallbackQuery!.Data!))
                        )
                        .UsingJobData(tr.JobDataMap)
                        .Build();
                    Console.WriteLine(newTriggers[i].ToString());
                }

                for (int i = 0; i < triggersToChange.Count; i++)
                {
                    await BotClient.QuizzesScheduler.RescheduleJob(triggersToChange.ElementAt(i).Key, newTriggers[i]);
                }

                return "Your schedule was changed according to the new time zone.\n";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                throw new NotImplementedException();
            }
        }
    }
}
