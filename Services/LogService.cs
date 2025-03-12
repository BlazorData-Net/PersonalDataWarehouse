using Newtonsoft.Json;
using OpenAI.Files;

namespace PersonalDataWarehouse.Model
{
    public class LogService
    {
        // Properties
        public string[] PersonalDataWarehouseLog { get; set; }

        // Constructor
        public LogService()
        {
            loadLog();
        }

        public void loadLog()
        {
            String folderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse";

            var PersonalDataWarehouseLogPath =
            $"{folderPath}/PersonalDataWarehouseLog.csv";

            // Read the lines from the .csv file
            using (var file = new System.IO.StreamReader(PersonalDataWarehouseLogPath))
            {
                PersonalDataWarehouseLog = file.ReadToEnd().Split('\n');
                if (PersonalDataWarehouseLog[PersonalDataWarehouseLog.Length - 1].Trim() == "")
                {
                    PersonalDataWarehouseLog = PersonalDataWarehouseLog.Take(PersonalDataWarehouseLog.Length - 1).ToArray();
                }
            }
        }

        public async Task WriteToLogAsync(string LogText)
        {
            await Task.Run(() => WriteToLog(LogText));
        }

        public void WriteToLog(string LogText)
        {
            // Open the file to get existing content
            String folderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse";

            var PersonalDataWarehouseLogPath =
                $"{folderPath}/PersonalDataWarehouseLog.csv";

            using (var file = new System.IO.StreamReader(PersonalDataWarehouseLogPath))
            {
                PersonalDataWarehouseLog = file.ReadToEnd().Split('\n');

                if (PersonalDataWarehouseLog[PersonalDataWarehouseLog.Length - 1].Trim() == "")
                {
                    PersonalDataWarehouseLog = PersonalDataWarehouseLog.Take(PersonalDataWarehouseLog.Length - 1).ToArray();
                }
            }

            // If log has more than 1000 lines, keep only the recent 1000 lines
            if (PersonalDataWarehouseLog.Length > 1000)
            {
                PersonalDataWarehouseLog = PersonalDataWarehouseLog.Take(1000).ToArray();
            }

            // Append the text to csv file
            using (var streamWriter = new StreamWriter(PersonalDataWarehouseLogPath))
            {
                // Remove line breaks from the log text
                LogText = LogText.Replace("\n", " ");

                streamWriter.WriteLine($"{DateTime.Now.ToShortDateString()}-{DateTime.Now.ToLongTimeString()} : {LogText}");
                streamWriter.WriteLine(string.Join("\n", PersonalDataWarehouseLog));
            }
        }
    }
}