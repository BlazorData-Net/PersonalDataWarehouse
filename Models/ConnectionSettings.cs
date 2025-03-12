namespace PersonalDataWarehouse.Models
{
    public class ConnectionSettings
    {
        public SQLServerSettings SQLServer { get; set; }
        public FabricSettings Fabric { get; set; }
        public AzureStorageSettings AzureStorage { get; set; }
    }

    public class SQLServerSettings
    {
        public string DatabaseName { get; set; }
        public string DatabasePassword { get; set; }
        public string DatabaseUsername { get; set; }
        public string IntegratedSecurityDisplay { get; set; }
        public string ServerName { get; set; }
    }

    public class FabricSettings
    {
        public string DatabaseName { get; set; }
        public string DatabasePassword { get; set; }
        public string DatabaseUsername { get; set; }
        public string IntegratedSecurityDisplay { get; set; }
        public string ServerName { get; set; }
    }

    public class AzureStorageSettings
    {
        public string StorageAccountName { get; set; }
        public string ContainerName { get; set; }
        public string AccountKey { get; set; }
    }
}
