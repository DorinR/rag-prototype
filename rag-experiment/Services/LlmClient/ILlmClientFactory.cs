using rag_experiment.Models;

namespace rag_experiment.Services
{
    /// <summary>
    /// Factory interface for creating LLM clients with different model tier configurations.
    /// Enables runtime selection of models based on performance/cost requirements.
    /// </summary>
    /// <remarks>
    /// Usage example:
    /// <code>
    /// public class MyService
    /// {
    ///     private readonly ILlmClientFactory _llmFactory;
    ///     
    ///     public MyService(ILlmClientFactory llmFactory)
    ///     {
    ///         _llmFactory = llmFactory;
    ///     }
    ///     
    ///     public async Task&lt;string&gt; SimpleQuery(string query, string context)
    ///     {
    ///         // Use fast, cheap model for simple queries
    ///         var client = _llmFactory.CreateClient(LlmModelTier.Fast);
    ///         return await client.GenerateResponseAsync(query, context);
    ///     }
    ///     
    ///     public async Task&lt;string&gt; ComplexAnalysis(string query, string context)
    ///     {
    ///         // Use premium model for complex reasoning
    ///         var client = _llmFactory.CreateClient(LlmModelTier.Premium);
    ///         return await client.GenerateResponseAsync(query, context);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface ILlmClientFactory
    {
        /// <summary>
        /// Creates an LLM client configured for the specified model tier.
        /// </summary>
        /// <param name="tier">The model tier to use (Fast, Standard, or Premium)</param>
        /// <returns>An ILlmService instance configured with the tier's settings</returns>
        /// <exception cref="ArgumentException">Thrown if the tier is not configured in appsettings.json</exception>
        /// <example>
        /// <code>
        /// // For high-volume, simple queries (cheapest)
        /// var fastClient = factory.CreateClient(LlmModelTier.Fast);
        /// 
        /// // For balanced performance and cost
        /// var standardClient = factory.CreateClient(LlmModelTier.Standard);
        /// 
        /// // For complex reasoning (most expensive)
        /// var premiumClient = factory.CreateClient(LlmModelTier.Premium);
        /// </code>
        /// </example>
        ILlmService CreateClient(LlmModelTier tier);
    }
}

