using EnVoQbot.AdditionalObjects;
using Microsoft.Data.SqlClient;
using Quartz;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using EnVoQbot.LLM;
namespace EnVoQbot
{
    internal class GeneratePollsJob : IJob
    {
        private long UserID = 0;
        private long ChatID = 0;

        private int UserLanguageID = 0;
        private string UserLanguage = string.Empty;

        private int QuizzesCount = 0;
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap dataMap = context.MergedJobDataMap;

                ChatID = dataMap.GetLong("ChatID");
                UserID = dataMap.GetLong("UserID");
                QuizzesCount = dataMap.GetInt("QuizzesCount");

                WordData[]? userVocabulary = await GetUserVocabulary();

                if (userVocabulary == null)
                {
                    await BotClient.Bot.SendMessage(
                            chatId: ChatID,
                            text: "You have less than five words in your vocabulary.\n" +
                            " Add some more if you want me to create quizzes."
                            );
                    return;
                }

                var random = Random.Shared;
                while (QuizzesCount > 0)
                {
                    await GenerateQuiz(new Random(random.Next()), userVocabulary);
                    QuizzesCount--;// proceed to next quiz creation.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                throw new NotImplementedException();
            }
        }

        private async Task<WordData[]?> GetUserVocabulary()
        {
            WordData[] userVocabulary;
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var getVocabulary = new SqlCommand(
                    cmdText: $@"
                        SELECT COUNT (*)
                            FROM vocabulary#{UserID};

                        SELECT Languages.ID, Languages.Name
                            FROM Languages INNER JOIN UserData ON Languages.ID = UserData.LanguageID
                            WHERE UserTelegramID = {UserID};

                        SELECT EnglishWords.Spelling, EnglishWords.Transcription, Translations.Translation
                        FROM Translations
                            INNER JOIN vocabulary#{UserID} ON Translations.ID = vocabulary#{UserID}.TranslationID
                            INNER JOIN EnglishWords ON Translations.WordID = EnglishWords.ID;
                    ",
                connection: connection
                    );

                await connectionOpening;

                var dataReader = await getVocabulary.ExecuteReaderAsync();
                await dataReader.ReadAsync();
                var userVocabularySize = dataReader.GetInt32(0);

                if (userVocabularySize < 5) return null;


                await dataReader.NextResultAsync();
                await dataReader.ReadAsync();
                UserLanguageID = dataReader.GetInt32(0);
                UserLanguage = dataReader.GetString(1);


                await dataReader.NextResultAsync();

                userVocabulary = new WordData[userVocabularySize];
                for(int i = 0; i < userVocabularySize; i++)
                    userVocabulary[i] = new WordData();

                for (int i = 0; i < userVocabularySize; i++)
                {
                    await dataReader.ReadAsync();//data reader has rows because userVocabularySize >= 3.
                    userVocabulary[i].Spelling = dataReader.GetString(0);
                    userVocabulary[i].Transcription = dataReader.GetString(1);
                    userVocabulary[i].Translation = dataReader.GetString(2);
                }
            }
            return userVocabulary;
        }

        private async Task GenerateQuiz(Random randomizer, WordData[] userVocabulary)
        {
            //2 <= quizAnswersCount <= Min(10, userVocabularySize)
            var quizAnswersCount = randomizer.Next(2, Math.Min(10, userVocabulary.Length) + 1);

            int[] quizDataIndexes = new int[quizAnswersCount]; //auto-filled with 0s
            
            for (int i = 1; i < quizDataIndexes.Length; i++)
            {
                //choose the words for Gemini to generate poll from
                int newIndex = randomizer.Next(userVocabulary.Length);
                while (quizDataIndexes.Contains(newIndex)) // words won`t be repeated.
                    newIndex = randomizer.Next(userVocabulary.Length);
                quizDataIndexes[i] = newIndex;
            }

            WordData[] quizData = new WordData[quizAnswersCount];
            for (int i = 0; i < quizData.Length; i++)
                quizData[i] = userVocabulary[quizDataIndexes[i]];

            var quizDataStr = JsonConvert.SerializeObject(quizData);

            var response = await QuizGenerationAgent.GenerateAsync(quizDataStr, UserLanguage, "- Likes jokes and funny situations. ");
            
            if (response == null)
            {
                await BotClient.Bot.SendMessage(
                chatId: ChatID,
                text: "Could not generate the test: got an error speaking with Gemini."
                );
                return;
            }

            var possibleAnswerStrings = new string[quizAnswersCount];
            for (int i = 0; i < quizAnswersCount; i++)
                possibleAnswerStrings[i] = quizData[i].Spelling!;

            Shuffle(randomizer, possibleAnswerStrings);

            //сonvert strings to InputPollOption
            InputPollOption[] possibleAnswers = new InputPollOption[quizAnswersCount];
            for (int i = 0; i < quizAnswersCount; i++)
                possibleAnswers[i] = new InputPollOption(possibleAnswerStrings[i]);

            await BotClient.Bot.SendPoll(
                chatId: ChatID,
                question: response.Value.Question,
                options: possibleAnswers,
                isAnonymous: true,
                type: PollType.Quiz,
                correctOptionId: FindId(possibleAnswerStrings, response.Value.AnswerSpelling), // answers are translations, so we use translation to find.
                explanation: response.Value.Explanation,
                explanationParseMode: ParseMode.Html,
                protectContent: true
                );

        }

        private void Shuffle( Random randomizer, string[] array)
        {
            for(int i = array.Length - 1; i > 1; i--)
            {
                int j = randomizer.Next(i + 1); // 0 <= j <= i (that`s why i+1 there)
                string temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        private int FindId(string[] arrayWhereToSearch, string stringToFindId )
        {
            for(int i = 0; i < arrayWhereToSearch.Length; i++)
            {
                if (arrayWhereToSearch[i] == stringToFindId) return i;
            }
            return -1;
        }
    }
}
