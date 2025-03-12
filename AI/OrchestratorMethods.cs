using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using OpenAI;
using Azure.AI.OpenAI;
using Azure.Identity;
using PersonalDataWarehouse.Model;
using PersonalDataWarehouse.Services;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Azure;

namespace PersonalDataWarehouse.AI
{
    public partial class OrchestratorMethods
    {
        public SettingsService SettingsService { get; set; }
        public LogService LogService { get; set; }
        public string Summary { get; set; }

        public List<(string, float)> similarities = new List<(string, float)>();

        // Constructor
        public OrchestratorMethods(SettingsService _SettingsService, LogService _LogService)
        {
            SettingsService = _SettingsService;
            LogService = _LogService;
        }

        // Utility Methods

        #region public IChatClient CreateAIChatClient(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
        public IChatClient CreateAIChatClient(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
        {
            ApiKeyCredential apiKeyCredential = new ApiKeyCredential(ApiKey);

            if (AIType == "OpenAI")
            {
                OpenAIClientOptions options = new OpenAIClientOptions();
                options.NetworkTimeout = TimeSpan.FromSeconds(520);

                return new OpenAIClient(
                    apiKeyCredential, options)
                    .AsChatClient(AIModel);
            }
            else if (AIType == "Azure OpenAI")
            {
                AzureOpenAIClientOptions options = new AzureOpenAIClientOptions();
                options.NetworkTimeout = TimeSpan.FromSeconds(520);

                return new AzureOpenAIClient(
                    new Uri(Endpoint),
                    apiKeyCredential, options)
                    .AsChatClient(AIModel);
            }
            else if (AIType == "LM Studio")
            {
                OpenAIClientOptions options = new OpenAIClientOptions();
                options.NetworkTimeout = TimeSpan.FromSeconds(520);

                options.Endpoint = new Uri(Endpoint);

                // If Endpoint does not end with "/v1/", append it
                if (!Endpoint.EndsWith("/v1/"))
                {
                    Endpoint += "/v1/";
                }

                options.Endpoint = new Uri(Endpoint);

                return new OpenAIClient(
                    apiKeyCredential, options)
                    .AsChatClient(AIModel);
            }
            else // Ollama
            {
                return new OllamaChatClient(
                    new Uri(Endpoint),
                    AIModel);
            }
        }
        #endregion

        #region public float CosineSimilarity(float[] vector1, float[] vector2)
        public float CosineSimilarity(float[] vector1, float[] vector2)
        {
            // Initialize variables for dot product and
            // magnitudes of the vectors
            float dotProduct = 0;
            float magnitude1 = 0;
            float magnitude2 = 0;

            // Iterate through the vectors and calculate
            // the dot product and magnitudes
            for (int i = 0; i < vector1?.Length; i++)
            {
                // Calculate dot product
                dotProduct += vector1[i] * vector2[i];

                // Calculate squared magnitude of vector1
                magnitude1 += vector1[i] * vector1[i];

                // Calculate squared magnitude of vector2
                magnitude2 += vector2[i] * vector2[i];
            }

            // Take the square root of the squared magnitudes
            // to obtain actual magnitudes
            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            // Calculate and return cosine similarity by dividing
            // dot product by the product of magnitudes
            return dotProduct / (magnitude1 * magnitude2);
        }
        #endregion

        #region private string CombineAndSortLists(string paramExistingList, string paramNewList)
        private string CombineAndSortLists(string paramExistingList, string paramNewList)
        {
            // Split the lists into an arrays
            string[] ExistingListArray = paramExistingList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] NewListArray = paramNewList.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Combine the lists
            string[] CombinedListArray = ExistingListArray.Concat(NewListArray).ToArray();

            // Remove duplicates
            CombinedListArray = CombinedListArray.Distinct().ToArray();

            // Sort the array
            Array.Sort(CombinedListArray);

            // Combine the array into a string
            string CombinedList = string.Join("\n", CombinedListArray);

            return CombinedList;
        }
        #endregion

        #region public static string TrimToMaxWords(string input, int maxWords = 500)
        public static string TrimToMaxWords(string input, int maxWords = 500)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] words = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length <= maxWords)
                return input;

            return string.Join(" ", words.Take(maxWords));
        }
        #endregion

        #region public bool IsValidFolderName(string folderName)
        public bool IsValidFolderName(string folderName)
        {
            string invalidChars = new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars());
            Regex containsABadCharacter = new Regex("[" + Regex.Escape(invalidChars) + "]");
            if (containsABadCharacter.IsMatch(folderName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region public static string TrimInnerSpaces(string input)
        public static string TrimInnerSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return Regex.Replace(input, @"\s{2,}", " ");
        }
        #endregion

        #region public string SanitizeFileName(string input)
        private static readonly char[] InvalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
        private static readonly string[] ReservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        public string SanitizeFileName(string input)
        {
            // Strip out invalid characters
            string sanitized = new string(input.Where(ch => !InvalidFileNameChars.Contains(ch)).ToArray());

            // Remove the | character
            sanitized = sanitized.Replace("|", "");

            return sanitized;
        }
        #endregion

        #region public static List<string> ParseStringToList(string input)
        public static List<string> ParseStringToList(string input)
        {
            // Remove the brackets and split the string by comma
            string[] items = Regex.Replace(input, @"[\[\]]", "").Split(',');

            // Convert the array to a List<string> and return
            return new List<string>(items);
        }
        #endregion

        #region public class ReadTextEventArgs : EventArgs
        public class ReadTextEventArgs : EventArgs
        {
            public string Message { get; set; }
            public int DisplayLength { get; set; }

            public ReadTextEventArgs(string message, int display_length)
            {
                Message = message;
                DisplayLength = display_length;
            }
        }
        #endregion

        #region public static string ExtractJsonFromResponse(string input)
        public static string ExtractJsonFromResponse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            int startIndex = input.IndexOf('{');
            int endIndex = input.LastIndexOf('}');

            // Validate positions
            if (startIndex == -1 || endIndex == -1 || endIndex < startIndex)
                return string.Empty;

            // Extract and return the substring that should represent valid JSON
            return input.Substring(startIndex, endIndex - startIndex + 1);
        } 
        #endregion
    }
}