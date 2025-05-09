using CSScriptLib;
using Microsoft.JSInterop;
using PersonalDataWarehouse.Services;
using Renci.SshNet.Messages;
using System.Data;
using System.Linq;
using System.Text.Json.Nodes;
public class Dataloader
{
    private readonly IJSRuntime _jsRuntime;

    public Dataloader(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Dataloader()
    {

    }

    public async Task<IEnumerable<IDictionary<string, object>>> LoadParquet(string DatabaseName, string TableName)
    {
        IEnumerable<IDictionary<string, object>> response = new List<IDictionary<string, object>>();

        // Load the DataTable
        String parquetFolder = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{DatabaseName}/Parquet";
        var fileName = Path.Combine(parquetFolder, $"{TableName}.parquet");

        if (System.IO.File.Exists(fileName))
        {
            DataService objDataService = new DataService();
            var CurrentDataTable = await objDataService.ReadParquetFileAsync(fileName);

            // Convert the DataTable to a List of Dictionaries
            response = CurrentDataTable.AsEnumerable()
                .Select(row => CurrentDataTable.Columns
                .Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => row[column]))
                .ToList();

            return response;
        }

        return response;
    }

    public async Task<IEnumerable<IDictionary<string, object>>> LoadView(string DatabaseName, string ViewName)
    {
        IEnumerable<IDictionary<string, object>> response = new List<IDictionary<string, object>>();

        // Load the DataTable
        String viewFolder = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{DatabaseName}/Views";
        var fileName = Path.Combine(viewFolder, $"{ViewName}.view");

        if (System.IO.File.Exists(fileName))
        {
            // Read the View File
            string paramCode = System.IO.File.ReadAllText(fileName);

            // Convert the DataTable to a List of Dictionaries
            response = await RunDynamicCode(paramCode);

            return response;
        }

        return response;
    }

    public async Task<IEnumerable<IDictionary<string, object>>> RunDynamicCode(string paramCode)
    {
        List<Dictionary<string, object>> dataRows = new List<Dictionary<string, object>>();

        if (paramCode.Contains("def load_data()"))
        {
            // Python code

            // Get the referenced Parquet files in the Python script
            var referencedFiles = DataService.ExtractParquetPaths(paramCode);

            // Call JS to execute Python via Pyodide and clearFilesInPyodide
            await _jsRuntime.InvokeAsync<string>("clearFilesInPyodide");

            // Load the referenced Parquet files into Pyodide
            foreach (var filePath in referencedFiles)
            {
                // Open filePath as a FileStream
                using var FileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using var memoryStream = new MemoryStream();
                await FileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var bytes = memoryStream.ToArray();

                // Convert to Base64 for JS interop
                var base64 = Convert.ToBase64String(bytes);

                // Get the file name from the path
                var fileName = Path.GetFileName(filePath);

                // Get Database name from the path
                var DatabaseName = DataService.GetDatabaseName(filePath);

                await _jsRuntime.InvokeAsync<string>("writeFileToPyodide", new object[] { DatabaseName, fileName, base64 });
            }

            // Call JS to execute Python via Pyodide and get JSON
            var json = await _jsRuntime.InvokeAsync<string>("runPythonScript", new object[] { paramCode });

            // Parse and flatten JSON into a table-friendly format
            dataRows = new List<Dictionary<string, object>>();
            var parsedArray = JsonNode.Parse(json)?.AsArray();

            if (parsedArray != null)
            {
                foreach (var item in parsedArray)
                {
                    var dict = new Dictionary<string, object>();

                    if (item is JsonObject obj)
                    {
                        foreach (var prop in obj)
                        {
                            if(prop.Value != null)
                            {
                                dict[prop.Key] = Convert.ToString(prop.Value);
                            }
                            else
                            {
                                dict[prop.Key] = null;
                            }
                        }
                    }

                    dataRows.Add(dict);
                }
            }

            return dataRows as IEnumerable<IDictionary<string, object>>;
        }
        else if (paramCode.Contains("public async Task<IEnumerable<IDictionary<string, object>>> ReturnResult()"))
        {
            // C# Code
            dynamic script = CSScript.Evaluator.LoadMethod(paramCode);

            var result = await script.ReturnResult();

            return result as IEnumerable<IDictionary<string, object>>;
        }
        else
        {
            throw new InvalidOperationException("Invalid code.");
        }
    }
}
