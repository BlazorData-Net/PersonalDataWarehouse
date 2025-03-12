using CSScriptLib;
using PersonalDataWarehouse.Services;
using Renci.SshNet.Messages;
using System.Data;
using System.Linq;
public class Dataloader
{
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
        dynamic script = CSScript.Evaluator.LoadMethod(paramCode);

        var result = await script.ReturnResult();

        return result as IEnumerable<IDictionary<string, object>>;
    }
}