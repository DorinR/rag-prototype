using Microsoft.OpenApi.Models;
using rag_experiment.Services;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services.Events;
using rag_experiment.Services.Ingestion.VectorStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAG API", Version = "v1" });
});

// Add CORS service to allow requests from any origin
// TODO: Remove this before deploying to production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure RAG settings from appsettings.json
builder.Services.Configure<RagSettings>(
    builder.Configuration.GetSection("RagConfiguration"));

// Register Markdown table service with configurable file path
var markdownTablePath = builder.Configuration["MarkdownTablePath"] ?? "experiment_results.md";
builder.Services.AddSingleton(new MarkdownTableService(markdownTablePath));

// Register HttpClient services
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<QueryPreprocessor>();

// Register our services
builder.Services.AddScoped<IObsidianVaultReader, ObsidianVaultReader>();
builder.Services.AddScoped<IPdfDocumentReader, PdfDocumentReader>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<IEmbeddingGenerationService, OpenAiEmbeddingGenerationService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<IQueryPreprocessor, QueryPreprocessor>();
builder.Services.AddScoped<ILlmService, OpenAILlmService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IExperimentService, ExperimentService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();

// Register AppDbContext with SQLite connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Apply any pending migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG API v1"));

    // Redirect root to Swagger UI
    app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
}

// Enable CORS with the "AllowAll" policy
app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// During app startup
EventBus.Subscribe<DocumentUploadedEvent>(async evt =>
{
    using var scope = app.Services.CreateScope();
    var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
    await ingestionService.IngestDocumentAsync(evt.DocumentId);
});

app.Run();
