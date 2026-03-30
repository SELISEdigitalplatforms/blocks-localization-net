using DomainService.Utilities;
using FluentAssertions;

namespace XUnitTest
{
    public class ConstantsTests
    {
        [Fact]
        public void QueueConstants_HaveExpectedValues()
        {
            Constants.UilmQueue.Should().Be("blocks_uilm_listener");
            Constants.UilmImportExportQueue.Should().Be("blocks_uilm_import_export_listener");
            Constants.TranslateAllKeysQueue.Should().Be("blocks_uilm_translate_all_keys_listener");
            Constants.TranslateBlocksLanguageKeyQueue.Should().Be("blocks_uilm_translate_blocks_language_key_listener");
            Constants.EnvironmentDataMigrationQueue.Should().Be("blocks_uilm_environment_data_migration_listener");
        }

        [Fact]
        public void GetMessageConfiguration_ReturnsExpectedQueuesAndEmptyTopics()
        {
            var config = Constants.GetMessageConfiguration("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test");

            config.Should().NotBeNull();
            var serviceBusConfig = config.AzureServiceBusConfiguration;
            serviceBusConfig.Should().NotBeNull();

            serviceBusConfig!.Queues.Should().BeEquivalentTo(new[]
            {
                Constants.UilmQueue,
                Constants.UilmImportExportQueue,
                Constants.EnvironmentDataMigrationQueue,
                Constants.TranslateAllKeysQueue,
                Constants.TranslateBlocksLanguageKeyQueue
            });
            serviceBusConfig.Topics.Should().BeEmpty();
        }
    }
}
