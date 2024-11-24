using DomainService.Services;
using DomainService.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    /// <summary>
    /// API Controller for managing Key operations.
    /// </summary>
    /// 

    [ApiController]
    [Route("[controller]/[action]")]
    
    public class KeyController : Controller
    {
        private readonly IKeyManagementService _keyManagementService;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyController"/> class.
        /// </summary>
        /// <param name="keyManagementService">The service for managing keys.</param>
        
        public KeyController(IKeyManagementService keyManagementService)
        {
            _keyManagementService = keyManagementService;
        }

        /// <summary>
        /// Saves a new or existing key to the system.
        /// </summary>
        /// <param name="key">The key object to be saved.</param>
        /// <returns>An <see cref="ApiResponse"/> indicating the success or failure of the save operation.</returns>
   
        [HttpPost]
        public async Task<ApiResponse> Save(Key key)
        {
            return await _keyManagementService.SaveKeyAsync(key);
        }

        /// <summary>
        /// Retrieves all available Keys.
        /// </summary>
        /// <returns>A list of <see cref="Key"/> objects.</returns>
        [HttpGet]
        public async Task<List<Key>> Gets()
        {
            return await _keyManagementService.GetKeysAsync();
        }
    }
}
