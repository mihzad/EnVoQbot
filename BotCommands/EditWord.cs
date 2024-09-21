using EnVoQbot.AdditionalObjects;
using EnVoQbot.MultiUpdateCommandsStagesEnums;
using Microsoft.Data.SqlClient;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EnVoQbot.BotCommands
{
    internal class EditWord : MultiUpdateCommand, IUsesInlineKeyboards
    {
        internal EditWord(long userID)
        {
            UserID = userID;
        }

        public bool HasInlineKeyboardActivated {
            get{ return hasInlineKeyboardActivated; }
            set{ hasInlineKeyboardActivated = value; }
        }
        private bool hasInlineKeyboardActivated = false;

        public Message? MessageWithInlineKeyboardToDelete { 
            get{ return messageWithInlineKeyboardToDelete; }
            set{ messageWithInlineKeyboardToDelete = value; }
        }
        private Message? messageWithInlineKeyboardToDelete = null;

        private EditWordStages currentStage = EditWordStages.ChooseWordToEdit;
        private string whatToEdit = string.Empty;
        private long englishWordID;

        internal override async Task ExecuteAsync(Update update)
        {
            switch (currentStage)
            {
                case EditWordStages.ChooseWordToEdit:
                    {
                        await ChooseWordToEditAsync(update);
                        break;
                    }

                case EditWordStages.ChooseWhatToEdit:
                    {
                        //deactivate previous stage keyboard
                        var deactivating = ((IUsesInlineKeyboards)this).DeactivateInlineKeyboard("Done");
                        await ChooseWhatToEditAsync(update);
                        await deactivating;
                        break;
                    }
                case EditWordStages.SendDataRequest:
                    {
                        //deactivate previous stage keyboard
                        var deactivating = ((IUsesInlineKeyboards)this).DeactivateInlineKeyboard("Done");
                        await SendDataRequestAsync(update);
                        await deactivating;
                        break;
                    }
                case EditWordStages.GetNewData:
                    {
                        await GetNewDataAsync(update);
                        break;
                    }
            }
        }

        internal async Task ChooseWordToEditAsync(Update update)
        {
            var buttons = await GetVocabularyAsInlineButtons(update);

            if (await SendKeyboardToChooseWordToEdit(buttons, update))
            {
                currentStage = EditWordStages.ChooseWhatToEdit;
                NeededUpdateType = UpdateType.CallbackQuery;
                BotClient.CommandsCurrentlyExecuting.AddLast(this);
            }
        }
        #region  auxiliary methods
        private async Task<bool> SendKeyboardToChooseWordToEdit
            (List<List<InlineKeyboardButton>> buttons, Update update)
        {
            if (buttons.Count == 0)
            {
                await BotClient.Bot.SendTextMessageAsync(
                        chatId: update!.Message!.Chat.Id,
                        text: "Your vocabulary is empty. Add new words.\n" +
                                "See /help for instructions."
                        );
                return false;
            }
            else
            {
                InlineKeyboardMarkup markup = new InlineKeyboardMarkup(buttons);

                var dataRequesting = BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
                    text: "Wanna edit a word? Alright.\n" +
                            "Choose one from your vocabulary:"
                            ,
                    replyMarkup: markup
                    );

                this.HasInlineKeyboardActivated = true;

                MessageWithInlineKeyboardToDelete = await dataRequesting;
                return true;
            }
        }
        
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
        #endregion

        private async Task ChooseWhatToEditAsync(Update update)
        {
            if (!Int64.TryParse(update.CallbackQuery!.Data, out englishWordID))
            {
                Console.WriteLine("EditWord Error: can`t find a chosen word");
                BotClient.CommandsCurrentlyExecuting.Remove(this);
                return;
            }

            var buttons = new List<InlineKeyboardButton>()
            {
                InlineKeyboardButton.WithCallbackData("spelling"),
                InlineKeyboardButton.WithCallbackData("translation")
            };
            InlineKeyboardMarkup markup = new InlineKeyboardMarkup(buttons);

            var request = BotClient.Bot.SendTextMessageAsync(
                chatId: update!.CallbackQuery!.Message!.Chat.Id,
                text: "OK. Now choose what you want to edit:",
                replyMarkup: markup
                );

            this.HasInlineKeyboardActivated = true;
            currentStage = EditWordStages.SendDataRequest;
            //NextNeededUpdateType still is UpdateType.CallbackQuery;
            MessageWithInlineKeyboardToDelete = await request;
        }

        private async Task SendDataRequestAsync(Update update)
        {
            var request = BotClient.Bot.SendTextMessageAsync(
                chatId: update!.CallbackQuery!.Message!.Chat.Id,
                text: $"OK. Now enter new {update.CallbackQuery!.Data!}:"
                );

            whatToEdit = update.CallbackQuery!.Data!;
            NeededUpdateType = UpdateType.Message;
            currentStage = EditWordStages.GetNewData;

            await request;
        }

        private async Task GetNewDataAsync(Update update)
        {
            string commandText = string.Empty;
            switch(whatToEdit)
            {
                case "spelling":
                    var newSpelling = update.Message!.Text;
                    commandText =
                        "UPDATE EnglishWords\n" +
                       $"SET Spelling = N'{newSpelling}'\n" +
                       $"WHERE WordID = {englishWordID};";
                    break;

                case "translation":
                    var newTranslation = update.Message!.Text;
                    commandText =
                       $"UPDATE user#{UserID}\n" +
                       $"SET Translation = N'{newTranslation}'\n" +
                       $"WHERE EnglishWordID = {englishWordID};";
                    break;
            }
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                
                var editWord = new SqlCommand(
                    cmdText: Encoding.UTF8.GetString(Encoding.Default.GetBytes(commandText)),
                    connection: connection
                    );

                await connectionOpening;

                var editingWord = editWord.ExecuteNonQueryAsync();

                await BotClient.Bot.SendTextMessageAsync(
                    chatId: update.Message!.Chat.Id,
                    text: "The word was successfully edited.\n" +
                            "See /help for instructions."
                    );

                BotClient.CommandsCurrentlyExecuting.Remove(this);
                await editingWord;
            }
        }
    }
}
