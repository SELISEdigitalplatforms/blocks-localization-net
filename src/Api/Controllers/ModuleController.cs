using DomainService.Services;
using DomainService.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    /// <summary>
    /// Handles operations related to managing modules, such as saving and retrieving module data.
    /// </summary>
    
    [ApiController]
    [Route("[controller]/[action]")]

    public class ModuleController : Controller
    {
        private readonly IModuleManagementService _moduleManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleController"/> class.
        /// </summary>
        /// <param name="moduleManagementService"></param>


        public ModuleController(IModuleManagementService moduleManagementService)
        {
            _moduleManagementService = moduleManagementService;
        }


        /// <summary>
        /// Saves a new module or updates an existing one.
        /// </summary>
        /// <param name="module">The module object to be saved.</param>
        /// <returns>An <see cref="ApiResponse"/> indicating the result of the save operation.</returns>
        
        [HttpPost]
        public async Task<ApiResponse> Save([FromBody]Module module)
        {
            return await _moduleManagementService.SaveModuleAsync(module);
        }

        /// <summary>
        /// Retrieves a list of all available modules.
        /// </summary>
        /// <returns>A list of <see cref="Module"/> objects.</returns>
        
        [HttpGet]
        public async Task<List<Module>> Gets()
        {
            return await _moduleManagementService.GetModulesAsync();
        }
    }
}
