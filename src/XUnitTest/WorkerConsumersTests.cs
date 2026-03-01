using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Services.HelperService;
using DomainService.Shared.Entities;
using DomainService.Shared.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Worker.Consumers;

namespace XUnitTest
{
    public class WorkerConsumersTests
    {
        [Fact]
        public async Task GenerateUilmFilesConsumer_Consume_CallsGenerateAsync()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            keyManagementService.Setup(x => x.GenerateAsync(It.IsAny<GenerateUilmFilesEvent>())).ReturnsAsync(true);

            var consumer = new GenerateUilmFilesConsumer(keyManagementService.Object);
            var @event = new GenerateUilmFilesEvent { Guid = "g-1", ModuleId = "m-1", ProjectKey = "tenant-1" };

            await consumer.Consume(@event);

            keyManagementService.Verify(x => x.GenerateAsync(@event), Times.Once);
        }

        [Fact]
        public async Task TranslateAllEventConsumer_Consume_PublishesNotificationAndWebhook()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            var webHookService = new Mock<IWebHookService>();

            keyManagementService.Setup(x => x.ChangeAll(It.IsAny<TranslateAllEvent>())).ReturnsAsync(true);
            keyManagementService.Setup(x => x.PublishTranslateAllNotification(true, It.IsAny<string>())).Returns(Task.CompletedTask);
            webHookService.Setup(x => x.CallWebhook(It.IsAny<object>())).ReturnsAsync(true);

            var consumer = new TranslateAllEventConsumer(keyManagementService.Object, webHookService.Object);
            var @event = new TranslateAllEvent
            {
                ModuleId = "m-1",
                MessageCoRelationId = "corr-1",
                ProjectKey = "tenant-1",
                DefaultLanguage = "en"
            };

            await consumer.Consume(@event);

