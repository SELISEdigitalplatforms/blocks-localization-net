using Blocks.Genesis;
using DomainService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class AssistantController : Controller
    {
        private readonly ChangeControllerContext _changeControllerContext;
        private readonly IAssistantService _assistantService;

        public AssistantController(
            ChangeControllerContext changeControllerContext,
            IAssistantService assistantService
        )
        {
            _changeControllerContext = changeControllerContext;
            _assistantService = assistantService;
        }

        //[HttpPost]
        //[Authorize]
        //public async Task<IActionResult> AiCompletion([FromBody] AiCompletionRequest request)
        //{
        //    //_changeControllerContext.ChangeContext(request);
        //    var response = await _assistantService.AiCompletion(request);
        //    return StatusCode((int)HttpStatusCode.OK, new
        //    {
        //        Content = response
        //    });
        //}

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetTranslationSuggestion([FromBody] SuggestLanguageRequest request)
        {
            var response = await _assistantService.SuggestTranslation(request);
            return StatusCode((int)HttpStatusCode.OK, new
            {
                Content = response
            });
        }
    }
}
