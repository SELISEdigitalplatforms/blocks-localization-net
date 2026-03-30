using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task SaveKeyTimelineAsync_Inserts_WhenNoItemId()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.InsertOneAsync(It.IsAny<KeyTimeline>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var timeline = new KeyTimeline { EntityId = "e", UserId = "u" };
            await repo.SaveKeyTimelineAsync(timeline);
            collection.Verify(x => x.InsertOneAsync(It.IsAny<KeyTimeline>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
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

        [Fact]
        public async Task GetTimelineByItemIdAsync_ReturnsNullIfNotFound()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<KeyTimeline>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(Mock.Of<IAsyncCursor<KeyTimeline>>());
            collection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null).FirstOrDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((KeyTimeline)null);
            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetTimelineByItemIdAsync("notfound");
            result.Should().BeNull();
        }

        // More tests for GetKeyTimelineAsync can be added for full branch coverage
        [Fact]
        public async Task GetKeyTimelineAsync_PopulatesUserName_WithFirstNameAndLastName()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var timelinesCollection = new Mock<IMongoCollection<KeyTimeline>>();
            var usersCollection = new Mock<IMongoCollection<User>>();
            var userCursor = new Mock<IAsyncCursor<User>>();
            var timelineCursor = new Mock<IAsyncCursor<KeyTimeline>>();

            var timeline = new KeyTimeline { ItemId = "t1", UserId = "u1" };
            var user = new User { ItemId = "u1", FirstName = "John", LastName = "Doe" };

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(timelinesCollection.Object);
            database.Setup(x => x.GetCollection<User>("Users", null)).Returns(usersCollection.Object);
            timelinesCollection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(1);
            timelinesCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(timelineCursor.Object);
            timelineCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<KeyTimeline> { timeline });
            configuration.Setup(x => x["RootTenantId"]).Returns("root");
            dbContextProvider.Setup(x => x.GetDatabase("root")).Returns(database.Object);
            usersCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<User>>(), null)).Returns(userCursor.Object);
            userCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<User> { user });

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetKeyTimelineAsync(new GetKeyTimelineRequest());
            result.Timelines[0].UserName.Should().Be("John Doe");
        }

        [Fact]
        public async Task GetKeyTimelineAsync_PopulatesUserName_WithEmailOnly()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var timelinesCollection = new Mock<IMongoCollection<KeyTimeline>>();
            var usersCollection = new Mock<IMongoCollection<User>>();
            var userCursor = new Mock<IAsyncCursor<User>>();
            var timelineCursor = new Mock<IAsyncCursor<KeyTimeline>>();

            var timeline = new KeyTimeline { ItemId = "t1", UserId = "u1" };
            var user = new User { ItemId = "u1", Email = "user@email.com" };

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(timelinesCollection.Object);
            database.Setup(x => x.GetCollection<User>("Users", null)).Returns(usersCollection.Object);
            timelinesCollection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(1);
            timelinesCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(timelineCursor.Object);
            timelineCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<KeyTimeline> { timeline });
            configuration.Setup(x => x["RootTenantId"]).Returns("root");
            dbContextProvider.Setup(x => x.GetDatabase("root")).Returns(database.Object);
            usersCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<User>>(), null)).Returns(userCursor.Object);
            userCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<User> { user });

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetKeyTimelineAsync(new GetKeyTimelineRequest());
            result.Timelines[0].UserName.Should().Be("user@email.com");
        }

        [Fact]
        public async Task GetKeyTimelineAsync_PopulatesUserName_WithUserIdFallback()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var timelinesCollection = new Mock<IMongoCollection<KeyTimeline>>();
            var usersCollection = new Mock<IMongoCollection<User>>();
            var userCursor = new Mock<IAsyncCursor<User>>();
            var timelineCursor = new Mock<IAsyncCursor<KeyTimeline>>();

            var timeline = new KeyTimeline { ItemId = "t1", UserId = "u1" };
            var user = new User { ItemId = "u1" }; // No name, no email

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(timelinesCollection.Object);
            database.Setup(x => x.GetCollection<User>("Users", null)).Returns(usersCollection.Object);
            timelinesCollection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(1);
            timelinesCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(timelineCursor.Object);
            timelineCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<KeyTimeline> { timeline });
            configuration.Setup(x => x["RootTenantId"]).Returns("root");
            dbContextProvider.Setup(x => x.GetDatabase("root")).Returns(database.Object);
            usersCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<User>>(), null)).Returns(userCursor.Object);
            userCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<User> { user });

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetKeyTimelineAsync(new GetKeyTimelineRequest());
            result.Timelines[0].UserName.Should().Be("u1");
        }

        [Fact]
        public async Task GetKeyTimelineAsync_PopulatesUserName_WithUnknownIfNoUser()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var timelinesCollection = new Mock<IMongoCollection<KeyTimeline>>();
            var usersCollection = new Mock<IMongoCollection<User>>();
            var userCursor = new Mock<IAsyncCursor<User>>();
            var timelineCursor = new Mock<IAsyncCursor<KeyTimeline>>();

            var timeline = new KeyTimeline { ItemId = "t1", UserId = "u2" };

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(timelinesCollection.Object);
            database.Setup(x => x.GetCollection<User>("Users", null)).Returns(usersCollection.Object);
            timelinesCollection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(1);
            timelinesCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(timelineCursor.Object);
            timelineCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<KeyTimeline> { timeline });
            configuration.Setup(x => x["RootTenantId"]).Returns("root");
            dbContextProvider.Setup(x => x.GetDatabase("root")).Returns(database.Object);
            usersCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<User>>(), null)).Returns(userCursor.Object);
            userCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<User>()); // No user found

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetKeyTimelineAsync(new GetKeyTimelineRequest());
            result.Timelines[0].UserName.Should().Be("u2");
        }

        [Fact]
        public async Task GetKeyTimelineAsync_EmptyTimelines_ReturnsEmptyList()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var configuration = new Mock<IConfiguration>();
            var database = new Mock<IMongoDatabase>();
            var timelinesCollection = new Mock<IMongoCollection<KeyTimeline>>();
            var timelineCursor = new Mock<IAsyncCursor<KeyTimeline>>();

            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(timelinesCollection.Object);
            timelinesCollection.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(0);
            timelinesCollection.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(timelineCursor.Object);
            timelineCursor.Setup(x => x.ToListAsync(default)).ReturnsAsync(new List<KeyTimeline>());

            var repo = new KeyTimelineRepository(dbContextProvider.Object, configuration.Object);
            var result = await repo.GetKeyTimelineAsync(new GetKeyTimelineRequest());
            result.Timelines.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }
    }
}
