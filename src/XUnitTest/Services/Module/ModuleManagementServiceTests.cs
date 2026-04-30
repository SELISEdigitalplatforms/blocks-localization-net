using DomainService.Repositories;
using DomainService.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BlocksLanguageModule = DomainService.Repositories.BlocksLanguageModule;
using ModuleModel = DomainService.Services.Module;

namespace XUnitTest
{
    public class ModuleManagementServiceTests
    {
        private readonly Mock<ILogger<ModuleManagementService>> _loggerMock;
        private readonly Mock<IModuleRepository> _moduleRepositoryMock;
        private readonly Mock<IValidator<ModuleModel>> _validatorMock;
        private readonly Mock<IKeyManagementService> _keyManagementServiceMock;
        private readonly Mock<IKeyBulkOperationsService> _keyBulkOperationsServiceMock;
        private readonly Mock<IGlossaryRepository> _glossaryRepositoryMock;
        private readonly ModuleManagementService _service;

        public ModuleManagementServiceTests()
        {
            _loggerMock = new Mock<ILogger<ModuleManagementService>>();
            _moduleRepositoryMock = new Mock<IModuleRepository>();
            _validatorMock = new Mock<IValidator<ModuleModel>>();
            _keyManagementServiceMock = new Mock<IKeyManagementService>();
            _keyBulkOperationsServiceMock = new Mock<IKeyBulkOperationsService>();
            _glossaryRepositoryMock = new Mock<IGlossaryRepository>();

            _service = new ModuleManagementService(
                _validatorMock.Object,
                _moduleRepositoryMock.Object,
                _loggerMock.Object,
                new Lazy<IKeyBulkOperationsService>(() => _keyBulkOperationsServiceMock.Object),
                _glossaryRepositoryMock.Object
            );
        }

