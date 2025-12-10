using Blocks.Genesis;
using DomainService.Repositories;
using DomainService.Services;
using DomainService.Services.HelperService;
using DomainService.Shared;
using DomainService.Shared.Events;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using StorageDriver;
using Xunit;
using BlocksLanguageKey = DomainService.Repositories.BlocksLanguageKey;
using KeyTimeline = DomainService.Services.KeyTimeline;

namespace XUnitTest
{
    public class KeyManagementServiceTests
    {
        private readonly Mock<ILogger<KeyManagementService>> _loggerMock;
        private readonly Mock<IKeyRepository> _keyRepositoryMock;
        private readonly Mock<IKeyTimelineRepository> _keyTimelineRepositoryMock;
        private readonly Mock<IValidator<Key>> _validatorMock;
        private readonly Mock<ILanguageManagementService> _languageManagementServiceMock;
        private readonly Mock<IModuleManagementService> _moduleManagementServiceMock;
        private readonly Mock<IMessageClient> _messageClientMock;
        private readonly Mock<IAssistantService> _assistantServiceMock;
        private readonly Mock<IStorageDriverService> _storageDriverServiceMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly StorageHelper _storageHelper;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly KeyManagementService _service;

        public KeyManagementServiceTests()
        {
            _loggerMock = new Mock<ILogger<KeyManagementService>>();
            _keyRepositoryMock = new Mock<IKeyRepository>();
            _keyTimelineRepositoryMock = new Mock<IKeyTimelineRepository>();
            _validatorMock = new Mock<IValidator<Key>>();
            _languageManagementServiceMock = new Mock<ILanguageManagementService>();
            _moduleManagementServiceMock = new Mock<IModuleManagementService>();
            _messageClientMock = new Mock<IMessageClient>();
            _assistantServiceMock = new Mock<IAssistantService>();
            _storageDriverServiceMock = new Mock<IStorageDriverService>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            var storageLoggerMock = new Mock<ILogger<StorageHelper>>();
            _storageHelper = new StorageHelper(storageLoggerMock.Object, _storageDriverServiceMock.Object);
            _notificationServiceMock = new Mock<INotificationService>();

            _service = new KeyManagementService(
                _keyRepositoryMock.Object,
                _keyTimelineRepositoryMock.Object,
                _validatorMock.Object,
                _loggerMock.Object,
                _languageManagementServiceMock.Object,
                _moduleManagementServiceMock.Object,
                _messageClientMock.Object,
                _assistantServiceMock.Object,
                _storageDriverServiceMock.Object,
                _storageHelper,
                _serviceProviderMock.Object,
                _notificationServiceMock.Object
            );
        }

        [Fact]
        public async Task SaveKeyAsync_ValidKey_ReturnsSuccess()
        {
            // Arrange
            var key = new Key
            {
                KeyName = "welcome.message",
                ModuleId = "auth-module",
                Resources = new[]
                {
                    new Resource { Culture = "en-US", Value = "Welcome" }
                },
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(key, default))
                .ReturnsAsync(validationResult);

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(key.KeyName, key.ModuleId))
                .ReturnsAsync((BlocksLanguageKey)null);

            _keyRepositoryMock.Setup(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveKeyAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _keyRepositoryMock.Verify(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()), Times.Once);
        }

        [Fact]
        public async Task SaveKeyAsync_InvalidKey_ReturnsValidationError()
        {
            // Arrange
            var key = new Key
            {
                KeyName = "",
                ModuleId = "auth-module",
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            validationResult.Errors.Add(new FluentValidation.Results.ValidationFailure("KeyName", "KeyName is required."));
            
            _validatorMock.Setup(v => v.ValidateAsync(key, default))
                .ReturnsAsync(validationResult);

            // Act
            var result = await _service.SaveKeyAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            _keyRepositoryMock.Verify(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()), Times.Never);
        }

