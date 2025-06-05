using Microsoft.OpenApi.Models;
using rag_experiment.Services;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services.Events;
using rag_experiment.Services.Ingestion.VectorStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using rag_experiment.Services.Auth;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers(options =>
{
    // Add more detailed model validation
    options.ModelValidatorProviders.Clear();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Customize validation error response for better debugging
    options.InvalidModelStateResponseFactory = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Model validation failed for {ActionName}", context.ActionDescriptor.DisplayName);

        foreach (var modelError in context.ModelState)
        {
            foreach (var error in modelError.Value.Errors)
            {
                logger.LogWarning("Validation Error - Field: {Field}, Message: {Message}, Exception: {Exception}",
                    modelError.Key, error.ErrorMessage, error.Exception?.Message);
            }
        }

        return new BadRequestObjectResult(context.ModelState);
    };
})
.AddJsonOptions(options =>
{
    // Configure JSON serialization for better enum handling
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.AllowTrailingCommas = true;

    // Add enum converter for better error messages
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RAG API", Version = "v1" });

    // Configure JWT authentication for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    // Generate a random secret if none is configured
    var key = new byte[32];
    using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
    {
        rng.GetBytes(key);
    }
    jwtSecret = Convert.ToBase64String(key);
    builder.Configuration["Jwt:Secret"] = jwtSecret;
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };

    // Configure to read JWT token from cookie
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["token"];
            return Task.CompletedTask;
        }
    };
});

// Add CORS service to allow requests from any origin
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (allowedOrigins == null || !allowedOrigins.Any())
{
    throw new InvalidOperationException("No allowed origins configured for CORS");
}

// Debug logging for production troubleshooting
Console.WriteLine($"[CORS DEBUG] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[CORS DEBUG] Allowed Origins: [{string.Join(", ", allowedOrigins)}]");
Console.WriteLine($"[CORS DEBUG] Total Origins Count: {allowedOrigins.Length}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
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

// Add HttpContextAccessor for user context
builder.Services.AddHttpContextAccessor();

// Register our services
builder.Services.AddScoped<IUserContext, UserContext>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IObsidianVaultReader, ObsidianVaultReader>();
builder.Services.AddScoped<IPdfDocumentReader, PdfDocumentReader>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IEmbeddingGenerationService, OpenAiEmbeddingGenerationService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<EmbeddingStorage>();
builder.Services.AddScoped<IQueryPreprocessor, QueryPreprocessor>();
builder.Services.AddScoped<ILlmService, OpenAILlmService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IExperimentService, ExperimentService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();

// Register AppDbContext with SQLite connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add simple health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Database initialization - only run if Railway didn't handle migrations
var skipMigrations = builder.Configuration.GetValue<bool>("RAILWAY_SKIP_MIGRATIONS", false);
if (!skipMigrations)
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Checking for pending database migrations...");
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations: {string.Join(", ", pendingMigrations)}");
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrations completed successfully");
            }
            else
            {
                logger.LogInformation("Database is up to date");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            throw;
        }
    }
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

// Log CORS middleware application
Console.WriteLine("[CORS DEBUG] CORS middleware applied with 'AllowAll' policy");

// Add authentication middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// During app startup
EventBus.Subscribe<DocumentUploadedEvent>(async evt =>
{
    using var scope = app.Services.CreateScope();
    var ingestionService = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
    await ingestionService.IngestDocumentAsync(evt.DocumentId_, evt.UserId_, evt.ConversationId_);
});

EventBus.Subscribe<DocumentDeletedEvent>(evt =>
{
    using var scope = app.Services.CreateScope();
    var embeddingStorageService = scope.ServiceProvider.GetRequiredService<EmbeddingStorage>();
    embeddingStorageService.DeleteEmbeddingsByDocumentId(evt.DocumentId_.ToString());
});

app.Run();
