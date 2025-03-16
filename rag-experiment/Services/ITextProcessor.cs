namespace rag_experiment.Services
{
    public interface ITextProcessor
    {
        /// <summary>
        /// Cleans and preprocesses the input text by removing special characters,
        /// normalizing whitespace, and applying any other necessary transformations
        /// </summary>
        /// <param name="text">Raw input text</param>
        /// <returns>Cleaned and preprocessed text</returns>
        string ProcessText(string text);
    }
} 