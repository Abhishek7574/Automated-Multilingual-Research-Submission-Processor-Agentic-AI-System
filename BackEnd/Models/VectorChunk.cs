namespace BackEnd.Models
{
    public class VectorChunk
    {
        public string DocumentId { get; set; } = "";
        public string ChunkId { get; set; } = "";
        public string Text { get; set; } = "";
        public float[] Embedding { get; set; } = Array.Empty<float>();

    }
}
