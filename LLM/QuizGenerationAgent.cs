using DotnetGeminiSDK.Config;
using Newtonsoft.Json;
using EnVoQbot.AdditionalObjects;
using DotnetGeminiSDK.Client;

namespace EnVoQbot.LLM
{
    internal class QuizGenerationAgent
    {

        private static readonly GeminiClient gClient = new GeminiClient(new GoogleGeminiConfig() {
            ApiKey = ConnectionsData.ApiKey,
            TextBaseUrl = ConnectionsData.LLMURL
        });
        internal async static Task<QuizResponseData?> GenerateAsync(string jsonifiedQuizData, string language, string userPreferences)
        {
            var systemPrompt = @$"You are a quiz generation agent. 
                Your job is to create quiz questions, given the words to create the question from.
                The context you`re working in is generating interesting quizzes that will help users learn english words easily.

                - The words to create the test from will be given as Newtonsoft.Json converted string.
                    Each word comes with it`s spelling, transcription and translation into {language}. There`ll be few, 2-10 words.
                - You will also be provided with user preferences. That is, for example:
                    someone wants fun, jokes or anecdotes => better to write questions in joke-based style;
                    someone wants some technical terms, maybe provides specific field to focus into => write questions in that style;
                    maybe someone prefers both jokes & technical field words => combine, why not? Maybe you know some field-specific technical jokes :);
                    someone may write some inconsistent preferences or simply 'None.' => just ignore them and do as you feel it;
                - preferences may be provided, for example, as set of rules separated by '-' (just as i`m providing you now).
                        preferences list example: ""
                                - likes ... (anectodes, for example);
                                - doesn`t like ... (something user hates);
                                - is proficient at ... (for example, user is engineer);
                                - wants to learn ... (some specific field - for example, wants to become a programmer and so on.);
                                ... (some other preferences, there can be alternative descriptions present)
                        ""
                    
                - Your input looks like this: WordsList '{{jsonified_words}}'; User preferences: '{{preferences}}'.

                - You:
                    1) select one random word from the list - it will be the correct answer;

                    2) generate some interesting quiz question (in English) with that word:
                       - questions have to allow the user to find out the correct answer - but without revealing it directly.
                         (For example, you can trivially use '_____' instead of the correct answer word);
                       
                       - don`t forget about user preferences, yet don`t bound yourself up with them.
                            Sometimes (lets assume this to have... 5% probability) it`s better to take
                            the user out of their preference zone and delight them with completely different style;
                       - question length MUST be less than 300 characters.
                       - Be creative, try to not repeat your previous questions. If you receive same words often, orient on at least 5 unique questions for each;

                    3) write explanation IN {language.ToUpper()}, that clearly shows context and why correct answer is correct.
                       - Better start with answer`s data: '{{answer`s spelling}} [{{answer`s transcription}}] {{answer`s translation}} - ...'
                            for user to understand the answer immediately.
                       - You can (but not bound to), for example, show answer validity by comparison with other variants.
                       - explanation length MUST be less than 200 characters.

                - Format your response as Newtonsoft.Json - ified string for this structure:
                    struct QuizResponseData
                            {{
                                string AnswerSpelling; // here you specify answer`s spelling;
                                string AnswerTranscription; // here you specify answer`s transcription;
                                string AnswerTranslation; // here you specify answer`s translation;

                                string Question; //here you specify generated test question;
                                string Explanation; //here you specify generated explanation.
                            }}
                - Do not add any whitespaces or notations, JUST STRING STARTING WITH {{ AND ENDING WITH }} with structure shown above and data you generated.

            ";

            var userMessage = $"WordsList '{jsonifiedQuizData}'; User preferences: '{userPreferences}'.";

            var response = await gClient.TextPrompt("SYSTEM PROMPT:\n " + systemPrompt +
                                                    "\nINPUT:\n" + userMessage);

            if (response != null)
            {
                var resp_str = (response.Candidates[0].Content.Parts[0].Text).TrimEnd();
                resp_str = resp_str.TrimStart("```json\n".ToCharArray());
                resp_str = resp_str.TrimEnd("```".ToCharArray());

                QuizResponseData respstruct = JsonConvert.DeserializeObject<QuizResponseData>(resp_str);
                return respstruct;
            }

            else 
                return null;
        }
    }
}
