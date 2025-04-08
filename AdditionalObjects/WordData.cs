using Newtonsoft.Json;


namespace EnVoQbot.AdditionalObjects
{
    internal class WordData
    {
        public WordData() { }

        [JsonProperty]
        internal string? Spelling { get; set; }


        [JsonProperty]
        internal string? Transcription { get; set; }


        [JsonProperty]
        internal string? Translation { get; set; }
    }
}
