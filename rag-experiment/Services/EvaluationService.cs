using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using rag_experiment.Models;

namespace rag_experiment.Services
{
    public class EvaluationService : IEvaluationService
    {
        private readonly string _qryFilePath = Path.Combine("Test Data", "cisi_evaluation", "CISI.QRY");
        private readonly string _relFilePath = Path.Combine("Test Data", "cisi_evaluation", "CISI.REL");
        private readonly IEmbeddingService _embeddingService;
        private readonly IQueryPreprocessor _queryPreprocessor;
        private readonly EmbeddingService _dbEmbeddingService;

        public EvaluationService(
            IEmbeddingService embeddingService,
            IQueryPreprocessor queryPreprocessor,
            EmbeddingService dbEmbeddingService)
        {
            _embeddingService = embeddingService;
            _queryPreprocessor = queryPreprocessor;
            _dbEmbeddingService = dbEmbeddingService;
        }

        public async Task<Dictionary<int, string>> ReadQueriesAsync()
        {
            var queries = new Dictionary<int, string>();
            
            if (!File.Exists(_qryFilePath))
            {
                throw new FileNotFoundException($"CISI.QRY file not found at: {_qryFilePath}");
            }
            
            string content = await File.ReadAllTextAsync(_qryFilePath);
            Console.WriteLine($"Read {content.Length} characters from query file");
            
            // CISI.QRY format is: .I [id] followed by .W and then the query text
            var queryBlocks = Regex.Split(content, @"\.I\s+")
                .Where(block => !string.IsNullOrWhiteSpace(block))
                .ToList();
            
            Console.WriteLine($"Found {queryBlocks.Count} query blocks in the file");
            
            foreach (var block in queryBlocks)
            {
                try
                {
                    var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (lines.Length == 0)
                    {
                        Console.WriteLine("Skipping empty block");
                        continue;
                    }
                    
                    if (!int.TryParse(lines[0].Trim(), out int queryId))
                    {
                        Console.WriteLine($"Skipping block with invalid query ID: '{lines[0]}'");
                        continue;
                    }
                    
                    // Find the start of the query text after .W
                    int queryTextIndex = Array.FindIndex(lines, line => line.Trim() == ".W") + 1;
                    
                    if (queryTextIndex <= 0 || queryTextIndex >= lines.Length)
                    {
                        Console.WriteLine($"No query text found for query ID {queryId}, skipping");
                        continue;
                    }
                    
                    var queryTextBuilder = new StringBuilder();
                    
                    for (int i = queryTextIndex; i < lines.Length; i++)
                    {
                        queryTextBuilder.AppendLine(lines[i]);
                    }
                    
                    string queryText = queryTextBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(queryText))
                    {
                        Console.WriteLine($"Empty query text for query ID {queryId}, skipping");
                        continue;
                    }
                    
                    queries[queryId] = queryText;
                    Console.WriteLine($"Added query {queryId} with {queryText.Length} characters");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing query block: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Successfully loaded {queries.Count} queries");
            return queries;
        }

        public async Task<Dictionary<int, List<string>>> ReadRelevanceJudgmentsAsync()
        {
            var relevanceJudgments = new Dictionary<int, List<string>>();
            
            if (!File.Exists(_relFilePath))
            {
                throw new FileNotFoundException($"CISI.REL file not found at: {_relFilePath}");
            }
            
            string[] lines = await File.ReadAllLinesAsync(_relFilePath);
            
            Console.WriteLine($"Reading {lines.Length} lines from relevance judgments file at {_relFilePath}");
            
            foreach (var line in lines)
            {
                try
                {
                    // The format appears to be: [query_id] [doc_id] 0 0.000000
                    // with multiple spaces between fields
                    var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length < 2)
                    {
                        Console.WriteLine($"Skipping invalid line (not enough parts): '{line}'");
                        continue;
                    }
                    
                    if (!int.TryParse(parts[0], out int queryId))
                    {
                        Console.WriteLine($"Skipping line with invalid query ID: '{line}'");
                        continue;
                    }
                    
                    if (!int.TryParse(parts[1], out int docIdInt))
                    {
                        Console.WriteLine($"Skipping line with invalid document ID: '{line}'");
                        continue;
                    }
                    
                    // Convert the document ID to string
                    string docId = docIdInt.ToString();
                    
                    if (!relevanceJudgments.ContainsKey(queryId))
                    {
                        relevanceJudgments[queryId] = new List<string>();
                    }
                    
                    relevanceJudgments[queryId].Add(docId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing line '{line}': {ex.Message}");
                }
            }
            
            Console.WriteLine($"Loaded relevance judgments for {relevanceJudgments.Count} queries");
            
            foreach (var entry in relevanceJudgments)
            {
                Console.WriteLine($"Query {entry.Key}: {entry.Value.Count} relevant documents");
            }
            
            return relevanceJudgments;
        }

        public async Task<EvaluationResult> EvaluateSystemAsync(int topK = 10)
        {
            Console.WriteLine($"Starting evaluation with topK={topK}");
            var result = new EvaluationResult();
            
            try
            {
                var queries = await ReadQueriesAsync();
                Console.WriteLine($"Successfully read {queries.Count} queries");
                
                var relevanceJudgments = await ReadRelevanceJudgmentsAsync();
                Console.WriteLine($"Successfully read relevance judgments for {relevanceJudgments.Count} queries");
                
                foreach (var query in queries)
                {
                    int queryId = query.Key;
                    string queryText = query.Value;
                    
                    try
                    {
                        Console.WriteLine($"Processing query {queryId}: '{queryText.Substring(0, Math.Min(50, queryText.Length))}...'");
                        
                        // Check if we have relevance judgments for this query
                        if (!relevanceJudgments.ContainsKey(queryId))
                        {
                            Console.WriteLine($"No relevance judgments found for query {queryId}, skipping...");
                            continue;
                        }
                        
                        // Define relevant document IDs
                        var relevantDocIds = relevanceJudgments[queryId];
                        Console.WriteLine($"Found {relevantDocIds.Count} relevant documents for query {queryId}");
                        
                        // Process the query and retrieve results
                        Console.WriteLine("Processing query...");
                        string processedQuery = await _queryPreprocessor.ProcessQueryAsync(queryText);
                        Console.WriteLine("Generating embedding...");
                        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(processedQuery);
                        Console.WriteLine("Finding similar embeddings...");
                        var retrievedDocs = _dbEmbeddingService.FindSimilarEmbeddings(queryEmbedding, topK);
                        Console.WriteLine($"Retrieved {retrievedDocs.Count} documents");
                        
                        // Extract document IDs from retrieved results
                        var retrievedDocIds = retrievedDocs.Select(doc => doc.DocumentId).ToList();
                        Console.WriteLine($"Retrieved document IDs: [{string.Join(", ", retrievedDocIds.Take(5))}]" + (retrievedDocIds.Count > 5 ? ", ..." : ""));
                        
                        // Find intersection of relevant and retrieved docs
                        var relevantRetrieved = relevantDocIds.Intersect(retrievedDocIds).ToList();
                        
                        // Calculate metrics
                        double precision = retrievedDocIds.Count > 0 
                            ? (double)relevantRetrieved.Count / retrievedDocIds.Count 
                            : 0;
                        
                        double recall = relevantDocIds.Count > 0 
                            ? (double)relevantRetrieved.Count / relevantDocIds.Count 
                            : 0;
                        
                        double f1Score = (precision + recall) > 0 
                            ? 2 * precision * recall / (precision + recall) 
                            : 0;
                        
                        // Create metrics object
                        var metrics = new EvaluationMetrics
                        {
                            QueryId = queryId,
                            Query = queryText,
                            Precision = precision,
                            Recall = recall,
                            F1Score = f1Score,
                            RetrievedDocumentIds = retrievedDocIds,
                            RelevantDocumentIds = relevantDocIds,
                            RelevantRetrievedDocumentIds = relevantRetrieved
                        };
                        
                        result.QueryMetrics.Add(metrics);
                        
                        // Print results for this query
                        Console.WriteLine($"Query {queryId}:");
                        Console.WriteLine($"  Precision: {precision:F4}");
                        Console.WriteLine($"  Recall: {recall:F4}");
                        Console.WriteLine($"  F1 Score: {f1Score:F4}");
                        Console.WriteLine($"  Retrieved {retrievedDocIds.Count} documents, {relevantRetrieved.Count} are relevant");
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing query {queryId}: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                }
                
                // Calculate averages
                if (result.QueryMetrics.Count > 0)
                {
                    result.AveragePrecision = result.QueryMetrics.Average(m => m.Precision);
                    result.AverageRecall = result.QueryMetrics.Average(m => m.Recall);
                    result.AverageF1Score = result.QueryMetrics.Average(m => m.F1Score);
                    
                    Console.WriteLine("Overall Results:");
                    Console.WriteLine($"  Average Precision: {result.AveragePrecision:F4}");
                    Console.WriteLine($"  Average Recall: {result.AverageRecall:F4}");
                    Console.WriteLine($"  Average F1 Score: {result.AverageF1Score:F4}");
                }
                else
                {
                    Console.WriteLine("No metrics were calculated. Check logs for errors.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Evaluation failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
            
            return result;
        }
    }
} 