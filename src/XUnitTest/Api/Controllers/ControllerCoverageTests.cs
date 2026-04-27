using Api.Controllers;
using Blocks.Genesis;
using DomainService.Services;
using DomainService.Services.HelperService;
using DomainService.Shared;
using DomainService.Shared.Entities;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace XUnitTest
{
    /// <summary>
    /// Supplemental tests targeting branches in ConfigController, GlossaryController
    /// and KeyController that were not covered by the existing test classes, to push
    /// SonarQube coverage above 95%.
    /// </summary>
    public class ControllerCoverageTests
    {
        // ---------- Config ----------
        private ConfigController CreateConfigController(out Mock<IWebHookService> webhookMock)
        {
            webhookMock = new Mock<IWebHookService>();
            var ctx = TestChangeControllerContextFactory.Create();
            return new ConfigController(ctx, webhookMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Fact]
        public async Task Config_SaveWebHook_DelegatesToService_AndReturnsItsResponse()
        {
            var controller = CreateConfigController(out var svc);
            var webhook = new BlocksWebhook
            {
                Url = "https://x",
                ContentType = "application/json",
                ProjectKey = "p",
                BlocksWebhookSecret = new BlocksWebhookSecret { Secret = "s", HeaderKey = "H" }
            };
            var expected = new ApiResponse { Success = true };
            svc.Setup(x => x.SaveWebhookAsync(It.IsAny<BlocksWebhook>())).ReturnsAsync(expected);

            var result = await controller.SaveWebHook(webhook);

            result.Should().BeSameAs(expected);
            svc.Verify(x => x.SaveWebhookAsync(webhook), Times.Once);
        }

        // ---------- Glossary ----------
        private GlossaryController CreateGlossaryController(out Mock<IGlossaryManagementService> svcMock)
        {
            svcMock = new Mock<IGlossaryManagementService>();
            var ctx = TestChangeControllerContextFactory.Create();
            return new GlossaryController(svcMock.Object, ctx)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        [Fact]
        public async Task Glossary_Gets_WithNullRequest_ThrowsNullReferenceException()
        {
            var controller = CreateGlossaryController(out _);
            // ChangeContext(null) reaches reflection path -> NRE. Confirms null-branch is executed.
            await Assert.ThrowsAsync<NullReferenceException>(() => controller.Gets(null!));
        }

        [Fact]
        public async Task Glossary_Delete_WithNullRequest_ReturnsBadRequest()
        {
            var controller = CreateGlossaryController(out _);
            var result = await controller.Delete(null!);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Glossary_Delete_WithWhitespaceItemId_ReturnsBadRequest()
        {
            var controller = CreateGlossaryController(out _);
            var request = new DeleteGlossaryRequest { ItemId = "   ", ProjectKey = "p" };
            var result = await controller.Delete(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Glossary_Save_WhenServiceReturnsFailure_PropagatesResponse()
        {
            var controller = CreateGlossaryController(out var svc);
            var glossary = new Glossary { Name = "T", ProjectKey = "p" };
            var failure = new ApiResponse("boom");
            svc.Setup(x => x.SaveGlossaryAsync(glossary)).ReturnsAsync(failure);

            var result = await controller.Save(glossary);

            result.Should().BeSameAs(failure);
            result.Success.Should().BeFalse();
        }

        // ---------- Key ----------
        private KeyController CreateKeyController(
            out Mock<IKeyManagementService> svcMock,
            out Mock<IValidator<TranslateBlocksLanguageKeyRequest>> validatorMock,
            out DefaultHttpContext httpContext)
        {
            svcMock = new Mock<IKeyManagementService>();
            validatorMock = new Mock<IValidator<TranslateBlocksLanguageKeyRequest>>();
            httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            var ctx = TestChangeControllerContextFactory.Create();
            return new KeyController(svcMock.Object, ctx, validatorMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        [Fact]
        public async Task Key_SaveKeys_WithNullList_ReturnsErrorResponse()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var result = await controller.SaveKeys(null!);
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("null or empty");
        }

        [Fact]
        public async Task Key_GetsByKeyNames_WithValidRequest_DelegatesToService()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var request = new GetKeysByKeyNamesRequest
            {
                KeyNames = new[] { "A", "B" },
                ProjectKey = "p"
            };
            var expected = new GetKeysByKeyNamesResponse();
            svc.Setup(x => x.GetKeysByKeyNamesAsync(request)).ReturnsAsync(expected);

            var result = await controller.GetsByKeyNames(request);

            result.Should().BeSameAs(expected);
            svc.Verify(x => x.GetKeysByKeyNamesAsync(request), Times.Once);
        }

        [Fact]
        public async Task Key_GetLocalizationTimeline_DelegatesToService()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var query = new GetLocalizationTimelineRequest { ProjectKey = "p", PageSize = 5, PageNumber = 1 };
            var expected = new GetLocalizationTimelineResponse();
            svc.Setup(x => x.GetLocalizationTimelineAsync(query)).ReturnsAsync(expected);

            var result = await controller.GetLocalizationTimeline(query);

            result.Should().BeSameAs(expected);
        }

        [Fact]
        public async Task Key_GetLocalizationTimeline_WithNullQuery_ThrowsNullReferenceException()
        {
            var controller = CreateKeyController(out _, out _, out _);
            await Assert.ThrowsAsync<NullReferenceException>(() => controller.GetLocalizationTimeline(null!));
        }

        [Fact]
        public async Task Key_GetTimelineByOperationId_DelegatesToService()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var query = new GetTimelineByOperationIdRequest { OperationId = "op-1", ProjectKey = "p" };
            var expected = new GetKeyTimelineQueryResponse();
            svc.Setup(x => x.GetTimelineByOperationIdAsync(query)).ReturnsAsync(expected);

            var result = await controller.GetTimelineByOperationId(query);

            result.Should().BeSameAs(expected);
        }

        [Fact]
        public async Task Key_GetTimelineByOperationId_WithNullQuery_ThrowsNullReferenceException()
        {
            var controller = CreateKeyController(out _, out _, out _);
            await Assert.ThrowsAsync<NullReferenceException>(() => controller.GetTimelineByOperationId(null!));
        }

        [Fact]
        public async Task Key_GetUilmFile_WithValidProjectKey_WritesResultToResponse()
        {
            var controller = CreateKeyController(out var svc, out _, out var httpContext);
            var request = new GetUilmFileRequest { ProjectKey = "p", Language = "en", ModuleName = "m" };
            svc.Setup(x => x.GetUilmFile(request)).ReturnsAsync("{\"a\":1}");

            await controller.GetUilmFile(request);

            httpContext.Response.ContentType.Should().Be("application/json");
            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            (await reader.ReadToEndAsync()).Should().Be("{\"a\":1}");
        }

        [Fact]
        public async Task Key_GetUilmFile_WithNullServiceResult_WritesEmptyString()
        {
            var controller = CreateKeyController(out var svc, out _, out var httpContext);
            var request = new GetUilmFileRequest { ProjectKey = "p", Language = "en", ModuleName = "m" };
            svc.Setup(x => x.GetUilmFile(request)).ReturnsAsync((string)null!);

            await controller.GetUilmFile(request);

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            (await reader.ReadToEndAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task Key_GetUilmFile_WithWhitespaceProjectKey_Returns401()
        {
            var controller = CreateKeyController(out _, out _, out var httpContext);
            var request = new GetUilmFileRequest { ProjectKey = "   ", Language = "en", ModuleName = "m" };

            await controller.GetUilmFile(request);

            httpContext.Response.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Key_Delete_WithWhitespaceItemId_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new DeleteKeyRequest { ItemId = "   " };
            var result = await controller.Delete(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_DeleteCollections_WithNullCollections_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new DeleteCollectionsRequest { Collections = null };
            var result = await controller.DeleteCollections(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_DeleteCollections_WhenServiceFails_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var request = new DeleteCollectionsRequest { Collections = new List<string> { "c1" } };
            svc.Setup(x => x.DeleteCollectionsAsync(request))
               .ReturnsAsync(new BaseMutationResponse { IsSuccess = false });

            var result = await controller.DeleteCollections(request);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_GetLanguageFileGenerationHistory_WithNullRequest_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var result = await controller.GetLanguageFileGenerationHistory(null!);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_GetLanguageFileGenerationHistory_WithNegativePageNumber_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new GetLanguageFileGenerationHistoryRequest { PageSize = 10, PageNumber = -1 };
            var result = await controller.GetLanguageFileGenerationHistory(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_RollBack_WithNullRequest_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var result = await controller.RollBack(null!);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_RollBack_WithWhitespaceItemId_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new RollbackRequest { ItemId = "   " };
            var result = await controller.RollBack(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_RollBack_WhenServiceFails_ReturnsBadRequest()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var request = new RollbackRequest { ItemId = "k1" };
            svc.Setup(x => x.RollbackAsync(request))
               .ReturnsAsync(new BaseMutationResponse { IsSuccess = false });

            var result = await controller.RollBack(request);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Key_GetSuggestedGlossaries_DelegatesToService()
        {
            var controller = CreateKeyController(out var svc, out _, out _);
            var request = new GetSuggestedGlossariesRequest { ItemId = "k", ProjectKey = "p" };
            var expected = new GetSuggestedGlossariesResponse();
            svc.Setup(x => x.GetSuggestedGlossariesAsync(request)).ReturnsAsync(expected);

            var result = await controller.GetSuggestedGlossaries(request);

            result.Should().BeSameAs(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Key_UilmImport_WithInvalidProjectKey_ReturnsBadRequest(string? projectKey)
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new UilmImportRequest { ProjectKey = projectKey, FileId = "f" };
            var result = await controller.UilmImport(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Key_UilmExport_WithInvalidProjectKey_ReturnsBadRequest(string? projectKey)
        {
            var controller = CreateKeyController(out _, out _, out _);
            var request = new UilmExportRequest { ProjectKey = projectKey };
            var result = await controller.UilmExport(request);
            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
