using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using Xunit;
using XUnitTest.Shared;

namespace XUnitTest.Repositories
{
    public class GlossaryRepositoryTests
    {
        private readonly Mock<IDbContextProvider> _dbContextProvider;
        private readonly Mock<IMongoDatabase> _database;
        private readonly Mock<IMongoCollection<Glossary>> _glossaryCollection;
        private readonly Mock<IMongoCollection<BlocksGlossary>> _blocksGlossaryCollection;
        private readonly GlossaryRepository _repo;

        public GlossaryRepositoryTests()
        {
            _dbContextProvider = new Mock<IDbContextProvider>();
            _database = new Mock<IMongoDatabase>();
            _glossaryCollection = new Mock<IMongoCollection<Glossary>>();
            _blocksGlossaryCollection = new Mock<IMongoCollection<BlocksGlossary>>();

            _dbContextProvider.Setup(x => x.GetDatabase(It.IsAny<string>())).Returns(_database.Object);
            _database.Setup(x => x.GetCollection<Glossary>(It.IsAny<string>(), null)).Returns(_glossaryCollection.Object);
            _database.Setup(x => x.GetCollection<BlocksGlossary>(It.IsAny<string>(), null)).Returns(_blocksGlossaryCollection.Object);

            _repo = new GlossaryRepository(_dbContextProvider.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task GetAllAsync_WithoutSearchText_ReturnsAllAndCount()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API" },
                new Glossary { ItemId = "g2", Name = "SDK" }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 2);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest { PageNumber = 0, PageSize = 10 });

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(2);
        }

        [Fact]
        public async Task GetAllAsync_WithSearchText_AppliesRegexFilterBranch()
        {
            MockCursorHelper.SetupFindAsync(_glossaryCollection,
                new List<Glossary> { new Glossary { ItemId = "g1", Name = "API" } });
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 1);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                SearchText = "AP"
            });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAllAsync_WithWhitespaceSearchText_SkipsRegexFilterBranch()
        {
            MockCursorHelper.SetupFindAsync(_glossaryCollection, new List<Glossary>());
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 0);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                SearchText = "   "
            });

            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task GetAllAsync_EmptyResult_ReturnsEmptyList()
        {
            MockCursorHelper.SetupFindAsyncEmpty(_glossaryCollection);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 0);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest { PageNumber = 2, PageSize = 50 });

            result.Items.Should().BeEmpty();
            result.TotalCount.Should().Be(0);
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task GetByIdAsync_ReturnsGlossary_WhenFound()
        {
            MockCursorHelper.SetupFindAsync(_glossaryCollection,
                new List<Glossary> { new Glossary { ItemId = "g1", Name = "API" } });

            var result = await _repo.GetByIdAsync("g1");

            result.Should().NotBeNull();
            result.ItemId.Should().Be("g1");
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            MockCursorHelper.SetupFindAsyncEmpty(_glossaryCollection);

            var result = await _repo.GetByIdAsync("missing");

            result.Should().BeNull();
        }

        #endregion

        #region GetByIdsAsync

        [Fact]
        public async Task GetByIdsAsync_WithNullList_ReturnsEmptyWithoutHittingDb()
        {
            var result = await _repo.GetByIdsAsync(null!);

            result.Should().NotBeNull().And.BeEmpty();
            _dbContextProvider.Verify(x => x.GetDatabase(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdsAsync_WithEmptyList_ReturnsEmptyWithoutHittingDb()
        {
            var result = await _repo.GetByIdsAsync(new List<string>());

            result.Should().BeEmpty();
            _dbContextProvider.Verify(x => x.GetDatabase(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetByIdsAsync_ReturnsMatchingGlossaries()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API" },
                new Glossary { ItemId = "g2", Name = "SDK" }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);

            var result = await _repo.GetByIdsAsync(new List<string> { "g1", "g2" });

            result.Should().HaveCount(2);
        }

        #endregion

        #region SaveAsync

        [Fact]
        public async Task SaveAsync_UpsertsGlossary()
        {
            _blocksGlossaryCollection.Setup(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksGlossary>>(),
                It.IsAny<BlocksGlossary>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());

            var glossary = new BlocksGlossary { ItemId = "g1", Name = "API" };

            await _repo.SaveAsync(glossary);

            _blocksGlossaryCollection.Verify(x => x.ReplaceOneAsync(
                It.IsAny<FilterDefinition<BlocksGlossary>>(),
                glossary,
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetGlobalAsync

        [Fact]
        public async Task GetGlobalAsync_ReturnsGlobalGlossaries()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API", IsGlobal = true }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);

            var result = await _repo.GetGlobalAsync("proj-1");

            result.Should().HaveCount(1);
            result[0].IsGlobal.Should().BeTrue();
        }

        [Fact]
        public async Task GetGlobalAsync_ReturnsEmptyWhenNoGlobalGlossaries()
        {
            MockCursorHelper.SetupFindAsyncEmpty(_glossaryCollection);

            var result = await _repo.GetGlobalAsync("proj-1");

            result.Should().BeEmpty();
        }

        #endregion

        #region GetByModuleIdAsync

        [Fact]
        public async Task GetByModuleIdAsync_ReturnsGlossariesWithMatchingModule()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API", ModuleIds = new List<string> { "mod-1" } }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);

            var result = await _repo.GetByModuleIdAsync("proj-1", "mod-1");

            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetByModuleIdAsync_ReturnsEmptyWhenNoMatch()
        {
            MockCursorHelper.SetupFindAsyncEmpty(_glossaryCollection);

            var result = await _repo.GetByModuleIdAsync("proj-1", "non-existing");

            result.Should().BeEmpty();
        }

        #endregion

        #region GetAllAsync — IsGlobal and ModuleId filters

        [Fact]
        public async Task GetAllAsync_WithIsGlobalTrue_AppliesIsGlobalFilter()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API", IsGlobal = true }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 1);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                IsGlobal = true
            });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAllAsync_WithIsGlobalFalse_AppliesIsGlobalFilter()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g2", Name = "SDK", IsGlobal = false }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 1);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                IsGlobal = false
            });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAllAsync_WithModuleId_AppliesModuleIdFilter()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API", ModuleIds = new List<string> { "mod-1" } }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 1);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                ModuleId = "mod-1"
            });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAllAsync_WithWhitespaceModuleId_SkipsModuleIdFilter()
        {
            MockCursorHelper.SetupFindAsync(_glossaryCollection, new List<Glossary>());
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 0);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                ModuleId = "   "
            });

            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_WithIsGlobalAndModuleId_AppliesBothFilters()
        {
            var items = new List<Glossary>
            {
                new Glossary { ItemId = "g1", Name = "API", IsGlobal = true, ModuleIds = new List<string> { "mod-1" } }
            };
            MockCursorHelper.SetupFindAsync(_glossaryCollection, items);
            MockCursorHelper.SetupCountDocuments(_glossaryCollection, 1);

            var result = await _repo.GetAllAsync(new GetGlossariesRequest
            {
                PageNumber = 0,
                PageSize = 10,
                IsGlobal = true,
                ModuleId = "mod-1"
            });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_CallsDeleteOneAsync()
        {
            _blocksGlossaryCollection.Setup(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksGlossary>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<DeleteResult>());

            await _repo.DeleteAsync("g1");

            _blocksGlossaryCollection.Verify(x => x.DeleteOneAsync(
                It.IsAny<FilterDefinition<BlocksGlossary>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
