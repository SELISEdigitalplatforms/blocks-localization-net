using DomainService.Repositories;
using DomainService.Services.HelperService;
using DomainService.Shared;
using DomainService.Shared.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace XUnitTest
{
    public class WebHookServiceTests
    {
        private readonly Mock<IBlocksWebhookRepository> _blocksWebhookRepository;
        private readonly Mock<IHttpHelperServices> _httpHelperServices;
        private readonly Mock<ILogger<WebHookService>> _logger;
        private readonly WebHookService _service;

        public WebHookServiceTests()
        {
            _blocksWebhookRepository = new Mock<IBlocksWebhookRepository>();
            _httpHelperServices = new Mock<IHttpHelperServices>();
            _logger = new Mock<ILogger<WebHookService>>();
            _service = new WebHookService(_blocksWebhookRepository.Object, _httpHelperServices.Object, _logger.Object);
        }

        [Fact]
        public async Task CallWebhook_WhenNoWebhookConfigured_ReturnsTrueAndSkipsHttp()
        {
            // Arrange
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync((BlocksWebhook)null!);

            // Act
            var result = await _service.CallWebhook(new { ok = true });

            // Assert
            result.Should().BeTrue();
            _httpHelperServices.Verify(h => h.MakeHttpRequestForWebhook(It.IsAny<object>(), It.IsAny<BlocksWebhook>()), Times.Never);
        }

        [Fact]
        public async Task CallWebhook_WhenWebhookDisabled_ReturnsTrueAndSkipsHttp()
        {
            // Arrange
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                IsDisabled = true,
                ProjectKey = "proj"
            };
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync(webhook);

            // Act
            var result = await _service.CallWebhook(new { ok = true });

            // Assert
            result.Should().BeTrue();
            _httpHelperServices.Verify(h => h.MakeHttpRequestForWebhook(It.IsAny<object>(), It.IsAny<BlocksWebhook>()), Times.Never);
        }

        [Fact]
        public async Task CallWebhook_WhenWebhookEnabled_ForwardsToHttpHelper()
        {
            // Arrange
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                IsDisabled = false,
                ProjectKey = "proj"
            };
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync(webhook);
            _httpHelperServices
                .Setup(h => h.MakeHttpRequestForWebhook(It.IsAny<object>(), webhook))
                .ReturnsAsync(true);

            var payload = new { ok = true };

            // Act
            var result = await _service.CallWebhook(payload);

            // Assert
            result.Should().BeTrue();
            _httpHelperServices.Verify(h => h.MakeHttpRequestForWebhook(payload, webhook), Times.Once);
        }

        [Fact]
        public async Task SaveWebhookAsync_WhenSaveSucceeds_ReturnsSuccess()
        {
            // Arrange
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                ProjectKey = "proj"
            };

            _blocksWebhookRepository.Setup(r => r.SaveAsync(webhook)).Returns(Task.CompletedTask);

            // Act
            ApiResponse response = await _service.SaveWebhookAsync(webhook);

            // Assert
            response.Success.Should().BeTrue();
            response.ErrorMessage.Should().BeNull();
            _blocksWebhookRepository.Verify(r => r.SaveAsync(webhook), Times.Once);
        }

        [Fact]
        public async Task SaveWebhookAsync_WhenRepositoryThrows_ReturnsErrorResponse()
        {
            // Arrange
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                ProjectKey = "proj"
            };
            var exception = new Exception("database unavailable");

            _blocksWebhookRepository
                .Setup(r => r.SaveAsync(webhook))
                .ThrowsAsync(exception);

            // Act
            ApiResponse response = await _service.SaveWebhookAsync(webhook);

            // Assert
            response.Success.Should().BeFalse();
            response.ErrorMessage.Should().Be(exception.Message);
            _blocksWebhookRepository.Verify(r => r.SaveAsync(webhook), Times.Once);
            _httpHelperServices.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CallWebhook_WhenHttpRequestFails_ReturnsFalse()
        {
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                IsDisabled = false,
                ProjectKey = "proj"
            };
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync(webhook);
            _httpHelperServices
                .Setup(h => h.MakeHttpRequestForWebhook(It.IsAny<object>(), webhook))
                .ReturnsAsync(false);

            var result = await _service.CallWebhook(new { ok = true });

            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetWebhookAsync_WhenWebhookExists_ReturnsWebhook()
        {
            // Arrange
            var webhook = new BlocksWebhook
            {
                Url = "https://callback.test/webhook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Signature", Secret = "secret" },
                IsDisabled = false,
                ProjectKey = "proj"
            };
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync(webhook);

            // Act
            var result = await _service.GetWebhookAsync();

            // Assert
            result.Should().NotBeNull();
            result!.Url.Should().Be(webhook.Url);
            _blocksWebhookRepository.Verify(r => r.GetAsync(), Times.Once);
        }

        [Fact]
        public async Task GetWebhookAsync_WhenNotConfigured_ReturnsNull()
        {
            // Arrange
            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync((BlocksWebhook?)null);

            // Act
            var result = await _service.GetWebhookAsync();

            // Assert
            result.Should().BeNull();
            _blocksWebhookRepository.Verify(r => r.GetAsync(), Times.Once);
        }

        [Fact]
        public async Task SaveWebhookAsync_WhenExistingDocumentFound_ReusesItemId()
        {
            // Arrange
            var existingId = "existing-doc-id";
            var existing = new BlocksWebhook
            {
                ItemId = existingId,
                Url = "https://old.example.com/hook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-Old", Secret = "old-secret" },
                ProjectKey = "proj"
            };

            var newWebhook = new BlocksWebhook
            {
                Url = "https://new.example.com/hook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-New", Secret = "new-secret" },
                ProjectKey = "proj"
            };

            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync(existing);
            _blocksWebhookRepository.Setup(r => r.SaveAsync(It.IsAny<BlocksWebhook>())).Returns(Task.CompletedTask);

            // Act
            var response = await _service.SaveWebhookAsync(newWebhook);

            // Assert
            response.Success.Should().BeTrue();
            _blocksWebhookRepository.Verify(r => r.SaveAsync(It.Is<BlocksWebhook>(w => w.ItemId == existingId)), Times.Once);
        }

        [Fact]
        public async Task SaveWebhookAsync_WhenNoExistingDocument_UsesNewItemId()
        {
            // Arrange
            var newWebhook = new BlocksWebhook
            {
                Url = "https://new.example.com/hook",
                ContentType = "application/json",
                BlocksWebhookSecret = new BlocksWebhookSecret { HeaderKey = "X-New", Secret = "new-secret" },
                ProjectKey = "proj"
            };
            var originalId = newWebhook.ItemId;

            _blocksWebhookRepository.Setup(r => r.GetAsync()).ReturnsAsync((BlocksWebhook?)null);
            _blocksWebhookRepository.Setup(r => r.SaveAsync(It.IsAny<BlocksWebhook>())).Returns(Task.CompletedTask);

            // Act
            var response = await _service.SaveWebhookAsync(newWebhook);

            // Assert
            response.Success.Should().BeTrue();
            _blocksWebhookRepository.Verify(r => r.SaveAsync(It.Is<BlocksWebhook>(w => w.ItemId == originalId)), Times.Once);
        }
    }
}
