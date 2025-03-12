using Newtonsoft.Json;
using System;
using System.Collections.Generic; // Needed for List<T>
using System.IO;
using System.Threading.Tasks;

namespace PersonalDataWarehouse.Services
{
    public class SettingsService
    {
        // Nested Configuration Classes
        public class ApplicationSettings
        {
            public string AIModel { get; set; }
            public string AIType { get; set; }
            public string Endpoint { get; set; }
            public string ApiVersion { get; set; }
            public string AIEmbeddingModel { get; set; }
        }

        public enum ConnectionType
        {
            SQLServer,
            FabricWarehouse,
            AzureStorage
        }

        public enum ConfigurationType
        {
            DatabaseFull,
            DatabaseConnectionOnly,
            AzureStorage
        }

        public class ConnectionSettings
        {
            public ConnectionType ConnectionType { get; set; }
            public DatabaseServerSettings DatabaseServerSettings { get; set; }
            public AzureStorageSettings AzureStorageSettings { get; set; }
            public ConnectionSettings() { }
        }

        public class DatabaseServerSettings
        {
            public string DatabaseName { get; set; }
            public string DatabaseUsername { get; set; }
            public string IntegratedSecurityDisplay { get; set; }
            public string ServerName { get; set; }
        }

        public class AzureStorageSettings
        {
            public string StorageAccountName { get; set; }
            public string ContainerName { get; set; }
        }

        public class Configuration
        {
            public ApplicationSettings ApplicationSettings { get; set; }

            public List<ConnectionSettings> ConnectionSettings { get; set; }
        }

        // Configuration Property
        public Configuration Settings { get; private set; }

        // Path to the settings file
        private readonly string _settingsPath;

        // Constructor
        public SettingsService()
        {
            // Construct the path to the settings file using Path.Combine for cross-platform compatibility
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PersonalDataWarehouse");
            _settingsPath = Path.Combine(folderPath, "PersonalDataWarehouse.config");

            LoadSettings();
        }

        /// <summary>
        /// Loads the settings from the configuration file.
        /// </summary>
        public async void LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    await InitializeDefaultSettingsAsync();
                }

                // Read the content of the settings file
                string settingsContent;
                using (var streamReader = new StreamReader(_settingsPath))
                {
                    settingsContent = streamReader.ReadToEnd();
                }

                // Deserialize the JSON content into the Configuration object
                Settings = JsonConvert.DeserializeObject<Configuration>(settingsContent);

                if (Settings == null)
                {
                    throw new InvalidDataException("Failed to deserialize the settings file.");
                }

                // Set default values for specific settings if necessary
                if (string.IsNullOrWhiteSpace(Settings.ApplicationSettings.AIType))
                {
                    Settings.ApplicationSettings.AIType = "OpenAI";
                }

                // If ConnectionSettings was missing or null, initialize it
                if (Settings.ConnectionSettings == null)
                {
                    Settings.ConnectionSettings = new List<ConnectionSettings>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                throw; 
            }
        }

        /// <summary>
        /// Saves the entire configuration to the settings file.
        /// </summary>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveSettingsAsync()
        {
            try
            {
                // Ensure the directory exists
                string folderPath = Path.GetDirectoryName(_settingsPath);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Serialize the Configuration object back to JSON
                string updatedSettings = JsonConvert.SerializeObject(Settings, Formatting.Indented);

                // Write the updated JSON back to the file asynchronously
                using (var streamWriter = new StreamWriter(_settingsPath, false))
                {
                    await streamWriter.WriteAsync(updatedSettings);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                throw; 
            }
        }

        /// <summary>
        /// Updates specific sections of the configuration and saves the changes.
        /// 
        /// If you need more granular updates (e.g., only one type of connection),
        /// you can add logic to find/modify the correct ConnectionSettings item.
        /// </summary>
        /// <param name="applicationSettings">New application settings.</param>
        /// <param name="connectionSettings">List of connection settings to overwrite existing ones.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task UpdateSettingsAsync(
            ApplicationSettings applicationSettings = null,
            List<ConnectionSettings> connectionSettings = null)
        {
            // Update the settings if new values are provided
            if (applicationSettings != null)
            {
                Settings.ApplicationSettings = applicationSettings;
            }

            // Replace or merge connection settings if provided
            if (connectionSettings != null)
            {
                // Example: simply replace the entire collection
                Settings.ConnectionSettings = connectionSettings;
            }

            // Save the updated settings to the file
            await SaveSettingsAsync();
        }

        /// <summary>
        /// Initializes default settings and saves them to the configuration file.
        /// This can be used when the configuration file does not exist.
        /// </summary>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InitializeDefaultSettingsAsync()
        {
            try
            {
                // Initialize default settings
                Settings = new Configuration
                {
                    ApplicationSettings = new ApplicationSettings
                    {
                        AIModel = "DefaultAIModel",
                        AIType = "OpenAI",
                        Endpoint = "https://api.openai.com",
                        ApiVersion = "v1",
                        AIEmbeddingModel = "DefaultEmbeddingModel"
                    },
                    ConnectionSettings = new List<ConnectionSettings>()
                };

                // Save the default settings to the file
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing default settings: {ex.Message}");
                throw;
            }
        }
    }
}