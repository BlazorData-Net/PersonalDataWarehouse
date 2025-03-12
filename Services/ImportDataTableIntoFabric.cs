using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Data.SqlClient;
using Parquet;
using Parquet.Rows;
using Parquet.Schema;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

public class ImportDataTableIntoFabric
{
    public DataTable InputDataTable { get; set; }
    public string TableName { get; set; }
    public string DataWarehouseConnectionString { get; set; }
    public string StorageAccountName { get; set; }
    public string StorageAccountKey { get; set; }
    public string StorageContainer { get; set; }

    public ImportDataTableIntoFabric()
    {
        InputDataTable = new DataTable();
        TableName = "";
        DataWarehouseConnectionString = "";
        StorageAccountName = "";
        StorageAccountKey = "";
        StorageContainer = "";
    }

    public async Task<string> ImportDataAsync()
    {
        string response = "";

        try
        {
            var databaseFields = ExtractFieldsFromDataTable(InputDataTable);

            var parquetSchema =
                new ParquetSchema(databaseFields.Select(f => new DataField<string>(f)).ToList());

            var parquetTable =
                new Parquet.Rows.Table(parquetSchema);

            // Loop through the DataTable and add rows to the Parquet table
            foreach (DataRow dataRow in InputDataTable.Rows)
            {
                var row = new Row(
                    databaseFields.Select(f => CleanValue(dataRow[f]?.ToString())).ToArray()
                );

                parquetTable.Add(row);
            }

            using var ms = new MemoryStream();
            await parquetTable.WriteAsync(ms);
            ms.Position = 0;

            var blobClient = new BlobContainerClient(
                $"DefaultEndpointsProtocol=https;" +
                $"AccountName={StorageAccountName};" +
                $"AccountKey={StorageAccountKey};" +
                $"EndpointSuffix=core.windows.net",
                StorageContainer);

            string fileName = Path.GetFileName(TableName) + ".parquet";
            var blob = blobClient.GetBlobClient($"{fileName}");

            await blob.UploadAsync(ms, true);

            await Task.Delay(5000);

            var sasToken = GenerateSasToken();

            var sqlScripts = GenerateSqlScripts(TableName, databaseFields, sasToken);

            using (var connection = new SqlConnection(DataWarehouseConnectionString))
            {
                connection.Open();
                foreach (var sql in sqlScripts)
                {
                    using var command = new SqlCommand(sql, connection);
                    await command.ExecuteNonQueryAsync();
                    await Task.Delay(5000); // To ensure operations are sequenced properly
                }
            }
        }
        catch (Exception ex)
        {
            response =
                $"Error! ImportData() - TableName: {TableName} - {ex.Message} - {ex.StackTrace ?? ""}";
        }

        return response;
    }

    private static string CleanValue(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : Regex.Replace(value, @"[\r\n\t]", " ").Trim();
    }

    private string GenerateSasToken()
    {
        var sasBuilder = new AccountSasBuilder
        {
            Protocol = SasProtocol.Https,
            Services = AccountSasServices.Blobs,
            ResourceTypes = AccountSasResourceTypes.Container | AccountSasResourceTypes.Object,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(AccountSasPermissions.Read | AccountSasPermissions.Write);

        return sasBuilder.ToSasQueryParameters(
            new StorageSharedKeyCredential(
                StorageAccountName,
                StorageAccountKey)).ToString();
    }

    private List<string> GenerateSqlScripts(string tableName, IList<string> fields, string sasToken)
    {
        var fieldSchema =
            string.Join(", ", fields.Select(f => $"[{f}] VARCHAR(4000)"));

        var filePath =
            $"https://{StorageAccountName}.blob.core.windows.net/{StorageContainer}/{tableName}.parquet";

        return new List<string>
        {
            $"COPY INTO {tableName} " +
            $"FROM '{filePath}' " +
            $"WITH (file_type = 'PARQUET', CREDENTIAL=(IDENTITY= 'Shared Access Signature', SECRET='{sasToken}'))"
        };
    }

    public static IList<string> ExtractFieldsFromDataTable(DataTable dataTable)
    {
        // Extract the column names from the DataTable
        return dataTable.Columns.Cast<DataColumn>().Select(col => col.ColumnName).ToList();
    }
}