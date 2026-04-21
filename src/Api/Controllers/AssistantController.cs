using Blocks.Genesis;
using DomainService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class AssistantController : ControllerBase
    {
        private readonly IAssistantService _assistantService;
        private readonly ChangeControllerContext _changeControllerContext;

        public AssistantController(IAssistantService assistantService, ChangeControllerContext changeControllerContext)
        {
            _assistantService = assistantService;
            _changeControllerContext = changeControllerContext;
        }
        
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GetTranslationSuggestion([FromBody] SuggestLanguageRequest request)
        {
            _changeControllerContext.ChangeContext(request);
            var response = await _assistantService.SuggestTranslation(request);
            return StatusCode((int)HttpStatusCode.OK, new
            {
                Content = response
            });
        }
    }
}
