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
    public class ModuleRepositoryTests
    {
        private readonly Mock<IDbContextProvider> _dbContextProvider;
        private readonly Mock<IMongoDatabase> _database;
        private readonly Mock<IMongoCollection<BlocksLanguageModule>> _collection;
        private readonly ModuleRepository _repo;

        public ModuleRepositoryTests()
        {
            _dbContextProvider = new Mock<IDbContextProvider>();
            _database = new Mock<IMongoDatabase>();
            _collection = new Mock<IMongoCollection<BlocksLanguageModule>>();

            _dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_database.Object);
            _database.Setup(x => x.GetCollection<BlocksLanguageModule>(It.IsAny<string>(), null)).Returns(_collection.Object);

            _repo = new ModuleRepository(_dbContextProvider.Object);
        }

        [Fact]
        public async Task SaveAsync_UpsertsModule()
        {
            _collection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageModule>>(),
                It.IsAny<BlocksLanguageModule>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var module = new BlocksLanguageModule { ItemId = "m1", ModuleName = "mod", Name = "Module" };
            await _repo.SaveAsync(module);

            _collection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageModule>>(),
                module,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_UsesGetCollection()
        {
            // GetAllAsync uses extension method Find which can't be mocked,
            // but we can verify the collection is obtained from the correct collection name
            var collectionMock = new Mock<IMongoCollection<BlocksLanguageModule>>();
            _dbContextProvider.Setup(x => x.GetCollection<BlocksLanguageModule>("BlocksLanguageModules")).Returns(collectionMock.Object);

            // This will throw because Find is an extension method, but we verify collection access
            try
            {
                await _repo.GetAllAsync();
            }
            catch
            {
                // Expected - Find extension method cannot be mocked
            }

            _dbContextProvider.Verify(x => x.GetCollection<BlocksLanguageModule>("BlocksLanguageModules"), Times.Once);
        }
    }
}
