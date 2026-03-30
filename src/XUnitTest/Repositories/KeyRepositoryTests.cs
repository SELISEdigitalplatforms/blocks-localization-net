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
using Xunit;

namespace XUnitTest.Repositories
{
    public class KeyRepositoryTests
    {
        [Fact]
        public async Task SaveKeyAsync_UpsertsKey()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguageKey>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguageKey>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguageKey>>(), It.IsAny<BlocksLanguageKey>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());
            var repo = new KeyRepository(dbContextProvider.Object);
            var key = new BlocksLanguageKey { ItemId = "k1", KeyName = "k", ModuleId = "m" };
            await repo.SaveKeyAsync(key);
            collection.Verify(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguageKey>>(), key, It.Is<ReplaceOptions>(o => o.IsUpsert), It.IsAny<CancellationToken>()), Times.Once);
        }
        // Add more tests for coverage
    }
}
