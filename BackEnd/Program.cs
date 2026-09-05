
using Azure;
using Azure.AI.TextAnalytics;
using Backend.Agents;
using Backend.Endpoints;
using Backend.Pipeline;
using Backend.Plugins;
using Backend.Storage;
using BackEnd.Storage;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


// ── SK Plugins ─────────────────────────────────────
builder.Services.AddSingleton<OcrPlugin>();

// ── Semantic Kernel ────────────────────────────────
builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var kBuilder = Kernel.CreateBuilder();

    kBuilder.AddAzureOpenAITextEmbeddingGeneration(
    deploymentName: config["AzureOpenAI:EmbeddingDeployment"],
    endpoint: config["AzureOpenAI:Endpoint"],
    apiKey: config["AzureOpenAI:ApiKey"]);

    kBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: config["AzureOpenAI:ChatDeployment"]
            ?? throw new InvalidOperationException("AzureOpenAI:ChatDeployment is not configured."),
        endpoint: config["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured."),
        apiKey: config["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not configured.")
    );



    // Register plugins
    kBuilder.Plugins.AddFromObject(sp.GetRequiredService<OcrPlugin>(), "OcrPlugin");

    return kBuilder.Build();
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var endpointStr = config["AzureAI:LanguageEndpoint"]
        ?? throw new InvalidOperationException("AzureAI:LanguageEndpoint is not configured.");

    var apiKey = config["AzureAI:LanguageKey"]
        ?? throw new InvalidOperationException("AzureAI:LanguageKey is not configured.");

    return new TextAnalyticsClient(new Uri(endpointStr), new AzureKeyCredential(apiKey));
});


// ── Document Store ─────────────────────────────────
builder.Services.AddSingleton<IDocumentStore, DocumentStore>();
builder.Services.AddSingleton<VectorStore>();


// ── Agents ─────────────────────────────────────────
builder.Services.AddScoped<IIngestionAgent, IngestionAgent>();
builder.Services.AddScoped<IPreProcessAgent, PreProcessAgent>();
builder.Services.AddScoped<ITranslationAgent, TranslationAgent>();
builder.Services.AddScoped<IExtractionAgent, ExtractionAgent>();
builder.Services.AddScoped<IValidationAgent, ValidationAgent>();
builder.Services.AddScoped<IContentSafetyAgent, ContentSafetyAgent>();
builder.Services.AddScoped<IPlagiarismDetectionAgent, PlagiarismDetectionAgent>();
builder.Services.AddScoped<IRagAgent, RagAgent>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISummaryAgent, SummaryAgent>();
builder.Services.AddScoped<IQnAAgent, QnAAgent>();
builder.Services.AddScoped<IHumanFeedbackAgent, HumanFeedbackAgent>();


builder.Services.AddScoped<DocumentPipelineOrchestrator>();

builder.Services.AddAntiforgery();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

app.UseHttpsRedirection();

// Health API
app.MapGet("/api/health", () =>
    Results.Ok(new
    {
        version = "1.0.0",
        status = "healthy",
        timestamp = DateTime.UtcNow
    }))
.WithName("GetHealth");


app.MapDocumentEndpoints();

app.Run();
