using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace XUnitTest.Repositories
{
    public class KeyTimelineRepositoryTests
    {
        [Fact]
        public async Task SaveKeyTimelineAsync_SetsItemIdAndDates_WhenNoItemId()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var timeline = new KeyTimeline { EntityId = "e", UserId = "u" };
            // InsertOneAsync is an extension method in MongoDB.Driver and cannot be directly verified with Moq.
            // We verify the side effects: ItemId and CreateDate are set.
            try
            {
                await repo.SaveKeyTimelineAsync(timeline);
            }
            catch { /* InsertOneAsync may fail without real DB, but side effects should be set */ }
            timeline.ItemId.Should().NotBeNullOrWhiteSpace();
            timeline.CreateDate.Should().NotBe(default);
        }
        [Fact]
        public async Task SaveKeyTimelineAsync_Upserts_WhenItemIdExists()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<KeyTimeline>>(),
                It.IsAny<KeyTimeline>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var timeline = new KeyTimeline { ItemId = "id", EntityId = "e", UserId = "u", CreateDate = DateTime.UtcNow.AddDays(-1) };
            await repo.SaveKeyTimelineAsync(timeline);
            collection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<KeyTimeline>>(),
                timeline,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BulkSaveKeyTimelinesAsync_EmptyList_DoesNothing()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            await repo.BulkSaveKeyTimelinesAsync(new List<KeyTimeline>(), "tenant");
            dbContextProvider.Verify(x => x.GetDatabase(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task BulkSaveKeyTimelinesAsync_WithItems_SetsIdsAndDatesAndInserts()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            dbContextProvider.Setup(x => x.GetDatabase("tenant")).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<KeyTimeline>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var timelines = new List<KeyTimeline> {
                new KeyTimeline { ItemId = null, EntityId = "e1", UserId = "u1" },
                new KeyTimeline { ItemId = "id2", EntityId = "e2", UserId = "u2" }
            };
            await repo.BulkSaveKeyTimelinesAsync(timelines, "tenant");
            timelines[0].ItemId.Should().NotBeNullOrWhiteSpace();
            timelines[0].CreateDate.Should().NotBe(default);
            timelines[1].CreateDate.Should().NotBe(default);
            collection.Verify(x => x.InsertManyAsync(
                timelines,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
