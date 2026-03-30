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
        private readonly Mock<IDbContextProvider> _dbContextProvider;
        private readonly Mock<IMongoDatabase> _database;
        private readonly Mock<IMongoCollection<BlocksLanguage>> _collection;
        private readonly LanguageRepository _repo;

        public LanguageRepositoryTests()
        {
            _dbContextProvider = new Mock<IDbContextProvider>();
            _database = new Mock<IMongoDatabase>();
            _collection = new Mock<IMongoCollection<BlocksLanguage>>();

            _dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_database.Object);
            _database.Setup(x => x.GetCollection<BlocksLanguage>(It.IsAny<string>(), null)).Returns(_collection.Object);

            _repo = new LanguageRepository(_dbContextProvider.Object);
        }

        [Fact]
        public async Task SaveAsync_UpsertsLanguage()
        {
            _collection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                It.IsAny<BlocksLanguage>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var language = new BlocksLanguage { ItemId = "l1", LanguageName = "English", LanguageCode = "en" };
            await _repo.SaveAsync(language);

            _collection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                language,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_CallsDeleteOneAsync()
        {
            _collection.Setup(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<DeleteResult>());

            await _repo.DeleteAsync("English");

            _collection.Verify(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveDefault_CallsUpdateManyAsync()
        {
            _collection.Setup(x => x.UpdateManyAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                It.IsAny<UpdateDefinition<BlocksLanguage>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<UpdateResult>());

            var language = new BlocksLanguage { ItemId = "l1", LanguageName = "English", LanguageCode = "en" };
            await _repo.RemoveDefault(language);

            _collection.Verify(x => x.UpdateManyAsync(
                It.IsAny<FilterDefinition<BlocksLanguage>>(),
                It.IsAny<UpdateDefinition<BlocksLanguage>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
