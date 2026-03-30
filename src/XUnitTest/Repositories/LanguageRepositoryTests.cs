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
    public class LanguageRepositoryTests
    {
        [Fact]
        public async Task SaveAsync_UpsertsLanguage()
        {
            var dbContextProvider = new Mock<IDbContextProvider>();
            var database = new Mock<IMongoDatabase>();
            var collection = new Mock<IMongoCollection<BlocksLanguage>>();
            dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(database.Object);
            database.Setup(x => x.GetCollection<BlocksLanguage>(It.IsAny<string>(), null)).Returns(collection.Object);
            collection.Setup(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguage>>(), It.IsAny<BlocksLanguage>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());
            var repo = new LanguageRepository(dbContextProvider.Object);
            var language = new BlocksLanguage { ItemId = "l1", LanguageName = "English", LanguageCode = "en" };
            await repo.SaveAsync(language);
            collection.Verify(x => x.ReplaceOneAsync(It.IsAny<FilterDefinition<BlocksLanguage>>(), language, It.Is<ReplaceOptions>(o => o.IsUpsert), It.IsAny<CancellationToken>()), Times.Once);
        }
        // Add more tests for coverage
    }
}
