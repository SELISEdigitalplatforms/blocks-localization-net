using Azure.Core;
using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
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
        private readonly ChangeControllerContext _changeControllerContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyController"/> class.
        /// </summary>
        /// <param name="keyManagementService">The service for managing keys.</param>

        public KeyController(
            IKeyManagementService keyManagementService,
            ChangeControllerContext changeControllerContext)
        {
            _keyManagementService = keyManagementService;
            _changeControllerContext = changeControllerContext;
        }

        /// <summary>
        /// Saves a new or existing key to the system.
        /// </summary>
        /// <param name="key">The key object to be saved.</param>
        /// <returns>An <see cref="ApiResponse"/> indicating the success or failure of the save operation.</returns>
   
        [HttpPost]
        [Authorize]
        public async Task<ApiResponse> Save(Key key)
        {
            if (key == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(key);
            return await _keyManagementService.SaveKeyAsync(key);
        }

        /// <summary>
        /// Retrieves all available Keys.
        /// </summary>
        /// <returns>A list of <see cref="Key"/> objects.</returns>
        [HttpGet]
        [Authorize]
        public async Task<List<Key>> Gets([FromQuery] GetKeysQuery query)
        {
            if (query == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(query);
            return await _keyManagementService.GetKeysAsync(query);
        }
    }
}
