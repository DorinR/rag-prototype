using Microsoft.OpenApi.Models;
using rag_experiment.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAG API", Version = "v1" });
});

// Register our services
builder.Services.AddScoped<IObsidianVaultReader, ObsidianVaultReader>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

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