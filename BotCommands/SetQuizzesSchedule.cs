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
    internal class SetQuizzesSchedule : MultiUpdateCommand, IUsesInlineKeyboards
    {
        internal SetQuizzesSchedule(long userID, long chatID)
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

        private SetQuizzesScheduleStages currentStage = SetQuizzesScheduleStages.SetUpChoosingKeyboard;
        private DayOfWeekScheduleData? currentScheduledDay = null;
        private TimeZoneInfo? userTimeZone = null;

        private DayOfWeekScheduleData[] daysOfWeekScheduleData = new DayOfWeekScheduleData[]
        {
            new DayOfWeekScheduleData("Monday", 0),
            new DayOfWeekScheduleData("Tuesday", 1),
            new DayOfWeekScheduleData("Wednesday", 2),
            new DayOfWeekScheduleData("Thursday", 3),
            new DayOfWeekScheduleData("Friday", 4),
            new DayOfWeekScheduleData("Saturday", 5),
            new DayOfWeekScheduleData("Sunday", 6)
        };
        private List<List<InlineKeyboardButton>>? InlineButtons;

        internal override async Task ExecuteAsync(Update update)
        {
            switch(currentStage)
            {
                case SetQuizzesScheduleStages.SetUpChoosingKeyboard:
                    {
                        await SetUpChoosingKeyboard(update);
                        break;
                    }
                case SetQuizzesScheduleStages.ChooseDaysOfWeek:
                    {
                        await ChooseDaysOfWeek(update);
                        break;
                    }
                case SetQuizzesScheduleStages.GetTimerForEachOfScheduledDays:
                    {
                        var asking = AskQuizzesCountForCurrentScheduledDay(update);
                        GetTimerForCurrentScheduledDay(update);
                        await asking;
                        break;
                    }
                case SetQuizzesScheduleStages.GetQuizzesCountForEachOfScheduledDays:
                    {
                        GetQcountForCurrentScheduledDay(update);

                        if (ProceedToNextScheduledDay(update))
                            await AskTimerForNewScheduledDay(update); // and return back to GetTimer stage

                        else // all days proceeded
                            await ConfirmScheduleUpdating(update); // and go to ScheduleJobs stage

                        break;
                    }
                case SetQuizzesScheduleStages.ScheduleJobs:
                    {
                        await ScheduleJobs(update);
                        break;
                    }
            }
        }
        
        
        private async Task SetUpChoosingKeyboard (Update update)
        {
            if (await TimeZoneIsNotSetUp(update))
                return; // responce was already sent.

            InlineButtons = CreateInlineScheduleDaysKeyboard();

            var sending = BotClient.Bot.SendTextMessageAsync(
                chatId: ChatID,
                text: "Choose which days you want\n to receive the quizzes:",
                replyMarkup: new InlineKeyboardMarkup(InlineButtons)
                );

            HasInlineKeyboardActivated = true;
            currentStage = SetQuizzesScheduleStages.ChooseDaysOfWeek;
            NeededUpdateType = UpdateType.CallbackQuery;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            MessageWithInlineKeyboardToDelete = await sending;

        
        }

        private async Task ChooseDaysOfWeek (Update update)
        {
            switch(update!.CallbackQuery!.Data!)
            {
                case "Monday": {
                        daysOfWeekScheduleData[0].ChangeStatus();
                        InlineButtons![0][0].Text = daysOfWeekScheduleData[0].keyboardButtonText;
                        break;
                    }
                case "Tuesday": {
                        daysOfWeekScheduleData[1].ChangeStatus();
                        InlineButtons![1][0].Text = daysOfWeekScheduleData[1].keyboardButtonText;
                        break;
                    }
                case "Wednesday": {
                        daysOfWeekScheduleData[2].ChangeStatus();
                        InlineButtons![2][0].Text = daysOfWeekScheduleData[2].keyboardButtonText;
                        break;
                    }
                case "Thursday": {
                        daysOfWeekScheduleData[3].ChangeStatus();
                        InlineButtons![3][0].Text = daysOfWeekScheduleData[3].keyboardButtonText;
                        break;
                    }
                case "Friday": {
                        daysOfWeekScheduleData[4].ChangeStatus();
                        InlineButtons![4][0].Text = daysOfWeekScheduleData[4].keyboardButtonText;
                        break;
                    }                   
                case "Saturday": {
                        daysOfWeekScheduleData[5].ChangeStatus();
                        InlineButtons![5][0].Text = daysOfWeekScheduleData[5].keyboardButtonText;
                        break;
                    }
                case "Sunday": {
                        daysOfWeekScheduleData[6].ChangeStatus();
                        InlineButtons![6][0].Text = daysOfWeekScheduleData[6].keyboardButtonText;
                        break;
                    }
                case "Done choosing": {
                        var deactivating = ((IUsesInlineKeyboards)this).DeactivateInlineKeyboard("Done");

                        await AskTimerForFirstOfScheduledDays(update);// and go to GetTimer stage

                        await deactivating;
                        return;
                    }  
            }
            
            var sending = BotClient.Bot.EditMessageReplyMarkupAsync(
                chatId: update!.CallbackQuery!.Message!.Chat.Id,
                messageId: update.CallbackQuery.Message!.MessageId,
                replyMarkup: new InlineKeyboardMarkup(InlineButtons!)
                );

            //HasInlineKeyboardActivated still = true;
            //currentStage still = SetQuizzesScheduleStages.ChooseDaysOfWeek;
            //NeededUpdateType still = UpdateType.CallbackQuery;

            MessageWithInlineKeyboardToDelete = await sending;
        }

        # region GetTimer stage methods
        private void GetTimerForCurrentScheduledDay(Update update)
        {
            currentScheduledDay!.timerData = update.Message!.Text!;
            //appropriate dayData in daysOfWeekScheduleData changes too.
        }
        private async Task AskQuizzesCountForCurrentScheduledDay(Update update)
        {
            var askingForQuizzesCount = BotClient.Bot.SendTextMessageAsync(
                    chatId: ChatID,
                    text: $"Now type how many quizzes you would like to receive on {currentScheduledDay!.dayName}.\n" +
                    "Type a number between 1 and 100.\n" +
                    "Input more than 100 or less than 1 will be automatically converted to appropriate cutoff value."
                    );

            currentStage = SetQuizzesScheduleStages.GetQuizzesCountForEachOfScheduledDays;
            //NeededUpdateType = UpdateType.Message;
            //InlineButtons = null;

            await askingForQuizzesCount;
            return;
        }

        #endregion

        #region GetQcount stage methods
        private void GetQcountForCurrentScheduledDay(Update update)
        {
            currentScheduledDay!.QuizzesCount = Int32.Parse(update.Message!.Text!);
            //appropriate dayData in daysOfWeekScheduleData changes too.
        }
        private bool ProceedToNextScheduledDay(Update update)
        {
            for(int i = currentScheduledDay!.IndexInSequence + 1; i < daysOfWeekScheduleData.Length; i++)
            {
                if (daysOfWeekScheduleData[i].isDayScheduled)
                {
                    currentScheduledDay = daysOfWeekScheduleData[i];
                    return true;
                }
            }
            //if we checked all the days
            return false;
        }

        private async Task ConfirmScheduleUpdating(Update update)
        {
            var confirming = BotClient.Bot.SendTextMessageAsync(
                chatId: ChatID,
                text:   "Are you sure you want to set new schedule?\n" +
                        "New schedule will be applied and the old one will be deleted.\n" +
                        "Type \"Yes, i am sure.\" if you are.\n" +
                        "Otherwise scheduling will be automatically canceled."
                );

            currentStage = SetQuizzesScheduleStages.ScheduleJobs;
            //NeededUpdateType = UpdateType.Message;

            await confirming;
        }
        private async Task AskTimerForNewScheduledDay(Update update)
        {
            var askingForScheduleTime = BotClient.Bot.SendTextMessageAsync(
                    chatId: ChatID,
                    text: $"Now type time you would like to receive your tests on {currentScheduledDay!.dayName}.\n" +
                    "Enter time in 24-hours format, hours and minutes:\n" +
                    "10:00, 15:25, 23:41, ..."
                    );

            currentStage = SetQuizzesScheduleStages.GetTimerForEachOfScheduledDays;
            //NeededUpdateType = UpdateType.Message;

            await askingForScheduleTime;
        }
        #endregion

        private async Task ScheduleJobs(Update update)
        {
            if (update.Message!.Text != "Yes, i am sure.")
            {
                var sendingResponce = BotClient.Bot.SendTextMessageAsync(
                    chatId: ChatID,
                    text: "The scheduling was canceled.\n" +
                            "See /help for instructions."
                    );

                BotClient.CommandsCurrentlyExecuting.Remove(this);

                await sendingResponce;

                return;
            }

            string userKey = $"user#{UserID}";
            var deletingPreviousSchedule = BotClient.QuizzesScheduler!.DeleteJob(new JobKey(userKey));

            IJobDetail userJob = JobBuilder.Create<GeneratePollsJob>()
                        .WithIdentity(userKey)
                        .UsingJobData("userID", $"{UserID}")
                        .UsingJobData("chatID", $"{ChatID}")
                        .Build();
            
            await deletingPreviousSchedule;
            foreach (var dayData in daysOfWeekScheduleData)
            {
                if(dayData.isDayScheduled)
                {
                    string[] time = dayData!.timerData!.Split(':');

                    int.TryParse(time[0], out int hour);
                    int.TryParse(time[1], out int minute);
                    Enum.TryParse(dayData.dayName, out DayOfWeek thisDay);

                    ITrigger dayTrigger = TriggerBuilder.Create()
                    .WithIdentity(dayData.dayName, userKey)
                    .ForJob(userKey)
                    .WithSchedule(
                        CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(thisDay, hour, minute)
                        .InTimeZone(userTimeZone!)
                        )
                    .UsingJobData("quizzesCount", $"{dayData.QuizzesCount}")
                    .Build();

                    await BotClient.QuizzesScheduler!.ScheduleJob(userJob, dayTrigger);
                }
            }
            var sendingDoneResponce = BotClient.Bot.SendTextMessageAsync(
                    chatId: ChatID,
                    text: "Your quizzes schedule was successfully set up.\n" +
                         $"Current time zone: {userTimeZone!.DisplayName}.\n" +
                          "See /help for instructions."
                );

            BotClient.CommandsCurrentlyExecuting.Remove(this);

            await sendingDoneResponce;
        }

        #region auxiliary methods

        private async Task<bool> TimeZoneIsNotSetUp(Update update)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var getTimeZoneCommand = new SqlCommand(
                    cmdText:
                        "SELECT TimeZoneID FROM UserData \n" +
                       $"WHERE UserTelegramID = '{UserID}'\n",

                    connection: connection
                    );

                await connectionOpening;
                var zoneInfo = await getTimeZoneCommand.ExecuteScalarAsync();
                if (zoneInfo == null || zoneInfo == DBNull.Value)
                {
                    var sendingNoTimeZoneResponce = BotClient.Bot.SendTextMessageAsync(
                        chatId: ChatID,
                        text: "You haven`t set your time zone.\n" +
                        " Please, use /settimezone before setting schedule."
                    );

                    BotClient.CommandsCurrentlyExecuting.Remove(this);

                    await sendingNoTimeZoneResponce;
                    return true;
                }
                userTimeZone = TimeZoneInfo.FindSystemTimeZoneById((string)zoneInfo);
            }
            return false;
        }
        private List<List<InlineKeyboardButton>> CreateInlineScheduleDaysKeyboard()
        {
            return new List<List<InlineKeyboardButton>>()
            {
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[0].keyboardButtonText, "Monday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[1].keyboardButtonText, "Tuesday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[2].keyboardButtonText, "Wednesday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[3].keyboardButtonText, "Thursday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[4].keyboardButtonText, "Friday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[5].keyboardButtonText, "Saturday") },
                new List<InlineKeyboardButton>()
                    { InlineKeyboardButton.WithCallbackData(daysOfWeekScheduleData[6].keyboardButtonText, "Sunday") },

                new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData("Done choosing") }
            };
        }

        private async Task AskTimerForFirstOfScheduledDays(Update update)
        {
            foreach (var dayData in daysOfWeekScheduleData)
            {
                if (dayData.isDayScheduled)
                {
                    var askingForScheduleTime = BotClient.Bot.SendTextMessageAsync(
                    chatId: update.CallbackQuery!.Message!.Chat.Id,
                    text: $"Now type time you would like to receive your tests on {dayData.dayName}.\n" +
                    "Enter time in 24-hours format, hours and minutes:\n" +
                    "10:00, 15:25, 23:41, ..."
                    );

                    currentScheduledDay = dayData;
                    currentStage = SetQuizzesScheduleStages.GetTimerForEachOfScheduledDays;
                    NeededUpdateType = UpdateType.Message;
                    InlineButtons = null;

                    await askingForScheduleTime;
                    return;
                }
            }

            var informingThereIsNoDayChosen = BotClient.Bot.SendTextMessageAsync(
                chatId: update.CallbackQuery!.Message!.Chat.Id,
                text: "No schedule added because no day to schedule was chosen."
                );

            BotClient.CommandsCurrentlyExecuting.Remove(this);

            await informingThereIsNoDayChosen;
        }
        #endregion
    }
}
