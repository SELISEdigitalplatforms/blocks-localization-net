using Microsoft.AspNetCore.Mvc;

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
    }
}
