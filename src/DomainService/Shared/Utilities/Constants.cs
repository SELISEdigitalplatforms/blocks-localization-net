using Blocks.Genesis;

namespace DomainService.Utilities
{
    public static class Constants
    {
        public const string UilmQueue = "blocks_uilm_listener";
        public const string UilmImportExportQueue = "blocks_uilm_import_export_listener";
        public const string TranslateAllKeysQueue = "blocks_uilm_translate_all_keys_listener";
        public const string TranslateBlocksLanguageKeyQueue = "blocks_uilm_translate_blocks_language_key_listener";
        public const string EnvironmentDataMigrationQueue = "blocks_uilm_environment_data_migration_listener";
        public static MessageConfiguration GetMessageConfiguration()
        {
            return new MessageConfiguration
            {
                AzureServiceBusConfiguration = new AzureServiceBusConfiguration
                {
                    Queues = [
                        Constants.UilmQueue,
                        Constants.UilmImportExportQueue,
                        Constants.EnvironmentDataMigrationQueue,
                        Constants.TranslateAllKeysQueue,
                        Constants.TranslateBlocksLanguageKeyQueue
                    ],
                    Topics = []
                }
            };
        } 
    }
}
