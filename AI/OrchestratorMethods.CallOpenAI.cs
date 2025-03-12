using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using PersonalDataWarehouse.Services;
using DocumentFormat.OpenXml.EMMA;

namespace PersonalDataWarehouse.AI
{
    public class AIResponse
    {
        [JsonProperty("response")]
        public string Response { get; set; }
        [JsonProperty("error")]
        public string Error { get; set; }
    }
    public partial class OrchestratorMethods
    {
        #region public async Task<bool> TestAccessAsync(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
        public async Task<bool> TestAccessAsync(string AIType, string AIModel, string ApiKey, string Endpoint, string AIEmbeddingModel)
        {
            var chatClient = CreateAIChatClient(AIType, AIModel, ApiKey, Endpoint, AIEmbeddingModel);
            string SystemMessage = "Please return the following as json: \"This is successful\" in this format {\r\n  'message': message\r\n}";
            var response = await chatClient.CompleteAsync(SystemMessage);

            if (response.Choices.Count == 0)
            {
                return false;
            }

            // Pass though ExtractJsonFromResponse
            var json = ExtractJsonFromResponse(response.Choices.FirstOrDefault().Text);

            return true;
        }
        #endregion

        #region public async Task<AIResponse> CallOpenAIAsync(SettingsService objSettings, string ApiKey, string AIQuery)
        public async Task<AIResponse> CallOpenAIAsync(SettingsService objSettings, string ApiKey, string AIQuery)
        {
            try
            {
                // Create the AI chat client using the provided settings
                var chatClient = CreateAIChatClient(
                    SettingsService.Settings.ApplicationSettings.AIType,
                    SettingsService.Settings.ApplicationSettings.AIModel, 
                    ApiKey,
                    SettingsService.Settings.ApplicationSettings.Endpoint,
                    SettingsService.Settings.ApplicationSettings.AIEmbeddingModel);

                // Send the system message to the AI chat client
                var response = await chatClient.CompleteAsync(AIQuery);

                // Check if the response contains any choices
                if (response.Choices == null || response.Choices.Count == 0)
                {
                    // Optionally, log the absence of choices or handle it as needed
                    return new AIResponse() { Response = "", Error = "No choices returned in the AI response." };
                }

                // Extract the text from the first choice
                string jsonResponse = response.Choices[0].Text.Trim();

                jsonResponse = ExtractJsonFromResponse(jsonResponse);

                try
                {
                    var parsedResponse = JsonConvert.DeserializeObject<AIResponse>(jsonResponse);

                    if (parsedResponse != null && !string.IsNullOrEmpty(parsedResponse.Response))
                    {
                        return parsedResponse;
                    }
                    else
                    {
                        return new AIResponse() { Response = "", Error = "Parsed response is null or missing the 'Response' field." };
                    }
                }
                catch (JsonException jsonEx)
                {
                    // Handle JSON parsing errors
                    LogService.WriteToLog($"Error parsing JSON response: {jsonEx.Message}");
                    return new AIResponse() { Response = "", Error = $"Error parsing JSON response: {jsonEx.Message}" };
                }
            }
            catch (Exception ex)
            {
                // Handle unexpected exceptions
                LogService.WriteToLog($"An error occurred while calling the AI model: {ex.Message}");
                return new AIResponse() { Response = "", Error = $"An error occurred while testing access: {ex.Message}" };
            }
        }
        #endregion
    }
}
