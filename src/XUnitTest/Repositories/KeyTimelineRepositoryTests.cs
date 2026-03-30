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
        private readonly Mock<IDbContextProvider> _dbContextProvider;
        private readonly Mock<IConfiguration> _configuration;
        private readonly Mock<IMongoDatabase> _database;
        private readonly Mock<IMongoCollection<KeyTimeline>> _collection;
        private readonly KeyTimelineRepository _repo;

        public KeyTimelineRepositoryTests()
        {
            _dbContextProvider = new Mock<IDbContextProvider>();
            _configuration = new Mock<IConfiguration>();
            _database = new Mock<IMongoDatabase>();
            _collection = new Mock<IMongoCollection<KeyTimeline>>();

            _dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_database.Object);
            _database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(_collection.Object);

            _repo = new KeyTimelineRepository(_dbContextProvider.Object, _configuration.Object);
        }

        [Fact]
        public async Task SaveKeyTimelineAsync_SetsItemIdAndDates_WhenNoItemId()
        {
            var timeline = new KeyTimeline { ItemId = null, EntityId = "e", UserId = "u" };

            // InsertOneAsync is settable on the mock
            _collection.Setup(x => x.InsertOneAsync(
                It.IsAny<KeyTimeline>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            await _repo.SaveKeyTimelineAsync(timeline);

            timeline.ItemId.Should().NotBeNullOrWhiteSpace();
            timeline.CreateDate.Should().NotBe(default);
            timeline.LastUpdateDate.Should().NotBe(default);

            _collection.Verify(x => x.InsertOneAsync(
                timeline,
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveKeyTimelineAsync_SetsEmptyItemIdAndDates_WhenEmptyString()
        {
            var timeline = new KeyTimeline { ItemId = "", EntityId = "e", UserId = "u" };

            _collection.Setup(x => x.InsertOneAsync(
                It.IsAny<KeyTimeline>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            await _repo.SaveKeyTimelineAsync(timeline);

            timeline.ItemId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task SaveKeyTimelineAsync_Upserts_WhenItemIdExists()
        {
            _collection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<KeyTimeline>>(),
                It.IsAny<KeyTimeline>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var timeline = new KeyTimeline { ItemId = "id", EntityId = "e", UserId = "u", CreateDate = DateTime.UtcNow.AddDays(-1) };
            await _repo.SaveKeyTimelineAsync(timeline);

            _collection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<KeyTimeline>>(),
                timeline,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BulkSaveKeyTimelinesAsync_EmptyList_DoesNothing()
        {
            await _repo.BulkSaveKeyTimelinesAsync(new List<KeyTimeline>(), "tenant");
            _dbContextProvider.Verify(x => x.GetDatabase("tenant"), Times.Never);
        }

        [Fact]
        public async Task BulkSaveKeyTimelinesAsync_WithItems_SetsIdsAndDatesAndInserts()
        {
            _collection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<KeyTimeline>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var timelines = new List<KeyTimeline> {
                new KeyTimeline { ItemId = null, EntityId = "e1", UserId = "u1" },
                new KeyTimeline { ItemId = "id2", EntityId = "e2", UserId = "u2" }
            };

            await _repo.BulkSaveKeyTimelinesAsync(timelines, "tenant");

            timelines[0].ItemId.Should().NotBeNullOrWhiteSpace();
            timelines[0].CreateDate.Should().NotBe(default);
            timelines[1].CreateDate.Should().NotBe(default);

            _collection.Verify(x => x.InsertManyAsync(
                timelines,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BulkSaveKeyTimelinesAsync_PreservesExistingItemId()
        {
            _collection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<KeyTimeline>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var timelines = new List<KeyTimeline> {
                new KeyTimeline { ItemId = "existing-id", EntityId = "e1", UserId = "u1" }
            };

            await _repo.BulkSaveKeyTimelinesAsync(timelines, "tenant");

            timelines[0].ItemId.Should().Be("existing-id");
        }
    }
}
