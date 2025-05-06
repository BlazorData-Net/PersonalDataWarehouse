using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PersonalDataWarehouse.Services;
using PersonalDataWarehouse.Model;
using System.Data;
using System.Net;
using Microsoft.JSInterop;

namespace PersonalDataWarehouse.Services
{
    // This attribute restricts access to localhost only
    // It is applied to the entire controller 
    // It checks the remote IP address of the request and returns a 403 Forbidden response if it's not localhost
    public class LocalhostOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var remoteIp = context.HttpContext.Connection.RemoteIpAddress;

            // Check both IPv4 and IPv6 loopback addresses
            if (!IPAddress.IsLoopback(remoteIp))
            {
                context.Result = new ForbidResult();
            }
        }
    }

    [LocalhostOnly]
    public class ReportController : ControllerBase
    {
        private readonly IJSRuntime _jsRuntime; // Add a private field for IJSRuntime

        // Constructor to inject IJSRuntime
        public ReportController(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        [HttpGet("/api/GetStatus")]
        public IActionResult GetStatus()
        {
            return Ok(new { Status = "Working" });
        }

        [HttpGet("/api/GetData")]
        public async Task<IActionResult> GetData(string database, string datasource)
        {  
            try
            {
                // Define directory for output
                String ClassesPath = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{database}/Classes";

                // **********************
                // ** Create the XML file
                // **********************

                // Load the class and generate the schema
                // Load the class by using its name and get its type
                var codeFile = $@"{ClassesPath}/{datasource}.cs";

                if (!System.IO.File.Exists(codeFile))
                {
                    string error_message = $"ERROR: Passed database:{database} - datasource {datasource} - {codeFile} not found";

                    // Log the error
                    LogService objLogService = new LogService();
                    await objLogService.WriteToLogAsync($"/api/GetData Error - {error_message}");

                    return Ok(error_message);
                }

                // Load the code from the file
                var code = System.IO.File.ReadAllText(codeFile);

                // Get the Type for the class we want to generate the RDL for
                var ClassType = XsdGenerator.GetTypeFromCode(code, datasource);

                var dt = await GetDataTableAsync(database, datasource);

                // Generate the XML string
                string XMLString = XsdGenerator.GenerateXmlForType(ClassType, dt);

                return Ok(XMLString);
            }
            catch (Exception ex)
            {
                string error_message = $"ERROR: Check exact casing of database and data source! Passed database:{database} - datasource {datasource} - {ex.Message}";

                // Log the error
                LogService objLogService = new LogService();
                await objLogService.WriteToLogAsync($"/api/GetData Error - {error_message}");

                // Return the error message
                return Ok(error_message);
            }
        }

        // Utility

        private async Task<System.Data.DataTable> GetDataTableAsync(string database, string datasource)
        {
            // Determine if it is a Table or a View
            // If the file is in the Parquet folder, it is a Table
            // If the file is in the Views folder, it is a View

            String ClassType = "Table";

            var ClassName = DataService.FirstCharToUpper(datasource.ToLower());

            String viewsFolder = $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}/PersonalDataWarehouse/Databases/{database}/Views";
            
            var fileName = Path.Combine(viewsFolder, $"{ClassName}.view");

            if (System.IO.File.Exists(fileName))
            {
                ClassType = "View";
            }

            IEnumerable<IDictionary<string, object>> result = new List<IDictionary<string, object>>();

            Dataloader dataloader = new Dataloader(_jsRuntime);

            // Get the data based on the ClassType
            if (ClassType == "Table")
            {
                // Get the data from the Parquet file
                result = await dataloader.LoadParquet(database, ClassName);
            }
            else // View
            {
                // Get the data from the View file
                result = await dataloader.LoadView(database, ClassName);
            }

            // Get the fields from the first record (ensure result is not empty)
            var fields = result.Any()
                ? result.First().Keys.ToList()
                : new List<string>();

            // Create a DataTable
            System.Data.DataTable dt = new System.Data.DataTable("MyDataTable");

            // Add an Id column
            dt.Columns.Add("Id", typeof(int));

            // Add columns for all fields (replace spaces with underscores)
            foreach (var field in fields)
            {
                string columnName = field.Replace(" ", "_");
                dt.Columns.Add(columnName, typeof(object));
            }

            // Fill the DataTable rows
            int i = 0;
            foreach (var item in result)
            {
                DataRow newRow = dt.NewRow();
                newRow["Id"] = i++;

                foreach (var field in fields)
                {
                    string columnName = field.Replace(" ", "_");
                    newRow[columnName] = item.ContainsKey(field) ? item[field] : null;
                }

                dt.Rows.Add(newRow);
            }

            return dt;
        }
    }
}
