using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EnVoQbot.BotCommands
{
    internal class ClearPreferences : MultiUpdateCommand
    {
        internal ClearPreferences(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }
       
        private ClearPreferencesStages currentStage = ClearPreferencesStages.Confirming;

        internal override async Task ExecuteAsync(Update update)
        {
            switch (currentStage)
            {
                case ClearPreferencesStages.Confirming:
                    {
                        await Confirm(update);
                        break;
                    }
                case ClearPreferencesStages.Clearing:
                    {
                        await Clear(update);
                        break;
                    }
            }
            
        }
        private async Task Confirm(Update update)
        {


            var sending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Are you sure you want to clear all your preferences? " +
                "This will reset quiz generation style. Type 'Yes, i am sure.' to continue."
                );

            currentStage = ClearPreferencesStages.Clearing;
            //NeededUpdateType = UpdateType.Message;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            await sending;
        }
        private async Task Clear(Update update)
        {
            if (update.Message!.Text != "Yes, i am sure.")
            {
                await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "Preferences removal was canceled.\n" +
                            "See /help for instructions."
                    );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                return;
            }

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var SetUserDataInDBcommand = new SqlCommand(
                    cmdText:
                    @$" 
                        UPDATE UserData
                            SET GenerationPreferences = NULL
                            WHERE UserTelegramID = '{UserID}';
                    ",

                    connection: connection
                    );
                await connectionOpening;


                var executing = SetUserDataInDBcommand.ExecuteNonQueryAsync();
                await BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: $"Your preferences were successfully cleared.\n"
                );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await executing;

            }
        }
    }
}
