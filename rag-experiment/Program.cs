using Microsoft.OpenApi.Models;
using rag_experiment.Services;
using Microsoft.EntityFrameworkCore;
using rag_experiment.Models;
using rag_experiment.Services.Events;
using rag_experiment.Services.Ingestion.VectorStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using rag_experiment.Services.Auth;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Hangfire;
using Hangfire.PostgreSql;
using rag_experiment.Services.BackgroundJobs;
using Microsoft.Extensions.Options;
using rag_experiment.Repositories;
using rag_experiment.Repositories.Documents;
using rag_experiment.Services.Ingestion.TextExtraction;

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

// Configure routing to use lowercase URLs
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
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

    // Configure to read JWT token from Authorization header only
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Read token from Authorization header (standard JWT approach)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                context.Token = authHeader.Substring("Bearer ".Length);
            }

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
              .AllowAnyHeader();
        // Removed .AllowCredentials() since we're using Authorization headers instead of cookies
    });
});

// Configure RAG settings from appsettings.json
builder.Services.Configure<RagSettings>(
    builder.Configuration.GetSection("RagConfiguration"));

// Configure OpenAI settings
builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection(OpenAISettings.SectionName));

// Configure OpenAI HttpClient
builder.Services.AddHttpClient("OpenAI", (serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<OpenAISettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");
});

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
builder.Services.AddScoped<ITextExtractor, PdfDocumentTextExtractor>();
builder.Services.AddScoped<ITextProcessor, TextProcessor>();
builder.Services.AddScoped<ITextChunker, TextChunker>();
builder.Services.AddScoped<IEmbeddingGenerationService, OpenAiEmbeddingGenerationService>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
builder.Services.AddScoped<EmbeddingRepository>();
builder.Services.AddScoped<IQueryPreprocessor, QueryPreprocessor>();
builder.Services.AddScoped<ILlmService, OpenAILlmService>();
builder.Services.AddSingleton<IDocumentProcessingStateRepository, InMemoryDocumentProcessingStateRepository>();
builder.Services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();



// Register AppDbContext with PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Debug logging for connection string
Console.WriteLine($"[DB DEBUG] Environment: {builder.Environment.EnvironmentName}");
if (!string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine($"[DB DEBUG] Original Connection String: {connectionString}");

    // Convert PostgreSQL URI format to key-value format if needed
    if (connectionString.StartsWith("postgresql://") || connectionString.StartsWith("postgres://"))
    {
        try
        {
            var uri = new Uri(connectionString);
            var host = uri.Host;
            var port = uri.Port;
            var database = uri.AbsolutePath.TrimStart('/');
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
            Console.WriteLine($"[DB DEBUG] Converted Connection String: {connectionString}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB DEBUG] Failed to convert URI connection string: {ex.Message}");
        }
    }
}
else
{
    Console.WriteLine("[DB DEBUG] Warning: No connection string configured!");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);

    // Configure EF Core logging based on configuration
    var loggingEnabled = builder.Configuration.GetValue<bool>("Logging:EntityFramework:Enabled", false);
    if (loggingEnabled)
    {
        options.LogTo(Console.WriteLine)
               .EnableSensitiveDataLogging()
               .EnableDetailedErrors();
    }
});

// Register Hangfire services
builder.Services.AddScoped<DocumentProcessingJobService>();

// Configure Hangfire with PostgreSQL storage
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

// Add Hangfire server
builder.Services.AddHangfireServer();


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

// Add Hangfire Dashboard (protected by authentication)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Note: Document processing is now handled by Hangfire background jobs
// Document deletion still uses EventBus for immediate cleanup
EventBus.Subscribe<DocumentDeletedEvent>(evt =>
{
    using var scope = app.Services.CreateScope();
    var embeddingStorageService = scope.ServiceProvider.GetRequiredService<EmbeddingRepository>();
    embeddingStorageService.DeleteEmbeddingsByDocumentId(evt.DocumentId_.ToString());
});

app.Run();