            keyManagementService.Verify(x => x.ChangeAll(@event), Times.Once);
            keyManagementService.Verify(x => x.PublishTranslateAllNotification(true, "corr-1"), Times.Once);
            webHookService.Verify(x => x.CallWebhook(It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task TranslateBlocksLanguageKeyEventConsumer_Consume_PublishesNotification()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            keyManagementService.Setup(x => x.TranslateBlocksLanguageKey(It.IsAny<TranslateBlocksLanguageKeyEvent>())).ReturnsAsync(true);
            keyManagementService.Setup(x => x.PublishTranslateBlocksLanguageKeyNotification(true, It.IsAny<string>())).Returns(Task.CompletedTask);

            var consumer = new TranslateBlocksLanguageKeyEventConsumer(keyManagementService.Object);
            var @event = new TranslateBlocksLanguageKeyEvent
            {
                MessageCoRelationId = "corr-2",
                ProjectKey = "tenant-1",
                DefaultLanguage = "en",
                KeyId = "key-1"
            };

            await consumer.Consume(@event);

            keyManagementService.Verify(x => x.TranslateBlocksLanguageKey(@event), Times.Once);
            keyManagementService.Verify(x => x.PublishTranslateBlocksLanguageKeyNotification(true, "corr-2"), Times.Once);
        }

        [Fact]
        public async Task UilmExportEventConsumer_Consume_PublishesExportNotification()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            keyManagementService.Setup(x => x.ExportUilmFile(It.IsAny<UilmExportEvent>())).ReturnsAsync(true);
            keyManagementService.Setup(x => x.PublishUilmExportNotification(true, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var consumer = new UilmExportEventConsumer(keyManagementService.Object);
            var @event = new UilmExportEvent
            {
                MessageCoRelationId = "corr-3",
                FileId = "file-1",
                CallerTenantId = "caller-tenant",
                Languages = new List<string> { "en" },
                ReferenceFileId = "ref-1"
            };

            await consumer.Consume(@event);

            keyManagementService.Verify(x => x.ExportUilmFile(@event), Times.Once);
            keyManagementService.Verify(x => x.PublishUilmExportNotification(true, "file-1", "corr-3", "caller-tenant"), Times.Once);
        }

        [Fact]
        public async Task UilmImportEventConsumer_Consume_WhenSuccess_CallsWebhook()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            var webHookService = new Mock<IWebHookService>();

            keyManagementService.Setup(x => x.ImportUilmFile(It.IsAny<UilmImportEvent>())).ReturnsAsync(true);
            webHookService.Setup(x => x.CallWebhook(It.IsAny<object>())).ReturnsAsync(true);

            var consumer = new UilmImportEventConsumer(keyManagementService.Object, webHookService.Object);
            var @event = new UilmImportEvent { MessageCoRelationId = "corr-4", FileId = "file-2", ProjectKey = "tenant-1" };

            await consumer.Consume(@event);

            keyManagementService.Verify(x => x.ImportUilmFile(@event), Times.Once);
            webHookService.Verify(x => x.CallWebhook(It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task EnvironmentDataMigrationEventConsumer_Consume_SendsCompletionMessageOnSuccess()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            var migrationRepository = new Mock<IEnvironmentDataMigrationRepository>();
            var logger = new Mock<ILogger<EnvironmentDataMigrationEventConsumer>>();
            var messageClient = new Mock<IMessageClient>();

            migrationRepository.Setup(x => x.GetAllModulesAsync("source")).ReturnsAsync(new List<BlocksLanguageModule>());
            migrationRepository.Setup(x => x.GetAllKeysAsync("source")).ReturnsAsync(new List<BlocksLanguageKey>());
            messageClient.Setup(x => x.SendToMassConsumerAsync(It.IsAny<ConsumerMessage<MigrationCompletionEvent>>()))
                .Returns(Task.CompletedTask);

            var consumer = new EnvironmentDataMigrationEventConsumer(
                keyManagementService.Object,
                migrationRepository.Object,
                logger.Object,
                messageClient.Object);

            var @event = new EnvironmentDataMigrationEvent
            {
                ProjectKey = "source",
                TargetedProjectKey = "target",
                ShouldOverWriteExistingData = false,
                TrackerId = "tracker-1"
            };

            await consumer.Consume(@event);

            messageClient.Verify(x => x.SendToMassConsumerAsync(
                It.Is<ConsumerMessage<MigrationCompletionEvent>>(m =>
                    m.ConsumerName == "migration_topic" &&
                    m.Payload.TrackerId == "tracker-1" &&
                    m.Payload.IsSuccess)), Times.Once);
        }

        [Fact]
        public async Task EnvironmentDataMigrationEventConsumer_Consume_OnFailure_PublishesFailureAndRethrows()
        {
            var keyManagementService = new Mock<IKeyManagementService>();
            var migrationRepository = new Mock<IEnvironmentDataMigrationRepository>();
            var logger = new Mock<ILogger<EnvironmentDataMigrationEventConsumer>>();
            var messageClient = new Mock<IMessageClient>();

            migrationRepository.Setup(x => x.GetAllModulesAsync("source"))
                .ThrowsAsync(new InvalidOperationException("boom"));
            messageClient.Setup(x => x.SendToMassConsumerAsync(It.IsAny<ConsumerMessage<MigrationCompletionEvent>>()))
                .Returns(Task.CompletedTask);
            keyManagementService.Setup(x => x.PublishEnvironmentDataMigrationNotification(false, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var consumer = new EnvironmentDataMigrationEventConsumer(
                keyManagementService.Object,
                migrationRepository.Object,
                logger.Object,
                messageClient.Object);

            var @event = new EnvironmentDataMigrationEvent
            {
                ProjectKey = "source",
                TargetedProjectKey = "target",
                ShouldOverWriteExistingData = true,
                TrackerId = "tracker-2"
            };

            var action = async () => await consumer.Consume(@event);

            await action.Should().ThrowAsync<InvalidOperationException>();
            messageClient.Verify(x => x.SendToMassConsumerAsync(It.IsAny<ConsumerMessage<MigrationCompletionEvent>>()), Times.Once);
            keyManagementService.Verify(x => x.PublishEnvironmentDataMigrationNotification(false, "tracker-2", "source", "target"), Times.Once);
        }
    }
}
