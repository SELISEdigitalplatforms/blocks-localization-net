using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Shared.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using MongoDB.Driver;
using Xunit;

namespace XUnitTest.DomainService.Repositories.Key
{
    public class KeyTimelineRepositoryTests
    {
        private readonly Mock<IDbContextProvider> _dbContextProviderMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IMongoDatabase> _dbMock;
        private readonly Mock<IMongoCollection<KeyTimeline>> _timelineCollectionMock;
        private readonly Mock<IMongoCollection<User>> _userCollectionMock;
        private readonly KeyTimelineRepository _repository;

        public KeyTimelineRepositoryTests()
        {
            _dbContextProviderMock = new Mock<IDbContextProvider>();
            _configurationMock = new Mock<IConfiguration>();
            _dbMock = new Mock<IMongoDatabase>();
            _timelineCollectionMock = new Mock<IMongoCollection<KeyTimeline>>();
            _userCollectionMock = new Mock<IMongoCollection<User>>();

            _dbContextProviderMock.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_dbMock.Object);
            _dbMock.Setup(x => x.GetCollection<KeyTimeline>(It.IsAny<string>(), null)).Returns(_timelineCollectionMock.Object);
            _dbMock.Setup(x => x.GetCollection<User>(It.IsAny<string>(), null)).Returns(_userCollectionMock.Object);

            _repository = new KeyTimelineRepository(_dbContextProviderMock.Object, _configurationMock.Object);
        }

        [Fact]
        public async Task GetKeyTimelineAsync_ReturnsTimelinesWithUserNames()
        {
            // Arrange
            var query = new GetKeyTimelineRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SortProperty = "CreateDate",
                IsDescending = false
            };
            var timelines = new List<KeyTimeline>
            {
                new KeyTimeline { UserId = "user1", CreateDate = DateTime.UtcNow },
                new KeyTimeline { UserId = "user2", CreateDate = DateTime.UtcNow }
            };
            var users = new List<User>
            {
                new User { ItemId = "user1", FirstName = "John", LastName = "Doe" },
                new User { ItemId = "user2", Email = "user2@email.com" }
            };
            var timelineCursorMock = new Mock<IAsyncCursor<KeyTimeline>>();
            timelineCursorMock.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
            timelineCursorMock.Setup(x => x.Current).Returns(timelines);
            _timelineCollectionMock.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(Mock.Of<IFindFluent<KeyTimeline, KeyTimeline>>(f => f.Sort(It.IsAny<SortDefinition<KeyTimeline>>()) == f && f.Skip(It.IsAny<int>()) == f && f.Limit(It.IsAny<int>()) == f && f.ToListAsync(It.IsAny<CancellationToken>()) == Task.FromResult(timelines)));
            _timelineCollectionMock.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(2);

            var userCursorMock = new Mock<IAsyncCursor<User>>();
            userCursorMock.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
            userCursorMock.Setup(x => x.Current).Returns(users);
            _userCollectionMock.Setup(x => x.Find(It.IsAny<FilterDefinition<User>>(), null)).Returns(Mock.Of<IFindFluent<User, User>>(f => f.ToListAsync(It.IsAny<CancellationToken>()) == Task.FromResult(users)));

            _configurationMock.Setup(x => x["RootTenantId"]).Returns("rootTenant");

            // Act
            var result = await _repository.GetKeyTimelineAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Timelines.Should().HaveCount(2);
            result.Timelines[0].UserName.Should().Be("John Doe");
            result.Timelines[1].UserName.Should().Be("user2@email.com");
        }

        [Fact]
        public async Task GetKeyTimelineAsync_WithNoUserIds_ReturnsTimelinesWithoutUserNames()
        {
            // Arrange
            var query = new GetKeyTimelineRequest { PageNumber = 1, PageSize = 10 };
            var timelines = new List<KeyTimeline> { new KeyTimeline { UserId = null } };
            _timelineCollectionMock.Setup(x => x.Find(It.IsAny<FilterDefinition<KeyTimeline>>(), null)).Returns(Mock.Of<IFindFluent<KeyTimeline, KeyTimeline>>(f => f.Sort(It.IsAny<SortDefinition<KeyTimeline>>()) == f && f.Skip(It.IsAny<int>()) == f && f.Limit(It.IsAny<int>()) == f && f.ToListAsync(It.IsAny<CancellationToken>()) == Task.FromResult(timelines)));
            _timelineCollectionMock.Setup(x => x.CountDocumentsAsync(It.IsAny<FilterDefinition<KeyTimeline>>(), null, default)).ReturnsAsync(1);

            // Act
            var result = await _repository.GetKeyTimelineAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Timelines.Should().HaveCount(1);
            result.Timelines[0].UserName.Should().BeNull();
        }
    }
}
