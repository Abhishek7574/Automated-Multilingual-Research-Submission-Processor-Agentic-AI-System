using Azure;
using Azure.AI.OpenAI;
using Backend.Models;
using BackEnd.Models;
using BackEnd.Storage;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Backend.Agents;

/// <summary>
/// Q&A Agent – stub implementation.
/// TODO: Use SK ChatCompletionAgent with RAG retrieval from Azure AI Search.
/// Support multilingual queries and maintain per-session chat history.
/// </summary>
public class QnAAgent : IQnAAgent
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly VectorStore _vectorStore;
    private readonly ILogger<QnAAgent> _logger;

    private const int TopK = 10;

    // Chat history per session
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistory = new();

    public QnAAgent(IConfiguration config, VectorStore vectorStore, ILogger<QnAAgent> logger)
    {
        _logger = logger;
        _vectorStore = vectorStore;

        var endpoint = new Uri(config["AzureOpenAI:Endpoint"]!);
        var key = config["AzureOpenAI:ApiKey"]!;
        _openAIClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(key));
    }

    public Task<StepResult<QnAReadyResult>> PrepareAsync(string documentId, string indexId, CancellationToken ct = default)
    {
        var result = new QnAReadyResult(true, indexId, $"/api/documents/{documentId}/ask");
        return Task.FromResult(new StepResult<QnAReadyResult>(true, result));
    }

    public async Task<QnAResponse> AskAsync(QnARequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[QnAAgent] Question received: {Question}", request.Question);

        // 1️⃣ Generate embedding for question
        var queryEmbedding = await CreateEmbeddingAsync(request.Question, ct);

        // 2️⃣ Retrieve top-k relevant chunks
        var relevantChunks = RetrieveRelevantChunks(request.DocumentId, queryEmbedding);

        var context = string.Join("\n\n", relevantChunks.Select(c => c.Text));

        // 3️⃣ Prepare chat history
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var history = _chatHistory.GetOrAdd(sessionId, _ => new List<ChatMessage>());

        // Build prompt with context + previous chat history
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("You are an expert academic research assistant.");
        promptBuilder.AppendLine("Answer questions ONLY using the provided context.");
        promptBuilder.AppendLine("If the answer is partially available, try to infer from the context.");
        promptBuilder.AppendLine("If completely missing, reply: 'Information not found in the document.'");
        promptBuilder.AppendLine("\nContext:\n");
        promptBuilder.AppendLine(context);
        promptBuilder.AppendLine("\nQuestion:\n");
        promptBuilder.AppendLine(request.Question);

        var chatClient = _openAIClient.GetChatClient("gpt-4.1-mini");

        // Include previous history messages
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You answer questions about research papers."),
        };
        messages.AddRange(history.Select(m => m));

        messages.Add(new UserChatMessage(promptBuilder.ToString()));

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);

        var answer = response.Value.Content[0].Text;

        // Save in session history
        history.Add(new UserChatMessage(request.Question));
        history.Add(new AssistantChatMessage(answer));

        sw.Stop();

        return new QnAResponse(
            Question: request.Question,
            Answer: answer,
            Sources: relevantChunks.Select(c => c.ChunkId).ToList(),
            Confidence: 0.90,
            SessionId: sessionId
        );
    }

    private async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct)
    {
        var embeddingClient = _openAIClient.GetEmbeddingClient("text-embedding-ada-002");
        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    private List<VectorChunk> RetrieveRelevantChunks(string documentId, float[] queryEmbedding)
    {
        var chunks = _vectorStore.GetChunks(documentId);
        if (!chunks.Any()) return new List<VectorChunk>();

        return chunks.Select(c => new
        {
            Chunk = c,
            Score = CosineSimilarity(queryEmbedding, c.Embedding)
        })
        .OrderByDescending(x => x.Score)
        .Take(TopK)
        .Select(x => x.Chunk)
        .ToList();
    }

    private static double CosineSimilarity(float[] v1, float[] v2)
    {
        double dot = 0, mag1 = 0, mag2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2) + 1e-10);
    }
}
