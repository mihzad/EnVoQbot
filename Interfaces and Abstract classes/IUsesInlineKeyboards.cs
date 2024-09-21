
using Telegram.Bot.Types;
using Telegram.Bot;

namespace EnVoQbot
{
    internal interface IUsesInlineKeyboards
    {
        internal bool HasInlineKeyboardActivated { get; set; }

        internal Message? MessageWithInlineKeyboardToDelete { get; set; }

        internal async Task DeactivateInlineKeyboard(string actionInfo)
        {
            if (MessageWithInlineKeyboardToDelete != null && HasInlineKeyboardActivated == true)
            {
                var deleting = BotClient.Bot.EditMessageTextAsync(
                chatId: MessageWithInlineKeyboardToDelete!.Chat!.Id,
                messageId: MessageWithInlineKeyboardToDelete.MessageId,
                text: MessageWithInlineKeyboardToDelete.Text! + $" ({actionInfo})"
                );

                MessageWithInlineKeyboardToDelete = null;
                HasInlineKeyboardActivated = false;

                await deleting;
            }
        }
    }
}
