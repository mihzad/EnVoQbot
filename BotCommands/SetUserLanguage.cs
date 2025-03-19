using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EnVoQbot.BotCommands
{
    internal class SetUserLanguage : MultiUpdateCommand
    {
        internal SetUserLanguage(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }

       
        private SetUserLanguageStages currentStage = SetUserLanguageStages.WarnAboutConsequences;

        internal override async Task ExecuteAsync(Update update)
        {
            switch(currentStage)
            {
                case SetUserLanguageStages.WarnAboutConsequences:
                    {
                        await Warn(update);
                        break;
                    }
                case SetUserLanguageStages.ConfirmChange:
                    {
                        await ConfirmChange(update);
                        break;
                    }
                case SetUserLanguageStages.SetNewLanguage:
                    {
                        await SetNewLanguage(update);
                        break;
                    }
            }

        }
        private async Task Warn(Update update)
        {
            var sending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Are you sure you want to change your language? " +
                "Your vocabulary will be erased. Type 'Yes, i am sure.' to continue."
                );

            currentStage = SetUserLanguageStages.ConfirmChange;
            //NeededUpdateType = UpdateType.Message;
            BotClient.CommandsCurrentlyExecuting.AddLast(this);
            await sending;
        }
        private async Task ConfirmChange(Update update)
        {
            if (update.Message!.Text != "Yes, i am sure.")
            {
                await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "Language change was canceled.\n" +
                            "See /help for instructions."
                    );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                return;
            }
            var sending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Mhmm, now enter your new native language name, please."
                );

            currentStage = SetUserLanguageStages.SetNewLanguage;
            //NeededUpdateType = UpdateType.Message;
            await sending;
        }

        private async Task SetNewLanguage(Update update)
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
                var SetUserDataDBC = new SqlCommand(
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
                
                        UPDATE UserData
                        SET LanguageID = @languageID
                        WHERE UserTelegramID = '{UserID}'
                    ",

                    connection: connection
                );

                var DeleteCurrentVocabularyDBC = new SqlCommand(
                    cmdText:
                    @$"
                        UPDATE Translations
                            SET Popularity -= 1
                            WHERE ID IN (SELECT TranslationID FROM vocabulary#{UserID});

                        DECLARE @EngVocabularyIDs TABLE (ID INT);

                        INSERT INTO @EngVocabularyIDs (ID)
                            SELECT DISTINCT WordID 
                            FROM Translations 
                            WHERE ID IN (SELECT TranslationID FROM vocabulary#{UserID}) 

                        UPDATE EnglishWords
                            SET Popularity -= 1
                            WHERE ID IN ( SELECT DISTINCT ID FROM @EngVocabularyIDs);

                        DELETE FROM vocabulary#{UserID};
                        
                        DELETE FROM Translations
                        WHERE Popularity = 0;

                        DELETE FROM EnglishWords
                        WHERE Popularity = 0 AND Spelling LIKE '% %';
                    ",
                    connection: connection
                );
                await connectionOpening;


                await SetUserDataDBC.ExecuteNonQueryAsync();
                var deletingVocabulary = DeleteCurrentVocabularyDBC.ExecuteNonQueryAsync();
                await BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: $"Native language, {languageName}, was successfully set.\n"
                );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await deletingVocabulary;
            }
        }
    }
}
