using System;
using System.Threading.Tasks;
using DotnetGeminiSDK.Client.Interfaces;
using DotnetGeminiSDK.Config;
using Newtonsoft.Json;
using System.Text;
using EnVoQbot.AdditionalObjects;
using DotnetGeminiSDK.Client;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
namespace EnVoQbot.LLM
{
    internal class LanguageDetectionAgent
    {
        private static readonly GeminiClient gClient = new GeminiClient(new GoogleGeminiConfig() {
            ApiKey = ConnectionsData.ApiKey,
            TextBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash"
        });
        internal async static Task<string?> AnalyseAsync(string input)
        {
            //Analyze available languages
            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);

            //Create sysprompt
            var systemPrompt = @"You are an intelligent language detection agent.
                You will be given some language name as input and yout task is to find out what the language it is,
                find it`s name in the list of supported languages and return this exact name.
                - LIST OF SUPPORTED LANGUAGES: ";
            //i=1 because cultures[0] is 'invariant culture' = useless.
            for (int i = 1; i < cultures.Length; i++) systemPrompt += $"'{cultures[i].EnglishName}', ";
            systemPrompt += @".
                - if input is inconsistent, unrecognizable, or does not match any language in the list, return NULL.
                - you return language names EXACTLY (case-sensitive) AS DESCRIBED IN THE GIVEN LIST.
                - do not write anything else or add any whitespaces/brackets/quotation marks. Just language name from list or NULL.
                Examples:
                input: 'Українська'; output: 'Ukrainian'.
                input: 'ukrainian'; output: 'Ukrainian'.
                input: 'Español'; output: 'Spanish'.
                input: 'Deutsch'; output: 'German'.
                input: 'alsdjfhalsdjfh'; output: 'NULL'.
                input: 'ффбьмкм мфдулр є'; output: 'NULL'.";


            var response = await gClient.TextPrompt("SYSTEM PROMPT:\n " + systemPrompt + "\nUSER INPUT:\n" + input);
            
            if (response != null)
                return (response.Candidates[0].Content.Parts[0].Text).TrimEnd();
            else 
                return null;
        }
    }
}
