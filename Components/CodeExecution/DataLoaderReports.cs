using CSScriptLib;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.JSInterop;
using PersonalDataWarehouse.Services;
using Renci.SshNet.Messages;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
public class DataLoaderReports
{
    private readonly IJSRuntime _jsRuntime;

    public DataLoaderReports(IJSRuntime jsRuntime) 
    {
        _jsRuntime = jsRuntime;
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

        // Load the View
        String viewFolder = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{DatabaseName}/Views";
        var fileName = Path.Combine(viewFolder, $"{ViewName}.view");

        if (System.IO.File.Exists(fileName))
        {
            // Read the View File
            string paramCode = System.IO.File.ReadAllText(fileName);

            if (paramCode.Contains("def load_data()"))
            {
                // Python code
                // Load the associated C# class 
                String classFolder = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{DatabaseName}/Classes";
                var classFileName = Path.Combine(classFolder, $"{ViewName}.cs");

                if (System.IO.File.Exists(classFileName))
                {
                    // 1) Read the class source
                    var classCode = await File.ReadAllTextAsync(classFileName);

                    // 1.1) Remove the namespace declaration
                    classCode = Regex.Replace(classCode, @"namespace\s+\w+\s*{(.*)}\s*$", "$1", RegexOptions.Singleline);

                    // 2) Compile it into an Assembly
                    var asm = CSScript.Evaluator.CompileCode(classCode);

                    // 3) Grab your POCO type by its full name
                    var classType = asm.GetType("css_root+" + ViewName, false, true);

                    if (classType == null)
                        throw new InvalidOperationException($"LoadView {DatabaseName}/{ViewName} - Could not find type {classType}");

                    // 4) Reflect its public instance properties
                    var props = classType
                                  .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(p => p.CanRead)
                                  .ToArray();

                    // If there is a property named Id remove it
                    var idProp = props.FirstOrDefault(p => p.Name == "Id");
                    if (idProp != null)
                    {
                        props = props.Where(p => p.Name != "Id").ToArray();
                    }

                    var rnd = new Random();
                    var sampleList = new List<IDictionary<string, object>>(10);

                    // 5) For i = 1..10 create a Dictionary<string,object> with a fake value per prop
                    for (int i = 0; i < 10; i++)
                    {
                        var row = new Dictionary<string, object>(props.Length);
                        foreach (var prop in props)
                        {
                            object value;
                            if (prop.PropertyType == typeof(int))
                            {
                                value = rnd.Next(1, 1000);
                            }
                            else if (prop.PropertyType == typeof(string))
                            {
                                // e.g. "FirstName_1", "Territory_Count_7", etc.
                                value = $"{prop.Name}_{i + 1}";
                            }
                            else
                            {
                                // fallback: default(T)
                                value = prop.PropertyType.IsValueType
                                          ? Activator.CreateInstance(prop.PropertyType)
                                          : null;
                            }
                            row[prop.Name] = value;
                        }
                        sampleList.Add(row);
                    }

                    // 6) Return as the completed result
                    return await Task.FromResult<IEnumerable<IDictionary<string, object>>>(sampleList);
                }
                else
                {
                    throw new FileNotFoundException($"{classFileName} class file not found for view {DatabaseName}/{ViewName} - Resave the View to create it.");
                }
            }
            else
            {
                // C# Code

                // Convert the DataTable to a List of Dictionaries
                response = await RunDynamicCode(paramCode);
            }

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
