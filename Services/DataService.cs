namespace PersonalDataWarehouse.Services
{
    using System;
    using System.Data;
    using System.IO;
    using System.IO.Compression;
    using Parquet;
    using Parquet.Schema;
    using Parquet.Data;
    using System.Threading.Tasks;
    using DataColumn = Parquet.Data.DataColumn;
    using ClosedXML.Excel;
    using System.Text;
    using static PersonalDataWarehouse.Services.SettingsService;

    public class DataService
    {
        #region public async Task<DataTable> ReadParquetFileAsync(string filename)
        public async Task<DataTable> ReadParquetFileAsync(string filename)
        {
            var dt = new DataTable($"{filename}");

            // Open the file stream
            using var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Create the reader asynchronously
            using (var reader = await ParquetReader.CreateAsync(fileStream))
            {
                DataField[] dataFields = reader.Schema.GetDataFields();

                for (int rgIndex = 0; rgIndex < reader.RowGroupCount; rgIndex++)
                {
                    using (var rowGroupReader = reader.OpenRowGroupReader(rgIndex))
                    {
                        // Add columns to the DataTable
                        foreach (var field in dataFields)
                        {
                            dt.Columns.Add(field.Name, field.ClrType);
                        }

                        // Read each column asynchronously from this row group
                        Parquet.Data.DataColumn[] parquetColumns = new Parquet.Data.DataColumn[dataFields.Length];

                        for (int colIndex = 0; colIndex < dataFields.Length; colIndex++)
                        {
                            parquetColumns[colIndex] = await rowGroupReader.ReadColumnAsync(dataFields[colIndex]);
                        }

                        long rowCount = rowGroupReader.RowCount;

                        // Build rows from the column data
                        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                        {
                            object[] rowValues = new object[dataFields.Length];
                            for (int colIndex = 0; colIndex < dataFields.Length; colIndex++)
                            {
                                rowValues[colIndex] = parquetColumns[colIndex].Data.GetValue(rowIndex);
                            }
                            dt.Rows.Add(rowValues);
                        }
                    }
                }
            }

            return dt;
        }
        #endregion

        #region public async Task<DataSet> ConvertParquetTableToDataSetAsync(Parquet.Rows.Table table)
        public async Task<DataSet> ConvertParquetTableToDataSetAsync(Parquet.Rows.Table table, string filename)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be null or whitespace.", nameof(filename));

            var dt = new DataTable(filename);

            // Retrieve schema information
            DataField[] dataFields = table.Schema.GetDataFields();

            // Add columns to the DataTable once
            foreach (var field in dataFields)
            {
                // Handle nullable types
                Type columnType = Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType;
                dt.Columns.Add(field.Name, columnType);
            }

            // Optimize DataTable loading
            dt.BeginLoadData();

            // Convert table to stream
            using var ms = new MemoryStream();
            await table.WriteAsync(ms);

            try
            {
                using (var reader = await ParquetReader.CreateAsync(ms))
                {
                    for (int rgIndex = 0; rgIndex < reader.RowGroupCount; rgIndex++)
                    {
                        using (var rowGroupReader = reader.OpenRowGroupReader(rgIndex))
                        {
                            // Read each column asynchronously from this row group
                            Parquet.Data.DataColumn[] parquetColumns = new Parquet.Data.DataColumn[dataFields.Length];

                            for (int colIndex = 0; colIndex < dataFields.Length; colIndex++)
                            {
                                parquetColumns[colIndex] = await rowGroupReader.ReadColumnAsync(dataFields[colIndex]);
                            }

                            long rowCount = rowGroupReader.RowCount;

                            // Build rows from the column data
                            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                            {
                                object[] rowValues = new object[dataFields.Length];
                                for (int colIndex = 0; colIndex < dataFields.Length; colIndex++)
                                {
                                    rowValues[colIndex] = parquetColumns[colIndex].Data.GetValue(rowIndex);
                                }
                                dt.Rows.Add(rowValues);
                            }
                        }
                    }
                }
            }
            finally
            {
                // End optimized loading
                dt.EndLoadData();
            }

            // Convert DataTable to DataSet
            var ds = new DataSet();
            ds.Tables.Add(dt);

            return ds;
        }
        #endregion

        #region public async Task WriteDataTableToParquetAsync(DataTable CurrentDataTable, string CurrentTableName)
        public async Task WriteDataTableToParquetAsync(DataTable CurrentDataTable, string CurrentTableName)
        {
            if (CurrentDataTable == null) throw new ArgumentNullException(nameof(CurrentDataTable));

            int columnCount = CurrentDataTable.Columns.Count;

            // Prepare fields for all columns
            var parquetFields = new List<Field>(columnCount);

            for (int i = 0; i < columnCount; i++)
            {
                string columnName = CurrentDataTable.Columns[i].ColumnName;
                parquetFields.Add(new DataField<string>(columnName));
            }

            ParquetSchema parquetSchema = new ParquetSchema(parquetFields);

            var parquetTable = new Parquet.Rows.Table(parquetSchema);

            foreach (DataRow dataRow in CurrentDataTable.Rows)
            {
                // Initialize the row with a pre-sized string array
                var row = new Parquet.Rows.Row(new string[columnCount]);

                for (int j = 0; j < columnCount; j++)
                {
                    string value = dataRow[j]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Using chained Replace calls for clarity and performance
                        value = value.Replace("\r\n", " ")
                                     .Replace("\t", " ")
                                     .Replace("\r", " ")
                                     .Replace("\n", " ")
                                     .Trim();
                    }
                    row[j] = value;
                }

                parquetTable.Add(row);
            }

            using var ms = new MemoryStream();

            await parquetTable.WriteAsync(ms); // Await for async write

            ms.Position = 0;

            // Get the Database Name and File Name
            var (Database, Table) = ExtractDatabaseAndTable(CurrentTableName);

            // Data Directory
            String folderPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases";

            string fileName = $"{folderPath}/{Database}/Parquet/{Table}.parquet";

            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                ms.CopyTo(fileStream);
            }
        }
        #endregion

        #region public async Task<byte[]> ExportDataTableToParquetAsync(DataTable CurrentDataTable, string CurrentTableName)
        public async Task<byte[]> ExportDataTableToParquetAsync(DataTable CurrentDataTable, string CurrentTableName)
        {
            if (CurrentDataTable == null) throw new ArgumentNullException(nameof(CurrentDataTable));
            int columnCount = CurrentDataTable.Columns.Count;

            // Prepare fields for all columns
            var parquetFields = new List<Field>(columnCount);
            for (int i = 0; i < columnCount; i++)
            {
                string columnName = CurrentDataTable.Columns[i].ColumnName;
                parquetFields.Add(new DataField<string>(columnName));
            }

            ParquetSchema parquetSchema = new ParquetSchema(parquetFields);

            var parquetTable = new Parquet.Rows.Table(parquetSchema);

            foreach (DataRow dataRow in CurrentDataTable.Rows)
            {
                // Initialize the row with a pre-sized string array
                var row = new Parquet.Rows.Row(new string[columnCount]);
                for (int j = 0; j < columnCount; j++)
                {
                    string value = dataRow[j]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Using chained Replace calls for clarity and performance
                        value = value.Replace("\r\n", " ")
                                     .Replace("\t", " ")
                                     .Replace("\r", " ")
                                     .Replace("\n", " ")
                                     .Trim();
                    }
                    row[j] = value;
                }
                parquetTable.Add(row);
            }

            using var ms = new MemoryStream();

            await parquetTable.WriteAsync(ms);

            return ms.ToArray();
        }
        #endregion

        #region public async Task<byte[]> ExportDataTableToExcelAsync(DataTable CurrentDataTable, string CurrentTableName)
        public async Task<byte[]> ExportDataTableToExcelAsync(DataTable CurrentDataTable, string CurrentTableName)
        {
            // Use a memory stream to store the Excel workbook
            using (var memoryStream = new MemoryStream())
            {
                using (var workbook = new XLWorkbook())
                {
                    // Add the DataTable as a worksheet with the specified table name
                    var worksheet = workbook.Worksheets.Add(CurrentDataTable, CurrentTableName);

                    // Optional: Adjust column widths for better readability
                    worksheet.Columns().AdjustToContents();

                    // Save the workbook to the memory stream
                    workbook.SaveAs(memoryStream);
                }

                // Return the byte array from the memory stream
                return await Task.FromResult(memoryStream.ToArray());
            }
        }
        #endregion

        #region public DataTable ConvertToDataTable(IEnumerable<IDictionary<string, object>> sourceData, IDictionary<string, Type> columnsDefinition)
        public DataTable ConvertToDataTable(
            IEnumerable<IDictionary<string, object>> sourceData,
            IDictionary<string, Type> columnsDefinition)
        {
            var dt = new DataTable();

            // Remove a _Id column from columnsDefinition if it exists
            if (columnsDefinition.ContainsKey("_Id"))
            {
                columnsDefinition.Remove("_Id");
            }

            //  Add a new _Id column as the first column in the DataTable
            dt.Columns.Add("_Id", typeof(int));

            // Add columns to the DataTable
            foreach (var column in columnsDefinition)
            {
                dt.Columns.Add($"{column.Key}", typeof(string));
            }

            //  For each record in sourceData, create a row in the DataTable
            foreach (var record in sourceData)
            {
                var row = dt.NewRow();

                foreach (var column in columnsDefinition)
                {
                    // Make sure the record actually contains this key
                    // and handle null or missing values as appropriate
                    if (record.ContainsKey(column.Key))
                        row[column.Key] = record[column.Key] ?? DBNull.Value;
                    else
                        row[column.Key] = DBNull.Value;
                }

                dt.Rows.Add(row);
            }

            //  Set the _Id column values to increment from 1
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                dt.Rows[i]["_Id"] = i + 1;
            }

            return dt;
        }
        #endregion

        #region public string GenerateCreateTableScript(string tableName, IEnumerable<string> tableColumns, ConnectionType paramConnectionType)
        public string GenerateCreateTableScript(string tableName, 
            IEnumerable<string> tableColumns,
            ConnectionType paramConnectionType)
        {
            // Create the SQL script
            var script = new StringBuilder();

            script.AppendLine($"CREATE TABLE [{tableName}] (");

            // Make the first column the primary key named Id
            if(paramConnectionType == ConnectionType.SQLServer)
                script.AppendLine("    [Id] INT PRIMARY KEY IDENTITY(1,1),");
            else // Fabric Warehouse
                script.AppendLine("    [_Id] VARCHAR(8000) NOT NULL,");

            foreach (var column in tableColumns.Select(c => c.Trim()))
            {              
                if (paramConnectionType == ConnectionType.SQLServer)
                    script.AppendLine($"    [{column}] NVARCHAR(MAX),");
                else // Fabric Warehouse
                    script.AppendLine($"    [{column}] VARCHAR(MAX),");
            }

            // Remove the trailing comma from the last column
            script.Remove(script.Length - 3, 1);
            script.AppendLine(");");

            if (paramConnectionType == ConnectionType.FabricWarehouse)
                script.AppendLine($" ALTER TABLE [{tableName}] ADD CONSTRAINT PK_{tableName} PRIMARY KEY NONCLUSTERED (_Id) NOT ENFORCED;");

            return script.ToString();
        }
        #endregion

        // Utility

        #region public static string FirstCharToUpper(string input)
        public static string FirstCharToUpper(string input)
        {
            return input.First().ToString().ToUpper() + input.Substring(1);
        }
        #endregion

        #region public (string Database, string Table) ExtractDatabaseAndTable(string paramTable)
        /// <summary>
        /// Splits the input string by '/' into two parts: Database and Table.
        /// Expected input format: "Database/Table"
        /// </summary>
        /// <param name="paramTable">The string containing the database and table, separated by '/'</param>
        /// <returns>A tuple (Database, Table)</returns>
        /// <exception cref="ArgumentException">Thrown if the input string is null/empty or not in the correct format</exception>
        public (string Database, string Table) ExtractDatabaseAndTable(string paramTable)
        {
            if (string.IsNullOrWhiteSpace(paramTable))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(paramTable));
            }

            string[] parts = paramTable.Split('/');
            if (parts.Length < 2)
            {
                throw new ArgumentException($"Input must be in the format 'Database/Table'. Received: {paramTable}", nameof(paramTable));
            }

            return (parts[0], parts[1]);
        }
        #endregion
    }
}