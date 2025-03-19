using EnVoQbot.AdditionalObjects;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EnVoQbot.BotCommands
{
    internal class DeleteWord : MultiUpdateCommand, IUsesInlineKeyboards
    {
        internal DeleteWord(long userID, long chatID)
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

        public Message? MessageWithInlineKeyboardToDelete {
            get { return messageWithInlineKeyboardToDelete; }
            set { messageWithInlineKeyboardToDelete = value; }
        }
        private Message? messageWithInlineKeyboardToDelete = null;

        private DeleteWordStages currentStage = DeleteWordStages.ChooseWordToDelete;

        private long EnglishWordID; //of candidate
        private long TranslationID;
        private int UserLanguageID;

        internal override async Task ExecuteAsync(Update update)
        {
            switch (currentStage)
            {
                case DeleteWordStages.ChooseWordToDelete:
                    await ChooseWordToDeleteAsync(update);
                    break;

                case DeleteWordStages.ConfirmDeletion:
                    {
                        //deactivate previous stage keyboard
                        var deactivating = ((IUsesInlineKeyboards)this).DeactivateInlineKeyboard("Done");
                        await ConfirmDeletionAsync(update);
                        await deactivating;
                        break;
                    }
                case DeleteWordStages.Deletion:
                        await DeleteWordAsync(update);
                        break;
            }
        }


        internal async Task ChooseWordToDeleteAsync(Update update)
        {
            var buttons = await GetVocabularyAsInlineButtons(update);

            if (await SendKeyboardToChooseWordToDelete(buttons, update))
            {
                currentStage = DeleteWordStages.ConfirmDeletion;
                NeededUpdateType = UpdateType.CallbackQuery;
                BotClient.CommandsCurrentlyExecuting.AddLast(this);
            }
        }
        #region auxiliary methods
        private async Task<List<List<InlineKeyboardButton>>> GetVocabularyAsInlineButtons(Update update)
        {
            List<List<InlineKeyboardButton>> buttons;

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var getVocabulary = new SqlCommand(
                    cmdText: $@"
                        SELECT COUNT (*)
                            FROM vocabulary#{UserID};

                        SELECT LanguageID
                            FROM UserData
                            WHERE UserTelegramID = {UserID};

                        SELECT EnglishWords.ID, Translations.ID, EnglishWords.Spelling, EnglishWords.Transcription, Translations.Translation
                        FROM Translations
                            INNER JOIN vocabulary#{UserID} ON Translations.ID = vocabulary#{UserID}.TranslationID
                            INNER JOIN EnglishWords ON Translations.WordID = EnglishWords.ID;
                    ",
                    connection: connection
                    );

                await connectionOpening;

                buttons = await CreateInlineButtonsVocabulary(await getVocabulary.ExecuteReaderAsync());
            }
            return buttons;
        }
        private async Task<List<List<InlineKeyboardButton>>> CreateInlineButtonsVocabulary(SqlDataReader ReaderToUse)
        {
            await ReaderToUse.ReadAsync();
            var buttons = new List<List<InlineKeyboardButton>>(ReaderToUse.GetInt32(0));

            await ReaderToUse.NextResultAsync();
            await ReaderToUse.ReadAsync();
            UserLanguageID = ReaderToUse.GetInt32(0);

            if (buttons.Capacity != 0)
            {
                await ReaderToUse.NextResultAsync();
                var wordCounter = 1;
                while (await ReaderToUse.ReadAsync())
                {
                    string str = $"{wordCounter}) {ReaderToUse.GetString(2)} - {ReaderToUse.GetString(3)} - {ReaderToUse.GetString(4)}";
                    buttons.Add( //i) spelling - transcription - translation
                        new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData(str, $"{ReaderToUse.GetInt64(0)}|{ReaderToUse.GetInt64(1)}") }
                        ); // use '|' ONLY as separator for vals in callback data
                    wordCounter++;

                }
            }
            return buttons;
        }

        private async Task<bool> SendKeyboardToChooseWordToDelete
            (List<List<InlineKeyboardButton>> buttons, Update update)
        {
            if (buttons.Count == 0)
            {
                await BotClient.Bot.SendMessage(
                        chatId: ChatID,
                        text: "Your vocabulary is empty. Nothing to delete.\n" +
                                "See /help for instructions."
                        );
                return false;
            }
            else
            {
                InlineKeyboardMarkup markup = new InlineKeyboardMarkup(buttons);

                var dataRequesting = BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "Delete a word? Alright.\n" +
                            "Choose one from your vocabulary:"
                            ,
                    replyMarkup: markup
                    );

                this.HasInlineKeyboardActivated = true;

                MessageWithInlineKeyboardToDelete = await dataRequesting;
                return true;
            }
        }
        #endregion
        private async Task ConfirmDeletionAsync(Update update)
        {
            var confirmationMessageSending = BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Are you sure? Type \"Yes, i am sure.\" if you are.\n" +
                "Otherwise command will be automatically canceled."
                );

            string[] data_to_parse = update!.CallbackQuery!.Data!.Split('|');

            if (!Int64.TryParse(data_to_parse[0], out EnglishWordID) || !Int64.TryParse(data_to_parse[1], out TranslationID))
            {
                Console.WriteLine("DeleteWord Error: can`t find a chosen word");
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                return;
            }
            currentStage = DeleteWordStages.Deletion;
            NeededUpdateType = UpdateType.Message;
            await confirmationMessageSending;
            
        }
        private async Task DeleteWordAsync(Update update)
        {
            if(update.Message!.Text != "Yes, i am sure.")
            {
                await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "The deletion was canceled.\n" +
                            "See /help for instructions."
                    );
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                return;
            }

            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var deleteWord = new SqlCommand(
                    cmdText: $@"
                    DELETE FROM vocabulary#{UserID}
                        WHERE TranslationID = {TranslationID};

                    UPDATE EnglishWords
                        SET Popularity -= 1
                        WHERE ID = {EnglishWordID};

                    UPDATE Translations
                        SET Popularity -= 1
                        WHERE ID = {TranslationID};

                    DELETE FROM EnglishWords
                        WHERE ID = {EnglishWordID} AND Popularity = 0 AND Spelling LIKE '% %';

                    DELETE FROM Translations
                        WHERE ID = {TranslationID} AND Popularity = 0;
                    ",
                    connection: connection
                    );

                await connectionOpening;

                var deletingWord = deleteWord.ExecuteNonQueryAsync();

                await BotClient.Bot.SendMessage(
                    chatId: ChatID,
                    text: "The word was successfully deleted.\n" +
                            "See /help for instructions."
                    );

                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await deletingWord;
            }

        }
    }
}
