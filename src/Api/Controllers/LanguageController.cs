using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    /// <summary>
    /// API Controller for managing language-related operations.
    /// </summary>
    
    [ApiController]
    [Route("[controller]/[action]")]

    public class LanguageController : Controller
    {
        private readonly ILanguageManagementService _languageManagementService;
        private readonly ChangeControllerContext _changeControllerContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageController"/> class.
        /// </summary>
        /// <param name="languageManagementService">The service used for managing languages.</param>

        public LanguageController(
            ILanguageManagementService languageManagementService,
            ChangeControllerContext changeControllerContext)
        {
            _languageManagementService = languageManagementService;
            _changeControllerContext = changeControllerContext;
        }

        /// <summary>
        /// Saves a new or existing language.
        /// </summary>
        /// <param name="language">The language object to be saved.</param>
        /// <returns>An <see cref="ApiResponse"/> indicating the success or failure of the operation.</returns>
        

        [HttpPost]
        [Authorize]
        public async Task<ApiResponse> Save(Language language)
        {
            if (language == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(language);
            return await _languageManagementService.SaveLanguageAsync(language);
        }

        /// <summary>
        /// Retrieves all available languages.
        /// </summary>
        /// <returns>A list of <see cref="Language"/> objects.</returns>
        
        [HttpGet]
        [Authorize]
        public async Task<List<Language>> Gets([FromQuery] GetLanguagesRequest request)
        {
            if (request == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);
            return await _languageManagementService.GetLanguagesAsync();
        }
    }
}
