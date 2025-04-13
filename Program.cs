using Microsoft.OpenApi.Models;
using rag_experiment.Services;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAG API", Version = "v1" });
});

// Configure settings
builder.Services.Configure<RagSettings>(
    builder.Configuration.GetSection("RagConfiguration"));

// Register Markdown table service with configurable file path
var markdownTablePath = builder.Configuration["MarkdownTablePath"] ?? "experiment_results.md";
builder.Services.AddSingleton(new MarkdownTableService(markdownTablePath));

// Add HttpClient for OpenAI embedding service
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<QueryPreprocessor>();

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register our services
builder.Services.AddScoped<IObsidianVaultReader, ObsidianVaultReader>();
builder.Services.AddScoped<ICisiPapersReader, CisiPapersReader>();
builder.Services.AddScoped<IPdfDocumentReader, PdfDocumentReader>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<IQueryPreprocessor, QueryPreprocessor>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IExperimentService, ExperimentService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run(); 