using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;
using Xunit;
using XUnitTest.Shared;

namespace XUnitTest.Repositories
{
    /// <summary>
    /// Supplemental repository tests that cover branches not exercised by the
    /// existing KeyRepositoryTests / KeyTimelineRepositoryTests classes
    /// (UpsertResourceKeysWithMergeAsync, GetLocalizationTimelineAsync,
    /// GetLatestPublishTimelinesAsync).
    /// </summary>
    public class RepositoryCoverageTests
    {
        // ----------------- KeyRepository.UpsertResourceKeysWithMergeAsync -----------------

        private static (KeyRepository repo, Mock<IMongoCollection<BlocksLanguageKey>> collection)
            CreateKeyRepo()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguageKey>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguageKey>(It.IsAny<string>(), null))
                    .Returns(collection.Object);

            return (new KeyRepository(dbContextProvider.Object), collection);
        }

        [Fact]
        public async Task UpsertResourceKeysWithMergeAsync_EmptyEntities_ReturnsZerosWithoutHittingDb()
        {
            var (repo, collection) = CreateKeyRepo();

            var result = await repo.UpsertResourceKeysWithMergeAsync(new List<BlocksLanguageKey>());

            result.upsertedCount.Should().Be(0);
            result.modifiedCount.Should().Be(0);
            collection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpsertResourceKeysWithMergeAsync_WithRoutesAndContext_BulkWritesAndMergesExistingResource()
        {
            var (repo, collection) = CreateKeyRepo();

            var bulk = new BulkWriteResult<BlocksLanguageKey>.Acknowledged(
                1, 0, 0, 0, 3,
                new List<WriteModel<BlocksLanguageKey>>(),
                new List<BulkWriteUpsert>());
            collection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(bulk);

            // First UpdateOneAsync (existing resource) returns ModifiedCount = 1,
            // so the "push new resource" branch is NOT taken.
            var updateExisting = new Mock<UpdateResult>();
            updateExisting.Setup(u => u.ModifiedCount).Returns(1);
            collection.Setup(x => x.UpdateOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(updateExisting.Object);

            var entities = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = "k1",
                    ModuleId = "m1",
                    KeyName = "Hello",
                    Context = "greeting",
                    Routes = new List<string> { "/home" },
                    Resources = new[]
                    {
                        new Resource { Culture = "en", Value = "Hello" }
                    }
                }
            };

            var result = await repo.UpsertResourceKeysWithMergeAsync(entities);

            result.upsertedCount.Should().Be(0);
            result.modifiedCount.Should().Be(3);
            collection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            collection.Verify(x => x.UpdateOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpsertResourceKeysWithMergeAsync_WhenResourceNotFound_PushesNewResource()
        {
            var (repo, collection) = CreateKeyRepo();

            var bulk = new BulkWriteResult<BlocksLanguageKey>.Acknowledged(
                1, 0, 0, 0, 0,
                new List<WriteModel<BlocksLanguageKey>>(),
                new List<BulkWriteUpsert>());
            collection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(bulk);

            // ModifiedCount == 0 => code takes the "push new resource" branch
            var notFound = new Mock<UpdateResult>();
            notFound.Setup(u => u.ModifiedCount).Returns(0);
            collection.Setup(x => x.UpdateOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(notFound.Object);

            var entities = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey
                {
                    ItemId = null, // triggers SetOnInsert new-Guid branch
                    ModuleId = "m1",
                    KeyName = "Greet",
                    Resources = new[]
                    {
                        new Resource { Culture = "fr", Value = "Bonjour" }
                    }
                }
            };

            await repo.UpsertResourceKeysWithMergeAsync(entities);

            // 2 calls: one attempting to update existing, one pushing the new resource
            collection.Verify(x => x.UpdateOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task UpsertResourceKeysWithMergeAsync_SkipsResourcesWithEmptyCultureOrValue()
        {
            var (repo, collection) = CreateKeyRepo();

            var bulk = new BulkWriteResult<BlocksLanguageKey>.Acknowledged(
                3, 0, 0, 0, 0,
                new List<WriteModel<BlocksLanguageKey>>(),
                new List<BulkWriteUpsert>());
            collection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(bulk);

            var entities = new List<BlocksLanguageKey>
            {
                // Entity with no Resources at all (null) -> outer 'continue' branch
                new BlocksLanguageKey { ItemId = "a", ModuleId = "m", KeyName = "A" },
                // Entity with empty Resources list -> outer 'continue' branch
                new BlocksLanguageKey { ItemId = "b", ModuleId = "m", KeyName = "B", Resources = Array.Empty<Resource>() },
                // Entity with resources but culture/value empty -> inner 'continue' branches
                new BlocksLanguageKey
                {
                    ItemId = "c",
                    ModuleId = "m",
                    KeyName = "C",
                    Resources = new[]
                    {
                        new Resource { Culture = "", Value = "x" },
                        new Resource { Culture = "en", Value = "" }
                    }
                }
            };

            await repo.UpsertResourceKeysWithMergeAsync(entities);

            // No UpdateOneAsync calls because every resource was skipped
            collection.Verify(x => x.UpdateOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateDefinition<BlocksLanguageKey>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // ----------------- KeyTimelineRepository.GetLocalizationTimelineAsync -----------------

        private static (KeyTimelineRepository repo,
                        Mock<IDbContextProvider> dbContextProvider,
                        Mock<IMongoDatabase> database,
                        Mock<IMongoDatabase> rootDatabase,
                        Mock<IMongoCollection<KeyTimeline>> collection,
                        Mock<IMongoCollection<User>> usersCollection)
            CreateTimelineRepo()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var rootDatabase = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            var usersCollection = new Mock<IMongoCollection<User>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);

            configuration.Setup(x => x["RootTenantId"]).Returns("root-tenant");
            dbContextProvider.Setup(x => x.GetDatabase("root-tenant")).Returns(rootDatabase.Object);
            rootDatabase.Setup(x => x.GetCollection<User>(It.IsAny<string>(), null)).Returns(usersCollection.Object);

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            return (repo, dbContextProvider, database, rootDatabase, collection, usersCollection);
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_GroupsByOperationIdAndPopulatesUsers()
        {
            var (repo, _, _, _, collection, usersCollection) = CreateTimelineRepo();

            var now = DateTime.UtcNow;
            var timelines = new List<KeyTimeline>
            {
                // op-1 has 2 entries -> CurrentData/PreviousData should be null on the group
                new KeyTimeline { ItemId = "t1", OperationId = "op-1", UserId = "u1", LogFrom = "Edit", CreateDate = now.AddMinutes(-2), CurrentData = new BlocksLanguageKey { KeyName = "a" }, PreviousData = new BlocksLanguageKey { KeyName = "b" } },
                new KeyTimeline { ItemId = "t2", OperationId = "op-1", UserId = "u1", LogFrom = "Edit", CreateDate = now.AddMinutes(-1) },
                // op-2 has 1 entry -> CurrentData/PreviousData should be preserved
                new KeyTimeline { ItemId = "t3", OperationId = "op-2", UserId = "u2", LogFrom = "Publish", CreateDate = now, CurrentData = new BlocksLanguageKey { KeyName = "cur" }, PreviousData = new BlocksLanguageKey { KeyName = "prev" } }
            };
            MockCursorHelper.SetupFindAsync(collection, timelines);

            MockCursorHelper.SetupFindAsync(usersCollection, new List<User>
            {
                new User { ItemId = "u1", FirstName = "John", LastName = "Doe" },
                new User { ItemId = "u2", FirstName = null, LastName = null, Email = "jane@test.com" }
            });

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10,
                IsDescending = true
            });

            result.Should().NotBeNull();
            result.TotalCount.Should().Be(2);
            result.Operations.Should().HaveCount(2);

            var op1 = result.Operations.Single(o => o.OperationId == "op-1");
            op1.AffectedKeysCount.Should().Be(2);
            op1.CurrentData.Should().BeNull();
            op1.PreviousData.Should().BeNull();
            op1.UserName.Should().Be("John Doe");

            var op2 = result.Operations.Single(o => o.OperationId == "op-2");
            op2.AffectedKeysCount.Should().Be(1);
            op2.CurrentData!.KeyName.Should().Be("cur");
            op2.PreviousData!.KeyName.Should().Be("prev");
            op2.UserName.Should().Be("jane@test.com");
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_AppliesAllOptionalFilters_AscendingSort()
        {
            var (repo, _, _, _, collection, usersCollection) = CreateTimelineRepo();

            var now = DateTime.UtcNow;
            MockCursorHelper.SetupFindAsync(collection, new List<KeyTimeline>
            {
                new KeyTimeline { ItemId = "t1", OperationId = "op-1", UserId = "u1", LogFrom = "Edit", CreateDate = now }
            });
            MockCursorHelper.SetupFindAsyncEmpty(usersCollection);

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10,
                IsDescending = false,
                UserId = "u1",
                LogFrom = "Edit",
                LogFromValues = new List<string> { "Edit", "Delete" },
                ExcludeLogFromValues = new List<string> { "Import" },
                CreateDateRange = new DateRange
                {
                    StartDate = now.AddDays(-1),
                    EndDate = now.AddDays(1)
                }
            });

            result.Should().NotBeNull();
            result.Operations.Should().HaveCount(1);
            // No matching user in lookup -> fallback to UserId
            result.Operations[0].UserName.Should().Be("u1");
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_UserWithFirstNameOnly_BuildsTrimmedName()
        {
            var (repo, _, _, _, collection, usersCollection) = CreateTimelineRepo();

            MockCursorHelper.SetupFindAsync(collection, new List<KeyTimeline>
            {
                new KeyTimeline { ItemId = "t1", OperationId = "op-1", UserId = "u1", LogFrom = "Edit", CreateDate = DateTime.UtcNow }
            });
            MockCursorHelper.SetupFindAsync(usersCollection, new List<User>
            {
                new User { ItemId = "u1", FirstName = "Alice", LastName = null }
            });

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10,
                IsDescending = true
            });

            result.Operations[0].UserName.Should().Be("Alice");
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_UserWithNoNameNoEmail_FallsBackToUserId()
        {
            var (repo, _, _, _, collection, usersCollection) = CreateTimelineRepo();

            MockCursorHelper.SetupFindAsync(collection, new List<KeyTimeline>
            {
                new KeyTimeline { ItemId = "t1", OperationId = "op-1", UserId = "u1", LogFrom = "Edit", CreateDate = DateTime.UtcNow }
            });
            MockCursorHelper.SetupFindAsync(usersCollection, new List<User>
            {
                new User { ItemId = "u1", FirstName = null, LastName = null, Email = null }
            });

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

            result.Operations[0].UserName.Should().Be("u1");
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_NullUserId_SetsUnknown()
        {
            var (repo, _, _, _, collection, _) = CreateTimelineRepo();

            MockCursorHelper.SetupFindAsync(collection, new List<KeyTimeline>
            {
                new KeyTimeline { ItemId = "t1", OperationId = "op-1", UserId = null, LogFrom = "Edit", CreateDate = DateTime.UtcNow }
            });

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

            result.Operations[0].UserName.Should().Be("Unknown");
        }

        [Fact]
        public async Task GetLocalizationTimelineAsync_EmptyResult_ReturnsEmpty()
        {
            var (repo, _, _, _, collection, _) = CreateTimelineRepo();
            MockCursorHelper.SetupFindAsyncEmpty(collection);

            var result = await repo.GetLocalizationTimelineAsync(new GetLocalizationTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10
            });

            result.TotalCount.Should().Be(0);
            result.Operations.Should().BeEmpty();
        }

        // ----------------- KeyTimelineRepository.GetLatestPublishTimelinesAsync -----------------

        [Fact]
        public async Task GetLatestPublishTimelinesAsync_EmptyEntityIds_ReturnsEmptyWithoutHittingDb()
        {
            var (repo, dbContextProvider, _, _, _, _) = CreateTimelineRepo();

            // Clear the GetDatabase setup by using a fresh Invocations reset:
            dbContextProvider.Invocations.Clear();

            var result = await repo.GetLatestPublishTimelinesAsync(new List<string>(), "tenant");

            result.Should().BeEmpty();
            dbContextProvider.Verify(x => x.GetDatabase("tenant"), Times.Never);
        }

        [Fact]
        public async Task GetLatestPublishTimelinesAsync_ReturnsLatestTimelinePerEntity()
        {
            var (repo, _, _, _, collection, _) = CreateTimelineRepo();

            var now = DateTime.UtcNow;
            MockCursorHelper.SetupFindAsync(collection, new List<KeyTimeline>
            {
                // After descending sort, first per group is the latest
                new KeyTimeline { ItemId = "t1", EntityId = "e1", LogFrom = LogFromConstants.Published, CreateDate = now },
                new KeyTimeline { ItemId = "t2", EntityId = "e1", LogFrom = LogFromConstants.Published, CreateDate = now.AddDays(-1) },
                new KeyTimeline { ItemId = "t3", EntityId = "e2", LogFrom = LogFromConstants.Published, CreateDate = now.AddHours(-1) }
            });

            var result = await repo.GetLatestPublishTimelinesAsync(new List<string> { "e1", "e2" }, "tenant");

            result.Should().HaveCount(2);
            result["e1"].ItemId.Should().Be("t1");
            result["e2"].ItemId.Should().Be("t3");
        }
    }
}
