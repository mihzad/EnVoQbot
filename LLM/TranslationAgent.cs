using DotnetGeminiSDK.Config;
using EnVoQbot.AdditionalObjects;
using DotnetGeminiSDK.Client;
namespace EnVoQbot.LLM
{
    internal class TranslationAgent
    {
        private static readonly GeminiClient gClient = new GeminiClient(new GoogleGeminiConfig() {
            ApiKey = ConnectionsData.ApiKey,
            TextBaseUrl = ConnectionsData.LLMURL
        });
        internal async static Task<string?> TranslateAsync(string input, string Language)
        {
            var systemPrompt = @"You are a multilingual translation expert. 
                Your job is to translate words and phrases from English into {language} while preserving meaning.
                - If the input contains idioms, translate them into culturally equivalent phrases instead of a literal word-for-word translation.
                - If the input has multiple meanings, provide the most common translation.
                - If the input is unclear, inconsistent or not translatable - just return 'NULL'.
                - If the input is non-English - return NULL too.
                - Your input looks like this: Translate '{spelling}' from English to {language}.
                     Format your response as: 'spelling|transcription|translation'. Do not use the
                     ""|"" character anywhere else and do not add any whitespaces after translation.
                - All the transcriptions should be made with symbols for the phonemic transcription of English.
                - Example 1. input:  ""Translate 'food' from English to Ukrainian"". output: ""food|/fuːd/|їжа"". 
                - Example 2. input: ""Translate 'aldkfjhalskdjhf' from English to Russian"". output: ""NULL"".
                - You can translate input to more than one word if it`s better for translation accuracy.
                - Example 3. input: ""Translate 'chase rainbows' from English to Ukrainian"".
                    output: ""chase rainbows|/tʃeɪs ˈreɪn.bəʊz/|переслідувати нереалістичні цілі"".
                - Example 4. input: ""Translate 'скакать' from English to Ukrainian"".
                    output: ""NULL"".";

            var userMessage = $"Translate '{input}' from English to {Language}.";

            var response = await gClient.TextPrompt("SYSTEM PROMPT:\n " + systemPrompt + "\nUSER MESSAGE:\n" + userMessage);
            
            if (response != null)
                return (response.Candidates[0].Content.Parts[0].Text).TrimEnd();
            else 
                return null;
        }
    }
}
