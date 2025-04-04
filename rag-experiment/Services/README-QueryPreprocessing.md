# Query Preprocessing Module

This module provides functionality to preprocess user queries before they are embedded and used for semantic search in the RAG system.

## Overview

The query preprocessing module transforms user queries to improve retrieval performance. It implements several techniques:

1. **OpenAI-Based Processing**: Leverages the OpenAI API to intelligently extract key concepts from queries
2. **Query Cleaning**: Removes unnecessary punctuation and normalizes whitespace (available as fallback)
3. **Pattern Matching**: Identifies specific query patterns and applies transformations (available as fallback)
4. **Query Expansion**: Adds related terms to improve recall (available as fallback)

## Architecture

The module consists of:

- `IQueryPreprocessor` interface that defines the contract for query preprocessing
- `QueryPreprocessor` implementation that contains the preprocessing logic using OpenAI API

## Current Preprocessing Techniques

### 1. OpenAI-Based Processing (Primary Method)

The QueryPreprocessor now uses OpenAI's chat completions API to process queries. Key characteristics:

- Sends the user query to OpenAI with a system prompt that instructs it to extract key concepts
- Removes question words, unnecessary phrasing, and focuses on core ideas or entities
- Returns a concise phrase or set of keywords for semantic search
- Uses a low temperature (0.1) for consistent, deterministic results
- Limits token usage to keep responses concise

### 2. Manual Processing (Fallback Methods)

The following techniques are still available as fallbacks through the `ProcessQueryManuallyAsync` method:

#### Query Cleaning

- Removes redundant whitespace
- Normalizes punctuation
- Trims the query

#### Pattern Matching

- Detects command-style queries (e.g., "find X", "search for Y")
- Transforms questions to declarative statements
- Replaces synonyms with standardized terms

#### Query Expansion

- Adds related technical terms
- Expands abbreviations or domain-specific language

## Configuration

The OpenAI integration requires configuration settings in your `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "ChatModel": "gpt-3.5-turbo",
    "EnableRateLimiting": true
  }
}
```

## How to Extend

### Modifying the OpenAI System Prompt

Edit the system prompt in the `ProcessQueryWithOpenAIAsync` method to change how OpenAI processes queries:

```csharp
new Message
{
    Role = "system",
    Content = "Your custom system prompt here"
}
```

### Adding Custom Preprocessing Steps

You can add additional preprocessing before or after the OpenAI call in the `ProcessQueryAsync` method:

```csharp
public async Task<string> ProcessQueryAsync(string query)
{
    if (string.IsNullOrWhiteSpace(query))
        return query;
        
    // Add preprocessing before OpenAI (if needed)
    query = SomePreprocessingStep(query);
    
    // Use OpenAI to preprocess the query
    var processedQuery = await ProcessQueryWithOpenAIAsync(query);
    
    // Add post-processing after OpenAI (if needed)
    processedQuery = SomePostProcessingStep(processedQuery);
    
    return processedQuery;
}
```

### Adding New Synonym Mappings (for Fallback Mode)

Add entries to the `_synonyms` dictionary in `QueryPreprocessor.cs`:

```csharp
private readonly Dictionary<string, string> _synonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "how to", "how do I" },
    { "what is", "explain" },
    { "definition of", "explain" },
    // Add your new synonyms here
    { "your synonym", "replacement" }
};
```

## Usage

The query preprocessor is automatically applied to all queries in the `RagController`. The original and processed queries are both returned in the API response for transparency.

## Examples

| Original Query | Processed Query |
|----------------|-----------------|
| "How do I connect to a database in C#?" | "connect database C#" |
| "What are the best practices for error handling in ASP.NET?" | "best practices error handling ASP.NET" |
| "Explain how dependency injection works in .NET Core" | "dependency injection .NET Core" |
| "Can you show me how to implement authentication in an API?" | "implement authentication API" |

## Fallback Mechanism

If the OpenAI API call fails, you can implement a fallback to the manual processing methods:

```csharp
public async Task<string> ProcessQueryWithFallbackAsync(string query)
{
    try
    {
        return await ProcessQueryWithOpenAIAsync(query);
    }
    catch (Exception ex)
    {
        // Log the error
        Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
        
        // Fall back to manual processing
        return await ProcessQueryManuallyAsync(query);
    }
} 