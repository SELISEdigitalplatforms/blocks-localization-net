using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using Xunit;
using KeyTimelineModel = DomainService.Services.KeyTimeline;

namespace XUnitTest
{
    public class RepositoryImplementationTests
    {
        [Fact]
        public async Task BlocksWebhookRepository_SaveAsync_UsesUpsertReplace()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksWebhook>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksWebhook>("BlocksWebhooks", It.IsAny<MongoCollectionSettings>()))
                .Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksWebhook>>(),
                    It.IsAny<BlocksWebhook>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var repository = new BlocksWebhookRepository(dbContextProvider.Object);
            var webhook = new BlocksWebhook
            {
                ItemId = "webhook-1",
                Url = "https://example.com/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { Secret = "secret", HeaderKey = "x-signature" },
                ProjectKey = "tenant-a"
            };

            await repository.SaveAsync(webhook);

            collection.Verify(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksWebhook>>(),
                    webhook,
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EnvironmentDataMigrationRepository_BulkUpsertModulesAsync_EmptyInput_DoesNotTouchDatabase()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var repository = new EnvironmentDataMigrationRepository(dbContextProvider.Object);

            await repository.BulkUpsertModulesAsync(new List<BlocksLanguageModule>(), "target-tenant", shouldOverwrite: true);

            dbContextProvider.Verify(x => x.GetDatabase(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EnvironmentDataMigrationRepository_BulkUpsertKeysAsync_EmptyInput_ReturnsEmptyResult()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var repository = new EnvironmentDataMigrationRepository(dbContextProvider.Object);

            var result = await repository.BulkUpsertKeysAsync(
                new List<BlocksLanguageKey>(),
                new List<BlocksLanguageKey>(),
                "target-tenant",
                shouldOverwrite: false);

            result.Should().NotBeNull();
            result.UpsertedKeys.Should().BeEmpty();
            result.InsertedKeys.Should().BeEmpty();
            result.UpdatedKeys.Should().BeEmpty();
            dbContextProvider.Verify(x => x.GetDatabase(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task KeyRepository_SaveKeyAsync_UsesUpsertReplace()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguageKey>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguageKey>("BlocksLanguageKeys", It.IsAny<MongoCollectionSettings>()))
                .Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                    It.IsAny<BlocksLanguageKey>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var repository = new KeyRepository(dbContextProvider.Object);
            var key = new BlocksLanguageKey
            {
                ItemId = "key-1",
                KeyName = "welcome",
                ModuleId = "module-1",
                Value = "Welcome",
                Resources = new[] { new Resource { Culture = "en", Value = "Welcome" } },
                Routes = new List<string>()
            };

            await repository.SaveKeyAsync(key);

            collection.Verify(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                    key,
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task KeyTimelineRepository_SaveKeyTimelineAsync_WithNoItemId_Inserts()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimelineModel>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimelineModel>("KeyTimelines", It.IsAny<MongoCollectionSettings>()))
                .Returns(collection.Object);
            collection.Setup(x => x.InsertOneAsync(It.IsAny<KeyTimelineModel>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var repository = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var timeline = new KeyTimelineModel
            {
                ItemId = null!,
                EntityId = "entity-1",
                UserId = "user-1"
            };

            await repository.SaveKeyTimelineAsync(timeline);

            timeline.ItemId.Should().NotBeNullOrWhiteSpace();
            collection.Verify(x => x.InsertOneAsync(
                    It.IsAny<KeyTimelineModel>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LanguageRepository_SaveAsync_UsesUpsertReplace()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguage>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguage>("BlocksLanguages", It.IsAny<MongoCollectionSettings>()))
                .Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguage>>(),
                    It.IsAny<BlocksLanguage>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var repository = new LanguageRepository(dbContextProvider.Object);
            var language = new BlocksLanguage
            {
                ItemId = "lang-1",
                LanguageName = "English",
                LanguageCode = "en",
                IsDefault = true
            };

            await repository.SaveAsync(language);

            collection.Verify(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguage>>(),
                    language,
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ModuleRepository_SaveAsync_UsesUpsertReplace()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguageModule>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguageModule>("BlocksLanguageModules", It.IsAny<MongoCollectionSettings>()))
                .Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguageModule>>(),
                    It.IsAny<BlocksLanguageModule>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var repository = new ModuleRepository(dbContextProvider.Object);
            var module = new BlocksLanguageModule
            {
                ItemId = "module-1",
                ModuleName = "auth",
                Name = "Authentication"
            };

            await repository.SaveAsync(module);

            collection.Verify(x => x.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BlocksLanguageModule>>(),
                    module,
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
