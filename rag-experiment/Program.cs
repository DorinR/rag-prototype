using Microsoft.OpenApi.Models;
using rag_experiment.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAG API", Version = "v1" });
});

// Register HttpClient services
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<QueryPreprocessor>();

// Register our services
builder.Services.AddScoped<IObsidianVaultReader, ObsidianVaultReader>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<IEmbeddingService, OpenAIEmbeddingService>();
builder.Services.AddScoped<EmbeddingService>();
// Register the query preprocessor service
builder.Services.AddScoped<IQueryPreprocessor, QueryPreprocessor>();

// Register AppDbContext with SQLite connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

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