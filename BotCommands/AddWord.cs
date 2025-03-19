using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Data.SqlClient;
using System.Text;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using EnVoQbot.AdditionalObjects;
using EnVoQbot.LLM;
using Quartz.Util;
using Microsoft.Identity.Client;

namespace EnVoQbot.BotCommands
{
    internal class AddWord : MultiUpdateCommand
    {
        public AddWord(long userID, long chatID)
        {
            UserID = userID;
            ChatID = chatID;
        }

        private AddWordStages currentStage = AddWordStages.GetSpelling;

        private long EnglishWordID = 0;
        private long TranslationID = 0;
        private bool AlreadyInVocabulary = false;
        private string UserLanguage = string.Empty;
        private int UserLanguageID = 0;

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
                    chatId: ChatID,
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
            spelling = update!.Message!.Text;
            if(spelling.IsNullOrWhiteSpace())
            {
                var nextStageMessage = await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "You`ve entered nothing. Please,type the word you want to add to your "+
                          "vocabulary or call /cancel to quit this operation."
                );
                return;
            }

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                await connection.OpenAsync();
                //setup EnglishWordID, TranslationFound, AlreadyInVocabulary, UserLanguage
                await CheckDbFor(spelling!, connection);

                if (EnglishWordID == 0 || TranslationID == 0)
                {//word is clearly not in user vocabulary, need spelling and/or translation data

                    var resp = await TranslationAgent.TranslateAsync(spelling!, UserLanguage);
                    if (resp == null)
                    {
                        var sendingErrMessage = BotClient.Bot.SendMessage(
                        chatId: ChatID,
                        text: "Got an error speaking with Gemini. Please, try again later."
                        );
                        BotClient.CommandsCurrentlyExecuting.Remove(this);
                        await sendingErrMessage;
                        return;
                    }
                    if (resp == "NULL")
                    {
                        var wrongInputMessage = await BotClient.Bot.SendMessage(
                        chatId: ChatID,
                        text: "The input you`ve entered is intranslatable. Please, type the existing english word you want to add to your " +
                              "vocabulary or call /cancel to quit this operation."
                        );
                        return;
                    }

                    string[] resp_parts = resp.Split("|");

                    spelling = resp_parts[0];
                    transcription = resp_parts[1];
                    translation = resp_parts[2];

                    await AddSpellingAndTranslationData(connection);
                    var linking = LinkWordToUserVocabulary(connection);

                    await BotClient.Bot.SendMessage(
                        chatId: ChatID,
                        text:
                        $"New word, {spelling} | {transcription} | {translation}, is successfully added. See /help for instructions."
                        );

                    await linking;
                }
                else if (!AlreadyInVocabulary)
                {
                    //add translation link to vocabulary#{UserID}
                    await LinkWordToUserVocabulary(connection);

                    await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text:
                    $"New word, {spelling} | {transcription} | {translation}, is successfully added. See /help for instructions."
                    );

                }

                else //it`s already in user vocabulary
                {
                    await BotClient.Bot.SendMessage(
                        chatId: ChatID,
                        text: "You have already added this word to your vocabulary.\n" +
                        "See /help if you want to edit or delete the word."
                        );
                }
            }
            BotClient.CommandsCurrentlyExecuting.Remove(this);//execution finished.

        }

        private async Task CheckDbFor(string spelling, SqlConnection DBconnection)
        {
            var analyzeSpellingCmdText =
                @$"
                DECLARE @EnglishWordID BIGINT;
                DECLARE @LocalTranslationID BIGINT;
                DECLARE @AlreadyInVocabulary BIT;
                DECLARE @c1 INT;

                DECLARE @UserLanguage NVARCHAR(100);
                DECLARE @UserLanguageID INT;

                SELECT @UserLanguageID = Languages.ID, @UserLanguage = Languages.Name 
                    FROM Languages INNER JOIN UserData ON Languages.ID = UserData.LanguageID
                    WHERE UserTelegramID = {UserID};

                SET @EnglishWordID = (SELECT ID FROM EnglishWords
                    WHERE Spelling = N'{spelling}');

                IF ( @EnglishWordID IS NOT NULL )
                    BEGIN
                        SELECT @LocalTranslationID = ID FROM Translations WHERE WordID = @EnglishWordID AND LanguageID = @UserLanguageID;
                        IF ( @LocalTranslationID IS NOT NULL )
                            BEGIN
                                IF ( EXISTS (SELECT 1 FROM vocabulary#{UserID} WHERE TranslationID = @LocalTranslationID)) 
                                    BEGIN
                                            SET @c1 = 1;
                                            SET @AlreadyInVocabulary = 1;
                                    END
                                ELSE
                                    BEGIN
                                        SET @c1 = 2;
                                        SET @AlreadyInVocabulary = 0;
                                    END
                            END
                        ELSE
                            BEGIN
                                SET @c1 = 3;
                                SET @LocalTranslationID = CAST(0 AS BIGINT);
                                SET @AlreadyInVocabulary = 0;
                            END
                    END
                ELSE
                    BEGIN
                        SET @EnglishWordID = CAST(0 AS BIGINT);
                        SET @LocalTranslationID = CAST(0 AS BIGINT);
                        SET @AlreadyInVocabulary = 0;
                    END
                
                

                SELECT @EnglishWordID, @LocalTranslationID, @AlreadyInVocabulary, @UserLanguageID, @UserLanguage, @c1;
                ";
            var tryRecognizeTheWord = new SqlCommand(
                cmdText: Encoding.UTF8.GetString(Encoding.Default.GetBytes(analyzeSpellingCmdText)),
                connection: DBconnection
                );

            using (SqlDataReader ReadedData = await tryRecognizeTheWord.ExecuteReaderAsync())
            {
                await ReadedData.ReadAsync();

                EnglishWordID = ReadedData.GetInt64(0);
                TranslationID = ReadedData.GetInt64(1);
                AlreadyInVocabulary = ReadedData.GetBoolean(2);
                UserLanguageID = ReadedData.GetInt32(3);
                UserLanguage = ReadedData.GetString(4);
            }
               
            
        }

        private async Task AddSpellingAndTranslationData(SqlConnection DBconnection)
        {
            string cmdText = string.Empty;
            if (EnglishWordID == 0)//add new english word + translation to it
            {
                cmdText = @$"
                    DECLARE @EnglishWordID BIGINT
                    DECLARE @TranslationID BIGINT

                    INSERT INTO EnglishWords (Spelling, Transcription, Popularity)
                        VALUES (N'{spelling}', N'{transcription}', '0');

                    SET @EnglishWordID = SCOPE_IDENTITY();
                        
                   
                    INSERT INTO Translations (WordID, LanguageID, Translation, Popularity)
                            VALUES (@EnglishWordID, {UserLanguageID}, N'{translation}', 0);

                    SET @TranslationID = SCOPE_IDENTITY();
                    SELECT @EnglishWordID, @TranslationID
                ";
            }
            else //english word exists => adding translation
            {
                cmdText = @$"
                    DECLARE @TranslationID BIGINT

                    INSERT INTO Translations (WordID, LanguageID, Translation, Popularity)
                         VALUES ({EnglishWordID}, {UserLanguageID}, N'{translation}', 0);

                    SET @TranslationID = SCOPE_IDENTITY();
                    SELECT CAST(0 AS BIGINT), @TranslationID;
                ";
            }
            var addWord = new SqlCommand(
                cmdText: cmdText,
                connection: DBconnection
                );

            using (SqlDataReader ReadedData = await addWord.ExecuteReaderAsync())
            {
                await ReadedData.ReadAsync();
                //we need to capture TranslationID in order to link vocabulary to it.
                if (EnglishWordID == 0)
                    EnglishWordID = ReadedData.GetInt64(0);
                TranslationID = ReadedData.GetInt64(1);
            }
                
        }
        private async Task LinkWordToUserVocabulary(SqlConnection DBconnection)
        {
            string commandText = $@"
                DECLARE @WordDataVar TABLE(
                    ID BIGINT NOT NULL,
                    Spelling NVARCHAR(MAX),
                    Transcription NVARCHAR(MAX)
                    );

                DECLARE @TranslationDataVar TABLE(
                    WordID BIGINT NOT NULL,
                    Translation NVARCHAR(MAX)
                    );
                
                UPDATE EnglishWords
                    SET Popularity += 1
                    OUTPUT INSERTED.ID, INSERTED.Spelling, INSERTED.Transcription INTO @WordDataVar
                    WHERE ID = {EnglishWordID};


                UPDATE Translations
                    SET Popularity += 1
                    OUTPUT INSERTED.WordID, INSERTED.Translation INTO @TranslationDataVar
                    WHERE ID = {TranslationID};

                INSERT INTO vocabulary#{UserID} (TranslationID)
                    VALUES ({TranslationID});
                
                SELECT Spelling, Transcription, Translation
                    FROM @WordDataVar INNER JOIN @TranslationDataVar ON [@WordDataVar].ID = [@TranslationDataVar].WordID;
            ";

            var addWord = new SqlCommand(
                cmdText: commandText,
                connection: DBconnection
                );

            using (SqlDataReader readedData = await addWord.ExecuteReaderAsync())
            {
                await readedData.ReadAsync();
                spelling = readedData.GetString(0); //debug: var spelling2 & assert
                transcription = readedData.GetString(1);
                translation = readedData.GetString(2);

            }
        }
    }
}
