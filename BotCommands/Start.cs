using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EnVoQbot.BotCommands
{
    internal class Start : MultiUpdateCommand
    {
        internal Start(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }
       
        private StartupStages currentStage = StartupStages.AskLanguage;

        internal override async Task ExecuteAsync(Update update)
        {
            switch (currentStage)
            {
                case StartupStages.AskLanguage:
                    {
                        await AskLanguage(update);
                        break;
                    }
                case StartupStages.SetLanguage:
                    {
                        await SetLanguage(update);
                        break;
                    }
            }
            
        }
        private async Task AskLanguage(Update update)
        {
            

            var sending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: $"Hello, {update!.Message!.From!.FirstName}!\n" +
                    " I can generate quizzes using words you specified.\n" +
                    " Whenever you want and as much as you want.\n" +
                    "In order to start, please enter your native language:"
                );

            currentStage = StartupStages.SetLanguage;
            //NeededUpdateType = UpdateType.Message;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            await sending;
        }
        private async Task SetLanguage(Update update)
        {
            var languageName = await LanguageDetectionAgent.AnalyseAsync(update.Message!.Text!);
            if (languageName == null)
            {
                var sendingErrMessage = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Got an error speaking with Gemini. Please, try again later."
                );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await sendingErrMessage;
                return;
            }
            if (languageName == "NULL")
            {
                var wrongInputMessage = await BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "The input you`ve entered is not a language name. Please, type existing one."
                );
                return;
            }

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var SetUserDataInDBcommand = new SqlCommand(
                    cmdText:
                    @$" 
                        DECLARE @languageID INT

                        SET @languageID = (SELECT ID FROM Languages WHERE Name = '{languageName}');

                        IF @languageID IS NULL
                        BEGIN
                            INSERT INTO Languages (Name)
                            VALUES( N'{languageName}' )
                            SET @languageID = SCOPE_IDENTITY();
                        END


                        IF (NOT EXISTS (SELECT *
                            FROM INFORMATION_SCHEMA.TABLES
                            WHERE  TABLE_NAME = 'vocabulary#{UserID}'))
                        BEGIN
                              CREATE TABLE vocabulary#{UserID} (
                                TranslationID BIGINT PRIMARY KEY REFERENCES Translations(ID))
                        END

                        IF(NOT EXISTS (SELECT * 
                           FROM UserData 
                           WHERE UserTelegramID = '{UserID}'))
                        BEGIN
                            INSERT INTO UserData (UserTelegramID, Username, LanguageID)
                            VALUES( '{UserID}', N'{update!.Message!.From!.Username}', @languageID)
                        END
                    ",

                    connection: connection
                    );
                await connectionOpening;


                var executing = SetUserDataInDBcommand.ExecuteNonQueryAsync();
                await BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: $"Native language, {languageName}, was successfully set.\n"
                );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await executing;

            }
        }
    }
}
