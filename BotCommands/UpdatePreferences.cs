using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using System.Data;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EnVoQbot.BotCommands
{
    internal class UpdatePreferences : MultiUpdateCommand
    {
        internal UpdatePreferences(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }

        private bool PreviousPreferencesAvailable = false;
        private string PreviousPreferences = "NULL";



        private UpdatePreferencesStages currentStage = UpdatePreferencesStages.AskPreferences;

        internal override async Task ExecuteAsync(Update update)
        {
            switch (currentStage)
            {
                case UpdatePreferencesStages.AskPreferences:
                    {
                        await AskPrefs(update);
                        break;
                    }
                case UpdatePreferencesStages.UpdatePreferences:
                    {
                        await UpdatePrefs(update);
                        break;
                    }
            }
            
        }
        private async Task AskPrefs(Update update)
        {


            var sending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: $"Enter your preferences. These will influence the style your quizzes will be generated.\n\n" +
                    " For clarity, use structure like:\n" +
                    " \"- i like something (anectodes, for examle);\n" +
                    " \"- i also like smth else;\n" +
                    " \"- i don`t like smth;\n" +
                    " and so on. "
                );

            currentStage = UpdatePreferencesStages.UpdatePreferences;
            //NeededUpdateType = UpdateType.Message;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            await sending;
        }
        private async Task UpdatePrefs(Update update)
        {
            
            await GatherPreviousPrefs();

            var prefs = await PreferencesAnalyzer.Run(update.Message!.Text!, PreviousPreferences);
            if (prefs == null)
            {
                var sendingErrMessage = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Got an error analysing your preferences.\n Changes reverted. Please, try again later."
                );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await sendingErrMessage;
                return;
            }

            await SaveNewPrefset(prefs);
            await BotClient.Bot.SendMessage(
            chatId: ChatID,
            text: $"Preferences were successfully updated.\n"
            );
            BotClient.CommandsCurrentlyExecuting.Remove(this);

        }
        #region Auxiliary methods
        private async Task GatherPreviousPrefs()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var SetUserDataInDBcommand = new SqlCommand(
                    cmdText:
                    @$" 
                        DECLARE @GenPrefs NVARCHAR(MAX)

                        SET @GenPrefs = (SELECT GenerationPreferences FROM UserData WHERE UserTelegramID = '{UserID}');

                        IF @GenPrefs IS NULL
                            BEGIN
                                SELECT CAST(0 as BIT), N'NULL';
                            END
                        
                        ELSE
                            BEGIN
                                SELECT CAST (1 as BIT), @GenPrefs;
                            END
                    ",

                    connection: connection
                    );
                await connectionOpening;


                var dataReader = await SetUserDataInDBcommand.ExecuteReaderAsync();

                await dataReader.ReadAsync();

                PreviousPreferencesAvailable = dataReader.GetBoolean(0);
                if (PreviousPreferencesAvailable)
                    PreviousPreferences = dataReader.GetString(1);
            }
        }
        private async Task SaveNewPrefset(string ruleset)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var updatePrefsCMD = new SqlCommand(
                    cmdText:
                    @$" 
                        Update UserData
                            SET GenerationPreferences = @Ruleset WHERE UserTelegramID = @UserID;
                    ",

                    connection: connection
                    );
                await connectionOpening;
                updatePrefsCMD.Parameters.Add("@Ruleset", SqlDbType.NVarChar, -1).Value = ruleset;
                updatePrefsCMD.Parameters.Add("@UserID", SqlDbType.BigInt).Value = UserID;

                await updatePrefsCMD.ExecuteNonQueryAsync();
            }
        }
        #endregion
    }
}
