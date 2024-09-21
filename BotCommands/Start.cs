using EnVoQbot.AdditionalObjects;
using Microsoft.Data.SqlClient;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EnVoQbot.BotCommands
{
    internal class Start : SingleUpdateCommand
    {
        internal Start() { }

        internal override async Task ExecuteAsync(Update update)
        {
            var messageSending = BotClient.Bot.SendTextMessageAsync(
                    chatId: update!.Message!.Chat.Id,
                    text: $"Hello, {update!.Message!.From!.FirstName}!\n" +
                    " I can generate quizzes using words you specified.\n" +
                    " Whenever you want and almost as much as you want.\n" +
                    "Type /help for further information about commands."
                    );
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();
                var SetUserDataInDBcommand = new SqlCommand(
                    cmdText:
                    "IF (NOT EXISTS (SELECT * " +
                    "      FROM INFORMATION_SCHEMA.TABLES " +
                    $"      WHERE  TABLE_NAME = 'user#{update.Message.From.Id}'))\n" +
                    "  BEGIN\n" +
                    $"    CREATE TABLE user#{update.Message.From.Id} (" +
                    "      EnglishWordID BIGINT, " +
                    "      Translation NVARCHAR(MAX) NOT NULL, " +
                    "      PRIMARY KEY (EnglishWordID), " +
                    "      FOREIGN KEY (EnglishWordID) REFERENCES EnglishWords(WordID) ); " +
                    "  END;\n" +
                    "IF(NOT EXISTS (SELECT * " +
                    "       FROM UserData " +
                    $"       WHERE UserTelegramID = '{update!.Message!.From!.Id}'))\n" +
                    "   BEGIN\n" +
                    "       INSERT INTO UserData (UserTelegramID, Username) " +
                    $"      VALUES( '{update!.Message!.From!.Id}', '{update!.Message!.From!.Username}')\n" +
                    $"  END\n",

                    connection: connection
                    );
                await connectionOpening;
                await SetUserDataInDBcommand.ExecuteNonQueryAsync();

            }
            await messageSending;
        }
    }
}
