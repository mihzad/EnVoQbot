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
        internal DeleteWord(long userID)
        {
            UserID = userID;
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
        private long englishWordID;


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
                    cmdText:
                    "SELECT COUNT (*)\n" +
                    "FROM EnglishWords\n" +
                   $"INNER JOIN user#{update!.Message!.From!.Id} On EnglishWords.WordID = user#{update!.Message!.From!.Id}.EnglishWordID;\n" +

                   $"SELECT EnglishWords.WordID, EnglishWords.Spelling, EnglishWords.Transcription, user#{update!.Message!.From!.Id}.Translation\n" +
                    "FROM EnglishWords\n" +
                   $"INNER JOIN user#{update!.Message!.From!.Id} On EnglishWords.WordID = user#{update!.Message!.From!.Id}.EnglishWordID;\n"
                    ,
                    connection: connection
                    );

                await connectionOpening;

                buttons = CreateInlineButtonsVocabulary(await getVocabulary.ExecuteReaderAsync());
            }
            return buttons;
        }
        private List<List<InlineKeyboardButton>> CreateInlineButtonsVocabulary(SqlDataReader ReaderToUse)
        {
            ReaderToUse.Read();
            var buttons = new List<List<InlineKeyboardButton>>(ReaderToUse.GetInt32(0));

            if (buttons.Capacity != 0)
            {
                ReaderToUse.NextResult();
                var wordCounter = 1;
                while (ReaderToUse.Read())
                {
                    string str = $"{wordCounter}) {ReaderToUse.GetString(1)} - {ReaderToUse.GetString(2)} - {ReaderToUse.GetString(3)}";
                    buttons.Add( //i) spelling - transcription - translation
                        new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData(str, $"{ReaderToUse.GetInt64(0)}") }
                        );
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
                await BotClient.Bot.SendTextMessageAsync(
                        chatId: update!.Message!.Chat.Id,
                        text: "Your vocabulary is empty. Nothing to delete.\n" +
                                "See /help for instructions."
                        );
                return false;
            }
            else
            {
                InlineKeyboardMarkup markup = new InlineKeyboardMarkup(buttons);

                var dataRequesting = BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
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
            var confirmationMessageSending = BotClient.Bot.SendTextMessageAsync(
                chatId: update.CallbackQuery!.Message!.Chat.Id,
                text: "Are you sure? Type \"Yes, i am sure.\" if you are.\n" +
                "Otherwise command will be automatically canceled."
                );

            if (!Int64.TryParse(update.CallbackQuery!.Data, out englishWordID))
            {
                Console.WriteLine("EditWord Error: can`t find a chosen word");
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
                await BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
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
                    cmdText:
                   $"DELETE FROM user#{UserID}\n" +
                   $"WHERE EnglishWordID = {englishWordID};\n" +

                    "DECLARE @RemainingPopularity as INT\n" +

                    "UPDATE EnglishWords\n" +
                    "SET NumberOfUsersCurrentlyUsing -= 1,\n" +
                    "@RemainingPopularity = NumberOfUsersCurrentlyUsing - 1\n" +
                   $"WHERE WordID = {englishWordID};\n" +

                    "IF(@RemainingPopularity = 0)\n" +
                    "   DELETE FROM EnglishWords\n" +
                   $"       WHERE WordID = {englishWordID};\n"
                    ,
                    connection: connection
                    );

                await connectionOpening;

                var deletingWord = deleteWord.ExecuteNonQueryAsync();

                await BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text: "The word was successfully deleted.\n" +
                            "See /help for instructions."
                    );

                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await deletingWord;
            }

        }
    }
}
