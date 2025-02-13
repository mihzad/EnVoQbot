using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Data.SqlClient;
using System.Text;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using Quartz.Util;

namespace EnVoQbot.BotCommands
{
    internal class AddWord : MultiUpdateCommand
    {
        public AddWord(long userID)
        {
            UserID = userID;
        }

        private AddWordStages currentStage = AddWordStages.GetSpelling;

        private long englishWordID = 0;
        private string? spelling = null;
        private string? transcription = null;
        private string? translation = null;

        internal override async Task ExecuteAsync(Update update)
        {
            switch(currentStage)
            {
                case AddWordStages.GetSpelling:
                    await GetSpellingAsync(update);
                    break;

                case AddWordStages.ProcessSpelling:
                    await ProcessSpellingAsync(update);
                    break;
            }
        }
        private async Task GetSpellingAsync(Update update)
        {
            var dataRequesting = BotClient.Bot.SendMessage(
                    chatId: update!.Message!.Chat.Id,
                    text:
                    "OK, let`s start. Enter the english word`s spelling."
                    );

            currentStage = AddWordStages.ProcessSpelling;
            //NextNeededUpdateType = UpdateType.Message, we don`t specify because it`s default.
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            await dataRequesting;
        }
        private async Task ProcessSpellingAsync(Update update)
        {
            bool IsTranslationAvailable;
            spelling = update!.Message!.Text;
            if(spelling.IsNullOrWhiteSpace())
            {
                var nextStageMessage = await BotClient.Bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: "You`ve entered nothing. Please,type the word you want to add to your "+
                          "vocabulary or call /cancel to quit this operation."
                );
                return;
            }

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var commandText =
                    "DECLARE @EnglishWordID as BIGINT\n" +
                    "SET @EnglishWordID = (SELECT WordID FROM EnglishWords\n" +
                    $"    WHERE Spelling = N'{spelling}');\n\n" +

                    "IF(@EnglishWordID IS NOT NULL)\n" +
                    "   BEGIN\n" +
                    $"       IF(EXISTS(SELECT * FROM user#{update!.Message!.From!.Id} WHERE EnglishWordID = @EnglishWordID))\n" +
                    "           SELECT @EnglishWordID, 1;\n" +
                    "       ELSE\n" +
                    "           SELECT @EnglishWordID, 0;\n" +
                    "   END\n" +
                    "ELSE\n" +
                    "   SELECT CAST(0 AS BIGINT), '0';";
                var tryRecognizeTheWord = new SqlCommand(
                    cmdText: Encoding.UTF8.GetString(Encoding.Default.GetBytes(commandText)),
                    connection: connection
                    );
                await connectionOpening;

                var ReadedData =  await tryRecognizeTheWord.ExecuteReaderAsync();
                await ReadedData.ReadAsync();
                englishWordID = ReadedData.GetInt64(0);
                IsTranslationAvailable = ( ReadedData[1].ToString() != "0" );
            }

            if (englishWordID == 0 || !IsTranslationAvailable)
            {//word is not in user vocabulary, so we need translation at least => connect to LLM
                var resp = await TranslationAgent.TranslateAsync(spelling!, "Ukrainian");
                if (resp == null)
                {
                    var sendingErrMessage = BotClient.Bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: "Got an error speaking with Gemini. Please, try again later."
                    );
                    BotClient.CommandsCurrentlyExecuting.Remove(this);
                    await sendingErrMessage;
                    return;
                }
                if (resp == "NULL")
                {
                    var wrongInputMessage = await BotClient.Bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: "The input you`ve entered is intranslatable. Please, type the existing word you want to add to your " +
                          "vocabulary or call /cancel to quit this operation."
                    );
                    return;
                }
                
                string[] resp_parts = resp.Split("|");

                spelling = resp_parts[0];
                transcription = resp_parts[1];
                translation = resp_parts[2];

                await AddWordToUserVocabulary(update!.Message!.From!.Id, update!.Message!.Chat!.Id);
            }

            else // we recognized the word and found it`s translation = it`s already in user vocabulary
            {
                await BotClient.Bot.SendMessage(
                    chatId: update.Message.Chat.Id,
                    text: "You have already added this word to your vocabulary.\n" +
                    "See /help if you want to edit or delete the word."
                    );
                BotClient.CommandsCurrentlyExecuting.Remove(this);//execution finished.
            }
        }


        private async Task AddWordToUserVocabulary(long UserID, long ChatID)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                string commandText = string.Empty;
                if (englishWordID == 0)//new english word + translation to it
                    commandText =
                        "DECLARE @EnglishWordID as BIGINT;\n\n" +

                        "INSERT INTO EnglishWords\n" +
                       $"VALUES (N'{spelling}', N'{transcription}', '1');\n" +

                        "SET @EnglishWordID = SCOPE_IDENTITY();\n" +

                       $"INSERT INTO user#{UserID}\n" +
                       $"   VALUES (@EnglishWordID, N'{translation}');\n";
                else //english word exists, adding translation and increasing popularity
                    commandText =
                        "Update EnglishWords\n" +
                        "SET NumberOfUsersCurrentlyUsing += 1\n" +
                       $"WHERE WordID = {englishWordID};\n" +

                       $"INSERT INTO user#{UserID}\n" +
                       $"VALUES ({englishWordID}, N'{translation}');\n";

                var addWord = new SqlCommand(
                    cmdText: commandText,
                    connection: connection
                    );

                await connectionOpening;

                var addingWord = addWord.ExecuteNonQueryAsync();

                var executionFinishedMessaging = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text:
                $"New word, {spelling} | {transcription} | {translation}, is successfully added. See /help for instructions."
                );
                await addingWord;
                await executionFinishedMessaging;
            }

            BotClient.CommandsCurrentlyExecuting.Remove(this);//execution finished.
        }
    }
}
