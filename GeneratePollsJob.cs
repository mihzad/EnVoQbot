using EnVoQbot.AdditionalObjects;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace EnVoQbot
{
    internal class GeneratePollsJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap dataMap = context.MergedJobDataMap;

                long chatID = dataMap.GetLong("chatID");
                long userID = dataMap.GetLong("userID");
                int quizzesCount = dataMap.GetInt("quizzesCount");

                WordData[]? userVocabulary = await GetUserVocabulary(userID);

                if (userVocabulary == null)
                {
                    await BotClient.Bot.SendTextMessageAsync(
                            chatId: chatID,
                            text: "You have less than three words in your vocabulary.\n" +
                            " Add some more if you want me to create quizzes."
                            );
                    return;
                }

                var random = Random.Shared;
                while (quizzesCount > 0)
                {
                    await GenerateQuiz(new Random(random.Next()), userVocabulary, chatID);
                    quizzesCount--;// proceed to next quiz creation.
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                throw new NotImplementedException();
            }
        }

        private async Task<WordData[]?> GetUserVocabulary(long userID)
        {
            WordData[] userVocabulary;
            using (SqlConnection connection = new SqlConnection(ConnectionsData.DBconnectionString))
            {
                var connectionOpening = connection.OpenAsync();

                var getVocabulary = new SqlCommand(
                    cmdText:
                    "SELECT COUNT (*)\n" +
                    "FROM EnglishWords\n" +
                   $"INNER JOIN user#{userID} On EnglishWords.WordID = user#{userID}.EnglishWordID;\n" +

                   $"SELECT EnglishWords.WordID, EnglishWords.Spelling, EnglishWords.Transcription, user#{userID}.Translation\n" +
                    "FROM EnglishWords\n" +
                   $"INNER JOIN user#{userID} On EnglishWords.WordID = user#{userID}.EnglishWordID;\n"
                ,
                connection: connection
                    );

                await connectionOpening;

                var dataReader = await getVocabulary.ExecuteReaderAsync();
                dataReader.Read();
                var userVocabularySize = dataReader.GetInt32(0);

                if (userVocabularySize < 3) return null;
                dataReader.NextResult();

                userVocabulary = new WordData[userVocabularySize];
                for(int i = 0; i < userVocabularySize; i++)
                    userVocabulary[i] = new WordData();

                for (int i = 0; i < userVocabularySize; i++)
                {
                    dataReader.Read();//data reader has rows because userVocabularySize >= 3.
                    userVocabulary[i].Spelling = dataReader.GetString(1);
                    userVocabulary[i].Transcription = dataReader.GetString(2);
                    userVocabulary[i].Translation = dataReader.GetString(3);
                }
            }
            return userVocabulary;
        }

        private async Task GenerateQuiz(Random randomizer, WordData[] userVocabulary, long chatID)
        {
            var quizAnswersCount = randomizer.Next(2, Math.Min(10, userVocabulary.Length) + 1); //2 <= quizAnswersCount <= Min(10, userVocabularySize)

            int[] possibleAnswersIndexes = new int[quizAnswersCount];
            //possibleAnswersIndexes stores index number in userVocabulary for each word used as answer in the quiz.
            string[] possibleAnswers = new string[quizAnswersCount]; // stores answers (word translations)

            //choose correct answer
            var correctAnswerIndex = randomizer.Next(userVocabulary.Length);// 0 <= correctAnswerIndex < userVocabularySize
            WordData correctAnswerData = userVocabulary[correctAnswerIndex];

            //We fill our array in order to use contains(). By the time, we guarantee quiz WILL HAVE correct answer.
            for (int i = 0; i < possibleAnswersIndexes.Length; i++)
                possibleAnswersIndexes[i] = correctAnswerIndex;
            
            for (int i = 1; i < possibleAnswersIndexes.Length; i++)
            {
                //choose the rest of answers
                int newIndex = randomizer.Next(userVocabulary.Length);
                while (possibleAnswersIndexes.Contains(newIndex)) // guarantee quiz will have ONLY ONE correct answer,
                    newIndex = randomizer.Next(userVocabulary.Length);// because answers won`t be repeated.
                possibleAnswersIndexes[i] = newIndex;
            }

            for (int i = 0; i < quizAnswersCount; i++)
                possibleAnswers[i] = userVocabulary[possibleAnswersIndexes[i]].Translation!;

            Shuffle(randomizer, possibleAnswers);

            await BotClient.Bot.SendPollAsync(
                chatId: chatID,
                question: $"What is correct translation of {correctAnswerData.Spelling} [{correctAnswerData.Transcription}]?",
                options: possibleAnswers,
                isAnonymous: true,
                type: PollType.Quiz,
                correctOptionId: FindId(possibleAnswers, correctAnswerData.Translation!), // answers are translations, so we use translation to find.
                explanation: "there i will explain smth",
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
