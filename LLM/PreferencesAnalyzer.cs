using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EnVoQbot.LLM
{
    
    internal class PreferencesAnalyzer
    {
        private class OllamaResponse
        {
            [JsonInclude]
            internal string model { get; set; } = string.Empty;
            [JsonInclude]
            internal string response { get; set; } = string.Empty;
        }

        private static readonly HttpClient client = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan };
        private static string ollama_endpoint = "http://localhost:11434/api/generate";

        private static Regex regex = new Regex("(?s)<think>.*?<\\/think>", RegexOptions.Compiled);
        internal async static Task<string?> Run(string feedback, string previousPreferences)
        {
            try
            {
                var requestBody = new
                {
                    model = "deepseek-r1:8b",
                    prompt = @$"
                            SYSTEM PROMPT:
                            You are a preferences analysis agent.
                            Your task is create set of user`s preferences given user`s feedback.
                                Or, if there`s a set of user`s previous preferences given, update it according to user`s feedback.

                            The set of preferences you work with (if not NULL) looks like:
                            ""
                                - likes something (anectodes, for example);
                                - doesn`t like (hates, so on. any synonym) something else;
                                - is proficient at something (for example, user is engineer);
                                - wants to learn some specific field (for example, wants to become a programmer and so on.);
                                ...
                            ""
                            
                            Your input looks like:
                                ""Previous preferences set: {{previous preferences}} ; New feedback: {{feedback}}; ""
                                if there`s no previous preferences - you will get NULL in {{previous preferences}}.

                            Rules for preferences set generation:
                             - if some user wishes contradict each other - ignore both.
                             - if user feedback parts are not understandable (nonsense written) - ignore them.
                             - just analyze user preferences and describe them one by one, and add them to the set then.

                            Rules for preferences set updating:
                             - if user wishes described in the feedback contradict with wishes described in preferences set ->
                                    update preferences set replacing old user wishes with new ones provided in the feedback.
                             - if some user wishes inside feedback itself contradict each other - ignore both.
                             - if user feedback parts are not understandable (nonsense written) - ignore them.
                             - just analyze user new preferences and update the set.

                            YOUR OUTPUT SHOULD BE ONLY SET OF NEW PREFERENCES ITSELF:
                            ""
                                - likes ... (anectodes, for example);
                                - doesn`t like ... (something user hates);
                                - is proficient at ... (for example, user is engineer);
                                - wants to learn ... (some specific field - for example, wants to become a programmer and so on.);
                                ... (some other preferences, you`re allowed to use alternative descriptions if it`s better for precision)
                            ""
                            and so on. The point is, you must gather user preferences given user`s feedback.
                            JUST OUTPUT THE SET ITSELF (-.....) WITHOUT ANY ADDITIONAL SYMBOLS OR FORMATTING.
                            INPUT:
                                ""Previous preferences set: {previousPreferences} ; New feedback: {feedback}; ""
                            ",
                    stream = false
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                HttpResponseMessage html_resp = await client.PostAsync(ollama_endpoint, content);

                var ollama_resp = JsonSerializer.Deserialize<OllamaResponse>(await html_resp.Content.ReadAsStringAsync());

                var parsed_response = regex.Replace(ollama_resp!.response, "");
                Console.WriteLine("Response:");
                Console.WriteLine(parsed_response);
                Console.WriteLine();
                return parsed_response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                return null;
            }
            

        }
    }
}
