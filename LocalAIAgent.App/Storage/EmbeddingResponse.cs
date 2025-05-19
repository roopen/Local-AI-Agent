namespace LocalAIAgent.App.Storage
{
    internal class EmbeddingResponse
    {
        public string Object { get; set; }
        public EmbeddingData[] Data { get; set; }
        public string Model { get; set; }
        public Usage Usage { get; set; }
    }

    internal class Usage
    {
        public int Prompt_tokens { get; set; }
        public int Total_tokens { get; set; }
    }

    internal class EmbeddingData
    {
        public string Object { get; set; }
        public float[] Embedding { get; set; }
        public int Index { get; set; }
    }
}