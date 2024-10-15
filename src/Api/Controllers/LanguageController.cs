using DomainService.Services;
using DomainService.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    public class LanguageController : Controller
    {
        private readonly ILanguageManagementService _languageManagementService;

        public LanguageController(ILanguageManagementService languageManagementService)
        {
            _languageManagementService = languageManagementService;
        }

        [HttpPost]
        public async Task<ApiResponse> Save(Language language)
        {
          return await _languageManagementService.SaveLanguageAsync(language);
        }

        [HttpGet]
        public async Task<List<Language>> Gets()
        {
            return await _languageManagementService.GetLanguagesAsync();
        }
    }
}