        [Fact]
        public async Task SaveKeyAsync_ExistingKey_UpdatesKey()
        {
            // Arrange
            var key = new Key
            {
                KeyName = "welcome.message",
                ModuleId = "auth-module",
                Resources = new[]
                {
                    new Resource { Culture = "en-US", Value = "Welcome Updated" }
                },
                ProjectKey = "test-project"
            };

            var existingKey = new BlocksLanguageKey
            {
                ItemId = "existing-id",
                KeyName = "welcome.message",
                ModuleId = "auth-module",
                CreateDate = DateTime.UtcNow.AddDays(-1)
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(key, default))
                .ReturnsAsync(validationResult);

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(key.KeyName, key.ModuleId))
                .ReturnsAsync(existingKey);

            _keyRepositoryMock.Setup(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveKeyAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _keyRepositoryMock.Verify(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()), Times.Once);
        }

        [Fact]
        public async Task SaveKeyAsync_WithShouldPublish_TriggersUilmGeneration()
        {
            // Arrange
            var key = new Key
            {
                KeyName = "welcome.message",
                ModuleId = "auth-module",
                ItemId = "key-id",
                ShouldPublish = true,
                Resources = new[]
                {
                    new Resource { Culture = "en-US", Value = "Welcome" }
                },
                ProjectKey = "test-project"
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(key, default))
                .ReturnsAsync(validationResult);

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(key.KeyName, key.ModuleId))
                .ReturnsAsync((BlocksLanguageKey)null);

            _keyRepositoryMock.Setup(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()))
                .Returns(Task.CompletedTask);

            _messageClientMock.Setup(m => m.SendToConsumerAsync(It.IsAny<ConsumerMessage<GenerateUilmFilesEvent>>()))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveKeyAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _messageClientMock.Verify(m => m.SendToConsumerAsync(It.IsAny<ConsumerMessage<GenerateUilmFilesEvent>>()), Times.Once);
        }

        [Fact]
        public async Task SaveKeysAsync_MultipleKeys_ProcessesAll()
        {
            // Arrange
            var keys = new List<Key>
            {
                new Key
                {
                    KeyName = "key1",
                    ModuleId = "module1",
                    Resources = new[] { new Resource { Culture = "en-US", Value = "Value1" } },
                    ProjectKey = "test-project"
                },
                new Key
                {
                    KeyName = "key2",
                    ModuleId = "module1",
                    Resources = new[] { new Resource { Culture = "en-US", Value = "Value2" } },
                    ProjectKey = "test-project"
                }
            };

            var validationResult = new FluentValidation.Results.ValidationResult();
            _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<Key>(), default))
                .ReturnsAsync(validationResult);

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((BlocksLanguageKey)null);

            _keyRepositoryMock.Setup(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveKeysAsync(keys);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _keyRepositoryMock.Verify(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SaveKeysAsync_EmptyList_ReturnsError()
        {
            // Arrange
            var keys = new List<Key>();

            // Act
            var result = await _service.SaveKeysAsync(keys);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("cannot be null or empty");
        }

        [Fact]
        public async Task SaveKeysAsync_SomeKeysInvalid_ContinuesProcessing()
        {
            // Arrange
            var keys = new List<Key>
            {
                new Key
                {
                    KeyName = "valid-key",
                    ModuleId = "module1",
                    Resources = new[] { new Resource { Culture = "en-US", Value = "Value" } },
                    ProjectKey = "test-project"
                },
                new Key
                {
                    KeyName = "",
                    ModuleId = "module1",
                    ProjectKey = "test-project"
                }
            };

            var validResult = new FluentValidation.Results.ValidationResult();
            var invalidResult = new FluentValidation.Results.ValidationResult();
            invalidResult.Errors.Add(new FluentValidation.Results.ValidationFailure("KeyName", "KeyName is required."));

            _validatorMock.Setup(v => v.ValidateAsync(keys[0], default))
                .ReturnsAsync(validResult);
            _validatorMock.Setup(v => v.ValidateAsync(keys[1], default))
                .ReturnsAsync(invalidResult);

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(keys[0].KeyName, keys[0].ModuleId))
                .ReturnsAsync((BlocksLanguageKey)null);

            _keyRepositoryMock.Setup(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.SaveKeysAsync(keys);

            // Assert
            result.Should().NotBeNull();
            _keyRepositoryMock.Verify(r => r.SaveKeyAsync(It.IsAny<BlocksLanguageKey>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_KeyExists_ReturnsKey()
        {
            // Arrange
            var request = new GetKeyRequest { ItemId = "key-id" };
            var key = new Key
            {
                ItemId = "key-id",
                KeyName = "welcome.message",
                ModuleId = "auth-module"
            };

            _keyRepositoryMock.Setup(r => r.GetByIdAsync(request.ItemId))
                .ReturnsAsync(key);

            // Act
            var result = await _service.GetAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.ItemId.Should().Be("key-id");
            _keyRepositoryMock.Verify(r => r.GetByIdAsync(request.ItemId), Times.Once);
        }

        [Fact]
        public async Task GetAsync_KeyNotFound_ReturnsNull()
        {
            // Arrange
            var request = new GetKeyRequest { ItemId = "non-existent" };

            _keyRepositoryMock.Setup(r => r.GetByIdAsync(request.ItemId))
                .ReturnsAsync((Key)null);

            // Act
            var result = await _service.GetAsync(request);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_KeyExists_ReturnsSuccess()
        {
            // Arrange
            var request = new DeleteKeyRequest { ItemId = "key-id" };
            var repoKey = new BlocksLanguageKey
            {
                ItemId = "key-id",
                KeyName = "welcome.message",
                ModuleId = "auth-module"
            };

            _keyRepositoryMock.Setup(r => r.GetByIdAsync(request.ItemId))
                .ReturnsAsync(new Key { ItemId = "key-id", KeyName = "welcome.message", ModuleId = "auth-module" });

            _keyRepositoryMock.Setup(r => r.GetKeyByNameAsync(repoKey.KeyName, repoKey.ModuleId))
                .ReturnsAsync(repoKey);

            _keyRepositoryMock.Setup(r => r.DeleteAsync(request.ItemId))
                .Returns(Task.CompletedTask);

            _keyTimelineRepositoryMock.Setup(r => r.SaveKeyTimelineAsync(It.IsAny<KeyTimeline>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.DeleteAsysnc(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            _keyRepositoryMock.Verify(r => r.DeleteAsync(request.ItemId), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_KeyNotFound_ReturnsError()
        {
            // Arrange
            var request = new DeleteKeyRequest { ItemId = "non-existent" };

            _keyRepositoryMock.Setup(r => r.GetByIdAsync(request.ItemId))
                .ReturnsAsync((Key)null);

            // Act
            var result = await _service.DeleteAsysnc(request);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().ContainKey("ItemId");
            _keyRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetKeysAsync_ReturnsQueryResponse()
        {
            // Arrange
            var request = new GetKeysRequest { ProjectKey = "test-project" };
            var response = new GetKeysQueryResponse
            {
                Keys = new List<Key>(),
                TotalCount = 0
            };

            _keyRepositoryMock.Setup(r => r.GetAllKeysAsync(request))
                .ReturnsAsync(response);

            // Act
            var result = await _service.GetKeysAsync(request);

            // Assert
            result.Should().NotBeNull();
            _keyRepositoryMock.Verify(r => r.GetAllKeysAsync(request), Times.Once);
        }

        [Fact]
        public async Task GetKeyTimelineAsync_ReturnsTimelineResponse()
        {
            // Arrange
            var request = new GetKeyTimelineRequest { ProjectKey = "test-project" };
            var response = new GetKeyTimelineQueryResponse
            {
                Timelines = new List<KeyTimeline>(),
                TotalCount = 0
            };

            _keyTimelineRepositoryMock.Setup(r => r.GetKeyTimelineAsync(request))
                .ReturnsAsync(response);

            // Act
            var result = await _service.GetKeyTimelineAsync(request);

            // Assert
            result.Should().NotBeNull();
            _keyTimelineRepositoryMock.Verify(r => r.GetKeyTimelineAsync(request), Times.Once);
        }
    }
}

