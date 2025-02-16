using Blocks.Genesis;
using DomainService.Services.Assistant;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class AssistantController : Controller
    {
        private readonly ChangeControllerContext _changeControllerContext;

        public AssistantController(
            ChangeControllerContext changeControllerContext
        )
        {
            _changeControllerContext = changeControllerContext;
        }

        [HttpPost]
        [ProtectedEndPoint]
        public async Task<IActionResult> AiCompletion([FromBody] AiCompletionRequest request)
        {
            _changeControllerContext.ChangeContext(request);
            var response = await _commandHandler.SubmitAsync<AiCompletionRequest, string>(request);
            return StatusCode((int)HttpStatusCode.OK, new
            {
                Content = response
            });
        }
    }
}
