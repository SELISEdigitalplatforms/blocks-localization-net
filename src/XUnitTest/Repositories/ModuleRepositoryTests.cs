using System;
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
    public class ModuleRepositoryTests
    {
        [Fact]
        public async Task SaveAsync_UpsertsModule()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguageModule>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguageModule>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguageModule>>(), It.IsAny<BlocksLanguageModule>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());
            var repo = new ModuleRepository(dbContextProvider.Object);
            var module = new BlocksLanguageModule { ItemId = "m1", ModuleName = "mod", Name = "Module" };
            await repo.SaveAsync(module);
            collection.Verify(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguageModule>>(), module, It.Is<ReplaceOptions>(o => o.IsUpsert), It.IsAny<CancellationToken>()), Times.Once);
        }
        // Add more tests for coverage
    }
}
