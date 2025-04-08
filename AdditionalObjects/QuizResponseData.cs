using Newtonsoft.Json;


namespace EnVoQbot.AdditionalObjects
{
    internal struct QuizResponseData //used for Quiz Generation Agent
        {
            [JsonProperty]
            internal string AnswerSpelling;
            [JsonProperty]
            internal string AnswerTranscription;
            [JsonProperty]
            internal string AnswerTranslation;
            [JsonProperty]
            internal string Question;
            [JsonProperty]
            internal string Explanation;

        }
}
