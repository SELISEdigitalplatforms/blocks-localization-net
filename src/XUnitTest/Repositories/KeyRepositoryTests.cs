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
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace XUnitTest.Repositories
{
    public class KeyRepositoryTests
    {
        private readonly Mock<IDbContextProvider> _dbContextProvider;
        private readonly Mock<IMongoDatabase> _database;
        private readonly Mock<IMongoCollection<BlocksLanguageKey>> _blkCollection;
        private readonly Mock<IMongoCollection<UilmFile>> _uilmFileCollection;
        private readonly Mock<IMongoCollection<BsonDocument>> _bsonCollection;
        private readonly Mock<IMongoCollection<UilmExportedFile>> _exportedFileCollection;
        private readonly KeyRepository _repo;

        public KeyRepositoryTests()
        {
            _dbContextProvider = new Mock<IDbContextProvider>();
            _database = new Mock<IMongoDatabase>();
            _blkCollection = new Mock<IMongoCollection<BlocksLanguageKey>>();
            _uilmFileCollection = new Mock<IMongoCollection<UilmFile>>();
            _bsonCollection = new Mock<IMongoCollection<BsonDocument>>();
            _exportedFileCollection = new Mock<IMongoCollection<UilmExportedFile>>();

            _dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_database.Object);
            _database.Setup(x => x.GetCollection<BlocksLanguageKey>(It.IsAny<string>(), null)).Returns(_blkCollection.Object);
            _database.Setup(x => x.GetCollection<UilmFile>(It.IsAny<string>(), null)).Returns(_uilmFileCollection.Object);
            _database.Setup(x => x.GetCollection<BsonDocument>(It.IsAny<string>(), null)).Returns(_bsonCollection.Object);
            _database.Setup(x => x.GetCollection<UilmExportedFile>(It.IsAny<string>(), null)).Returns(_exportedFileCollection.Object);

            _repo = new KeyRepository(_dbContextProvider.Object);
        }

        [Fact]
        public async Task SaveKeyAsync_UpsertsKey()
        {
            _blkCollection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<BlocksLanguageKey>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var key = new BlocksLanguageKey { ItemId = "k1", KeyName = "k", ModuleId = "m" };
            await _repo.SaveKeyAsync(key);

            _blkCollection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                key,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_CallsDeleteOneAsync()
        {
            _blkCollection.Setup(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<DeleteResult>());

            await _repo.DeleteAsync("item-1");

            _blkCollection.Verify(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksLanguageKey>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveNewUilmFiles_InsertsMany_ReturnsTrue()
        {
            _uilmFileCollection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<UilmFile>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var files = new List<UilmFile>
            {
                new UilmFile { Id = "f1", TenantId = "t", ModuleName = "mod", Language = "en", Content = "c" }
            };

            var result = await _repo.SaveNewUilmFiles(files);
            result.Should().BeTrue();

            _uilmFileCollection.Verify(x => x.InsertManyAsync(
                files,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUilmResourceKeys_CallsBulkWriteAsync()
        {
            _blkCollection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((BulkWriteResult<BlocksLanguageKey>)null!);

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey { ItemId = "k1", KeyName = "key1", ModuleId = "m1" },
                new BlocksLanguageKey { ItemId = "k2", KeyName = "key2", ModuleId = "m1" }
            };

            var result = await _repo.UpdateUilmResourceKeys(keys);
            result.Should().BeNull();

            _blkCollection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUilmResourceKeysForChangeAll_DelegatesToUpdateUilmResourceKeys()
        {
            _blkCollection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((BulkWriteResult<BlocksLanguageKey>)null!);

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey { ItemId = "k1", KeyName = "key1", ModuleId = "m1" }
            };

            var result = await _repo.UpdateUilmResourceKeysForChangeAll(keys);
            result.Should().BeNull();

            _blkCollection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageKey>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InsertUilmResourceKeys_WithTenantId_CallsInsertMany()
        {
            _blkCollection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<BlocksLanguageKey>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey { ItemId = "k1", KeyName = "key1", ModuleId = "m1" }
            };

            await _repo.InsertUilmResourceKeys(keys, "tenant-1");

            _blkCollection.Verify(x => x.InsertManyAsync(
                keys,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InsertUilmResourceKeys_WithoutTenantId_CallsInsertMany()
        {
            _blkCollection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<BlocksLanguageKey>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var keys = new List<BlocksLanguageKey>
            {
                new BlocksLanguageKey { ItemId = "k1", KeyName = "key1", ModuleId = "m1" }
            };

            await _repo.InsertUilmResourceKeys(keys);

            _blkCollection.Verify(x => x.InsertManyAsync(
                keys,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SaveUilmExportedFileAsync_CallsInsertOne()
        {
            _exportedFileCollection.Setup(x => x.InsertOneAsync(
                It.IsAny<UilmExportedFile>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var file = new UilmExportedFile
            {
                FileId = "file-1",
                FileName = "export.json",
                CreateDate = DateTime.UtcNow,
                CreatedBy = "user-1"
            };

            await _repo.SaveUilmExportedFileAsync(file);

            _exportedFileCollection.Verify(x => x.InsertOneAsync(
                file,
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InsertUilmApplications_WithList_CallsInsertMany()
        {
            var moduleCollection = new Mock<IMongoCollection<BlocksLanguageModule>>();
            _database.Setup(x => x.GetCollection<BlocksLanguageModule>(It.IsAny<string>(), null)).Returns(moduleCollection.Object);
            moduleCollection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<BlocksLanguageModule>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "m1", ModuleName = "mod1", Name = "Module1" }
            };

            await _repo.InsertUilmApplications(modules, "tenant-1");

            moduleCollection.Verify(x => x.InsertManyAsync(
                modules,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InsertUilmApplications_WithEnumerable_CallsInsertMany()
        {
            var moduleCollection = new Mock<IMongoCollection<BlocksLanguageModule>>();
            _database.Setup(x => x.GetCollection<BlocksLanguageModule>(It.IsAny<string>(), null)).Returns(moduleCollection.Object);
            moduleCollection.Setup(x => x.InsertManyAsync(
                It.IsAny<IEnumerable<BlocksLanguageModule>>(),
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            IEnumerable<BlocksLanguageModule> modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "m1", ModuleName = "mod1", Name = "Module1" }
            };

            await _repo.InsertUilmApplications(modules);

            moduleCollection.Verify(x => x.InsertManyAsync(
                modules,
                It.IsAny<InsertManyOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateBulkUilmApplications_BulkWritesBsonAndModules()
        {
            _bsonCollection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BsonDocument>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((BulkWriteResult<BsonDocument>)null!);

            var moduleCollection = new Mock<IMongoCollection<BlocksLanguageModule>>();
            _database.Setup(x => x.GetCollection<BlocksLanguageModule>(It.IsAny<string>(), null)).Returns(moduleCollection.Object);
            moduleCollection.Setup(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageModule>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync((BulkWriteResult<BlocksLanguageModule>)null!);

            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "m1", ModuleName = "mod1", Name = "Module1" }
            };

            await _repo.UpdateBulkUilmApplications(modules, "org-1", false, "tenant-1");

            _bsonCollection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BsonDocument>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);

            moduleCollection.Verify(x => x.BulkWriteAsync(
                It.IsAny<IEnumerable<WriteModel<BlocksLanguageModule>>>(),
                It.IsAny<BulkWriteOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
