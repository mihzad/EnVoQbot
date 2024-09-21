using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Data.SqlClient;
using System.Text;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using EnVoQbot.AdditionalObjects;

namespace EnVoQbot.BotCommands
{
    internal class NewWord : MultiUpdateCommand
    {
        public NewWord(long userID)
        {
            UserID = userID;
        }

        private NewWordStages currentStage = NewWordStages.GetSpelling;

        private long englishWordID = 0;
        private string? spelling = null;
        private string? transcription = null;
        private string? translation = null;

        internal override async Task ExecuteAsync(Update update)
        {
            switch(currentStage)
            {
                case NewWordStages.GetSpelling:
                    await GetSpellingAsync(update);
                    break;

                case NewWordStages.CheckSpelling:
                    await CheckSpellingAsync(update);
                    break;

                case NewWordStages.AddNewEnglishWord:
                    await AddNewEnglishWordAsync(update);
                    break;
                
                case NewWordStages.AddTranslation:
                    await AddTranslationAsync(update);
                    break;
                

            }
        }
        private async Task GetSpellingAsync(Update update)
        {
            var dataRequesting = BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
                    text:
                    "OK, let`s start. Enter the english word`s spelling."
                    );

            currentStage = NewWordStages.CheckSpelling;
            //NextNeededUpdateType = UpdateType.Message, we don`t specify because it`s default.
            BotClient.CommandsCurrentlyExecuting.AddLast(this);

            await dataRequesting;
        }
        private async Task CheckSpellingAsync(Update update)
        {
            bool IsTranslationAvailable;
            spelling = update!.Message!.Text;

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
                    "   SELECT '0', '0';";
                var tryRecognizeTheWord = new SqlCommand(
                    cmdText: Encoding.UTF8.GetString(Encoding.Default.GetBytes(commandText)),
                    connection: connection
                    );
                await connectionOpening;

                var ReadedData =  await tryRecognizeTheWord.ExecuteReaderAsync();
                await ReadedData.ReadAsync();
                englishWordID = Int64.Parse(ReadedData.GetString(0));
                IsTranslationAvailable = ( (string)ReadedData[1] != "0" );
            }

            if (englishWordID == 0)//there`s no spelling found, a new english word
            {
                var nextDataRequesting = BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Interesting word. Never met it before.\n" +
                    "How is it pronunciated? Please, enter the transcription.\n\n" +
                    "HINT: visit translate.google.com and type your word there," +
                    " then you will see the transcription in English language column."
                    );


                currentStage = NewWordStages.AddNewEnglishWord;
                await nextDataRequesting;
            }
            else if( ! IsTranslationAvailable)//the word is found in DB, but user didn`t add it to his vocabulary
            {
                var nextStageMessage = BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Hmm, I remember this one.\n" +
                    "I`ll add the transcription of it myself.\n" +
                    "Enter the translation of the word into your native language.\n"
                    );
                currentStage = NewWordStages.AddTranslation;
                await nextStageMessage;
            }

            else // we recognized the word and found it`s translation = it`s already added by the user
            {
                await BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "You have already added this word to your vocabulary.\n" +
                    "See /help if you want to edit or delete the word."
                    );
                BotClient.CommandsCurrentlyExecuting.Remove(this);//execution finished.
            }
        }

        private async Task AddNewEnglishWordAsync(Update update)
        {

            transcription = update!.Message!.Text;

            var nextDataRequesting = BotClient.Bot.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: 
                "Mmm-hmm, and please enter the translation of the word into your native language.\n"
                );

            currentStage = NewWordStages.AddTranslation;
            await nextDataRequesting;
        }

        private async Task AddTranslationAsync(Update update)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                translation = update!.Message!.Text;

                var connectionOpening = connection.OpenAsync();

                string commandText = string.Empty;
                if (englishWordID == 0)//new english word + translation to it
                    commandText =
                        "DECLARE @EnglishWordID as BIGINT;\n\n" +

                        "INSERT INTO EnglishWords\n" +
                       $"VALUES (N'{spelling}', N'{transcription}', '1');\n" +

                        "SET @EnglishWordID = SCOPE_IDENTITY();\n" +

                       $"INSERT INTO user#{update.Message.From!.Id}\n" +
                       $"   VALUES (@EnglishWordID, N'{translation}');\n";
                else //english word exists, adding translation and increasing popularity
                    commandText =
                        "Update EnglishWords\n" +
                        "SET NumberOfUsersCurrentlyUsing += 1\n" +
                       $"WHERE WordID = {englishWordID};\n" +

                       $"INSERT INTO user#{update.Message.From!.Id}\n" +
                       $"VALUES ({englishWordID}, N'{translation}');\n";

                var addWord = new SqlCommand(
                    cmdText: commandText,
                    connection: connection
                    );

                await connectionOpening;

                var addingWord = addWord.ExecuteNonQueryAsync();

                var executionFinishedMessaging = BotClient.Bot.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text:
                "New word is successfully added. See /help for instructions."
                );
                await addingWord;
                await executionFinishedMessaging;
            }

            BotClient.CommandsCurrentlyExecuting.Remove(this);//execution finished.
        }
    }
}
