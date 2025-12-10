using Blocks.Genesis;
using DomainService.Services.HelperService;
using DomainService.Shared.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace XUnitTest
{
    public class NotificationServiceTests
    {
        private readonly Mock<ICryptoService> _cryptoMock;
        private readonly Mock<ITenants> _tenantsMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IHttpHelperServices> _httpHelperMock;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _cryptoMock = new Mock<ICryptoService>();
            _tenantsMock = new Mock<ITenants>();
            _configMock = new Mock<IConfiguration>();
            _httpHelperMock = new Mock<IHttpHelperServices>();

            _configMock.SetupGet(c => c["RootTenantId"]).Returns("root");
            _configMock.SetupGet(c => c["NotificationServiceUrl"]).Returns("http://notify");

            _tenantsMock.Setup(t => t.GetTenantByID("root"))
                .Returns(new Tenant
                {
                    TenantSalt = "salt",
                    ApplicationDomain = "",
                    DbConnectionString = "",
                    JwtTokenParameters = new JwtTokenParameters()
                });
            _cryptoMock.Setup(c => c.Hash("root", "salt")).Returns("hashed");

            _service = new NotificationService(_cryptoMock.Object, _tenantsMock.Object, _configMock.Object, _httpHelperMock.Object);
        }

        [Fact]
        public async Task NotifyExportEvent_ReturnsTrueOnSuccess()
        {
            _httpHelperMock.Setup(h => h.MakeHttpPostRequest<NotificationResponse>(It.IsAny<object>(), "http://notify", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((new NotificationResponse { isSuccess = true }, "ok"));

            var result = await _service.NotifyExportEvent(true, "file1", "corr", "tenant");

            result.Should().BeTrue();
        }

        [Fact]
        public async Task NotifyExportEvent_ReturnsFalseOnNullResponse()
        {
            _httpHelperMock.Setup(h => h.MakeHttpPostRequest<NotificationResponse>(It.IsAny<object>(), "http://notify", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(((NotificationResponse)null, "fail"));

            var result = await _service.NotifyExportEvent(false, "file1", "corr", "tenant");

            result.Should().BeFalse();
        }

        [Fact]
        public async Task NotifyTranslateAllEvent_UsesHashedSecret()
        {
            _httpHelperMock.Setup(h => h.MakeHttpPostRequest<NotificationResponse>(It.IsAny<object>(), "http://notify", It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((new NotificationResponse { isSuccess = true }, "ok"));

            await _service.NotifyTranslateAllEvent(true, "corr");

            _cryptoMock.Verify(c => c.Hash("root", "salt"), Times.AtLeastOnce);
            _httpHelperMock.Verify(h => h.MakeHttpPostRequest<NotificationResponse>(It.IsAny<object>(), "http://notify", It.Is<Dictionary<string, string>>(d => d["Secret"] == "hashed")), Times.Once);
        }
    }
}

