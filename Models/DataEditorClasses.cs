using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PersonalDataWarehouse.Services.SettingsService;

namespace PersonalDataWarehouse.Models
{
    public class ConnectionSetting
    {
        public ConnectionType ConnectionType { get; set; }
        public string DatabaseName { get; set; }
        public string ServerName { get; set; }
        public bool IntegratedSecurity { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ConnectionString { get; set; }        
    }

    public class DatabaseImport
    {
        public ConnectionSetting ConnectionSetting { get; set; }
        public string TableName { get; set; }
        public string SQLQuery { get; set; }
    }

    public class DTOStatus
    {
        public string StatusMessage { get; set; }
        public string ConnectionString { get; set; }
        public bool Success { get; set; }
    }

    public class DTODatabaseColumn
    {
        public string ColumnName { get; set; }
        public string ColumnType { get; set; }
        public int ColumnLength { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}