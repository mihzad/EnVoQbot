using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Quartz;
using EnVoQbot.AdditionalObjects;

namespace EnVoQbot
{
    internal static class BotClient
    {
        internal static readonly TelegramBotClient Bot = new TelegramBotClient(ConnectionsData.BotConnectionToken);
        internal static LinkedList<MultiUpdateCommand> CommandsCurrentlyExecuting = new LinkedList<MultiUpdateCommand>();
        internal static IScheduler? QuizzesScheduler = null;

        public static async Task Start()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var schedulerSettingUp = JobScheduler.SetUp();


            ReceiverOptions receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new UpdateType[]
                {
                    UpdateType.Message,
                    UpdateType.CallbackQuery,
                }
            };
            QuizzesScheduler = await schedulerSettingUp;
            await QuizzesScheduler.Start();

            Bot.StartReceiving(UpdateHandlerAsync, ErrorHandler, receiverOptions);

            Console.ReadLine();
        }
        private static Task ErrorHandler(ITelegramBotClient bot, Exception ex, CancellationToken arg3)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            throw new NotImplementedException();
        }

        private static async Task UpdateHandlerAsync(ITelegramBotClient bot, Update update, CancellationToken arg3)
        {
            var commandsProcessing = CommandsManager.StartProcessing(update);

            if (update.Message != null &&
                update.Message.Type == MessageType.Text)
            {
                var text = update.Message.Text;

                long? id = 
                    (update.Message.From == null ?
                    null : update.Message.From.Id);

                string? username = 
                    (update.Message.From == null ?
                    null : update.Message.From.Username);

                Console.WriteLine($"{username} | {id} | {text};");
            }

            await commandsProcessing;
            
        }

    }
}
