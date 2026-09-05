using Azure;
using Azure.AI.OpenAI;
using Backend.Models;
using BackEnd.Models;
using BackEnd.Storage;
using System.Diagnostics;

namespace Backend.Agents;

/// <summary>
/// RAG Agent – stub implementation.
/// TODO: Chunk document text, generate embeddings via Azure OpenAI,
/// and upsert into Azure AI Search vector store using SK Memory.
/// </summary>
public class RagAgent : IRagAgent
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly VectorStore _vectorStore;
    private readonly ILogger<RagAgent> _logger;
    private const int ChunkSize = 500;

    public RagAgent(IConfiguration config, VectorStore vectorStore, ILogger<RagAgent> logger)
    {
        _logger = logger;
        _vectorStore = vectorStore;

        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]!);
        var key = config["AzureOpenAI:ApiKey"]!;
        _openAIClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(key));
    }

    public async Task<StepResult<RagIndexResult>> IndexAsync(
        string documentId, string text, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[RagAgent] Indexing document {DocumentId}", documentId);

            var embeddingClient = _openAIClient.GetEmbeddingClient("text-embedding-ada-002");
            var chunks = ChunkText(text);

            int chunkIndex = 0;
            int estimatedTokens = 0;

            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();

                var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(chunk, cancellationToken: ct);
                var embedding = embeddingResponse.Value.ToFloats().ToArray();

                var vectorChunk = new VectorChunk
                {
                    DocumentId = documentId,
                    ChunkId = $"{documentId}-{chunkIndex}",
                    Text = chunk,
                    Embedding = embedding
                };

                _vectorStore.AddChunk(documentId, vectorChunk);

                estimatedTokens += chunk.Length / 4;
                chunkIndex++;
            }

            sw.Stop();

            var result = new RagIndexResult(
                IndexId: $"idx-{documentId}",
                ChunksIndexed: chunkIndex,
                TotalTokens: estimatedTokens,
                VectorStore: "InMemoryVectorStore"
            );

            _logger.LogInformation("[RagAgent] Completed indexing: {Chunks} chunks in {Elapsed}ms",
                chunkIndex, sw.ElapsedMilliseconds);

            return new StepResult<RagIndexResult>(true, result, ElapsedMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[RagAgent] Failed indexing document {DocumentId}", documentId);
            return new StepResult<RagIndexResult>(false, null, ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += ChunkSize)
        {
            int length = Math.Min(ChunkSize, text.Length - i);
            chunks.Add(text.Substring(i, length));
        }
        return chunks;
    }
}