        [Fact]
        public async Task SaveModuleAsync_ValidModule_ReturnsSuccess()
        {
            // Arrange
            var module = new SaveModuleRequest
            {
                ModuleName = "authentication",
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(module, default))
                .ReturnsAsync(validationResult);

            _moduleRepositoryMock.Setup(r => r.GetByNameAsync(module.ModuleName))
                .ReturnsAsync((BlocksLanguageModule)null);

            _moduleRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<BlocksLanguageModule>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveModuleAsync(module);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _moduleRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<BlocksLanguageModule>()), Times.Once);
        }

        [Fact]
        public async Task SaveModuleAsync_InvalidModule_ReturnsValidationError()
        {
            // Arrange
            var module = new SaveModuleRequest
            {
                ModuleName = "",
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            validationResult.Errors.Add(new FluentValidation.Results.ValidationFailure("ModuleName", "Module name is required."));
            
            _validatorMock.Setup(v => v.ValidateAsync(module, default))
                .ReturnsAsync(validationResult);

            // Act
            var result = await _service.SaveModuleAsync(module);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            _moduleRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<BlocksLanguageModule>()), Times.Never);
        }

        [Fact]
        public async Task SaveModuleAsync_ExistingModule_UpdatesModule()
        {
            // Arrange
            var module = new SaveModuleRequest
            {
                ModuleName = "authentication",
                ProjectKey = "test-project"
            };

            var existingModule = new BlocksLanguageModule
            {
                ItemId = "existing-id",
                ModuleName = "authentication",
                CreateDate = DateTime.UtcNow.AddDays(-1)
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(module, default))
                .ReturnsAsync(validationResult);

            _moduleRepositoryMock.Setup(r => r.GetByNameAsync(module.ModuleName))
                .ReturnsAsync(existingModule);

            _moduleRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<BlocksLanguageModule>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveModuleAsync(module);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _moduleRepositoryMock.Verify(r => r.SaveAsync(It.Is<BlocksLanguageModule>(m => 
                m.ItemId == existingModule.ItemId)), Times.Once);
        }

        [Fact]
        public async Task SaveModuleAsync_ExceptionThrown_ReturnsError()
        {
            // Arrange
            var module = new SaveModuleRequest
            {
                ModuleName = "authentication",
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(module, default))
                .ReturnsAsync(validationResult);

            _moduleRepositoryMock.Setup(r => r.GetByNameAsync(module.ModuleName))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.SaveModuleAsync(module);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Database error");
        }

        [Fact]
        public async Task GetModulesAsync_NoModuleId_ReturnsAllModules()
        {
            // Arrange
            var modules = new List<BlocksLanguageModule>
            {
                new BlocksLanguageModule { ItemId = "1", ModuleName = "auth" },
                new BlocksLanguageModule { ItemId = "2", ModuleName = "common" }
            };

            _moduleRepositoryMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(modules);

            // Act
            var result = await _service.GetModulesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            _moduleRepositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
            _moduleRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetModulesAsync_WithModuleId_ReturnsSpecificModule()
        {
            // Arrange
            var moduleId = "module-id";
            var module = new BlocksLanguageModule
            {
                ItemId = moduleId,
                ModuleName = "authentication"
            };

            _moduleRepositoryMock.Setup(r => r.GetByIdAsync(moduleId))
                .ReturnsAsync(module);

            // Act
            var result = await _service.GetModulesAsync(moduleId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().ItemId.Should().Be(moduleId);
            _moduleRepositoryMock.Verify(r => r.GetByIdAsync(moduleId), Times.Once);
        }

        [Fact]
        public async Task GetModulesAsync_WithModuleId_ModuleNotFound_ReturnsEmptyList()
        {
            // Arrange
            var moduleId = "non-existent-id";

            _moduleRepositoryMock.Setup(r => r.GetByIdAsync(moduleId))
                .ReturnsAsync((BlocksLanguageModule)null);

            // Act
            var result = await _service.GetModulesAsync(moduleId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _moduleRepositoryMock.Verify(r => r.GetByIdAsync(moduleId), Times.Once);
        }

        #region DeleteModuleAsync Tests

        [Fact]
        public async Task DeleteModuleAsync_WithCascadeDelete_DeletesKeysAndModule()
        {
            // Arrange
            var request = new DeleteModuleRequest
            {
                ItemId = "module-id",
                TargetModuleId = null,
                ProjectKey = "project-1"
            };

            _keyBulkOperationsServiceMock
                .Setup(x => x.BulkDeleteByModuleAsync(request.ItemId, request.ProjectKey))
                .Returns(Task.CompletedTask);

            _moduleRepositoryMock
                .Setup(x => x.DeleteAsync(request.ItemId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteModuleAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            _keyBulkOperationsServiceMock.Verify(x => x.BulkDeleteByModuleAsync(request.ItemId, request.ProjectKey), Times.Once);
            _keyBulkOperationsServiceMock.Verify(x => x.BulkMoveByModuleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _moduleRepositoryMock.Verify(x => x.DeleteAsync(request.ItemId), Times.Once);
        }

        [Fact]
        public async Task DeleteModuleAsync_WithTargetModule_MovesKeysAndDeletesModule()
        {
            // Arrange
            var request = new DeleteModuleRequest
            {
                ItemId = "module-id",
                TargetModuleId = "target-module-id",
                ProjectKey = "project-1"
            };

            _keyBulkOperationsServiceMock
                .Setup(x => x.BulkMoveByModuleAsync(request.ItemId, request.TargetModuleId, request.ProjectKey))
                .Returns(Task.CompletedTask);

            _moduleRepositoryMock
                .Setup(x => x.DeleteAsync(request.ItemId))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteModuleAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            _keyBulkOperationsServiceMock.Verify(x => x.BulkMoveByModuleAsync(request.ItemId, request.TargetModuleId, request.ProjectKey), Times.Once);
            _keyBulkOperationsServiceMock.Verify(x => x.BulkDeleteByModuleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _moduleRepositoryMock.Verify(x => x.DeleteAsync(request.ItemId), Times.Once);
        }

        [Fact]
        public async Task DeleteModuleAsync_WhenExceptionThrown_ReturnsFailure()
        {
            // Arrange
            var request = new DeleteModuleRequest
            {
                ItemId = "module-id",
                ProjectKey = "project-1"
            };

            _keyBulkOperationsServiceMock
                .Setup(x => x.BulkDeleteByModuleAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _service.DeleteModuleAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainKey("Error");
        }

        #endregion

        #region TagGlossaryAsync Tests

        [Fact]
        public async Task TagGlossaryAsync_WithNewGlossaryIds_TagsGlossaries()
        {
            // Arrange
            var request = new TagGlossaryRequest
            {
                ModuleId = "module-id",
                GlossaryIds = new List<string> { "glossary-1", "glossary-2" },
                ProjectKey = "project-1"
            };

            _glossaryRepositoryMock
                .Setup(x => x.GetByModuleIdAsync(It.IsAny<string>(), request.ModuleId))
                .ReturnsAsync(new List<Glossary>());

            _glossaryRepositoryMock
                .Setup(x => x.GetByIdsAsync(request.GlossaryIds))
                .ReturnsAsync(new List<Glossary>
                {
                    new Glossary { ItemId = "glossary-1", Name = "Glossary One", ModuleIds = new List<string>() },
                    new Glossary { ItemId = "glossary-2", Name = "Glossary Two", ModuleIds = new List<string>() }
                });

            _glossaryRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<BlocksGlossary>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.TagGlossaryAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            _glossaryRepositoryMock.Verify(x => x.SaveAsync(It.IsAny<BlocksGlossary>()), Times.Exactly(2));
        }

        [Fact]
        public async Task TagGlossaryAsync_RemovesUntaggedGlossaries()
        {
            // Arrange
            var request = new TagGlossaryRequest
            {
                ModuleId = "module-id",
                GlossaryIds = new List<string>(),
                ProjectKey = "project-1"
            };

            _glossaryRepositoryMock
                .Setup(x => x.GetByModuleIdAsync(It.IsAny<string>(), request.ModuleId))
                .ReturnsAsync(new List<Glossary>
                {
                    new Glossary { ItemId = "old-glossary", Name = "Old", ModuleIds = new List<string> { request.ModuleId } }
                });

            _glossaryRepositoryMock
                .Setup(x => x.SaveAsync(It.IsAny<BlocksGlossary>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.TagGlossaryAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            _glossaryRepositoryMock.Verify(x => x.SaveAsync(It.IsAny<BlocksGlossary>()), Times.Once);
        }

        [Fact]
        public async Task TagGlossaryAsync_WhenExceptionThrown_ReturnsFailure()
        {
            // Arrange
            var request = new TagGlossaryRequest
            {
                ModuleId = "module-id",
                GlossaryIds = new List<string> { "g1" },
                ProjectKey = "project-1"
            };

            _glossaryRepositoryMock
                .Setup(x => x.GetByModuleIdAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Repository error"));

            // Act
            var result = await _service.TagGlossaryAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainKey("Error");
        }

        #endregion
    }
}

