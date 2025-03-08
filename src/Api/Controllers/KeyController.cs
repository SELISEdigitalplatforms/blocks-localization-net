using Azure.Core;
using Blocks.Genesis;
using DomainService.Services;
using DomainService.Shared;
using DomainService.Shared.Events;
using DomainService.Utilities;
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
        public async Task<GetKeysQueryResponse> Gets([FromQuery] GetKeysRequest query)
        {
            if (query == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(query);
            return await _keyManagementService.GetKeysAsync(query);
        }

        [HttpGet]
        [Authorize]
        public async Task<Key?> Get([FromQuery] GetKeyRequest request)
        {
            if (request == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);

            var result = await _keyManagementService.GetAsync(request);
            if (result == null)
            {
                var response = new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "Key", "No key found" }
                    }
                };

                BadRequest(response);
            }

            return result;
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> Delete([FromQuery] DeleteKeyRequest request)
        {
            if (request == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);

            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                return BadRequest(new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "ItemId", "Invalid or missing ConfigurationId" }
                    }
                });
            }

            var result = await _keyManagementService.DeleteAsysnc(request);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpGet]
        public async Task GetUilmFile([FromQuery] GetUilmFileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectKey))
            {
                Response.StatusCode = 401;
                await Response.WriteAsync(string.Empty);
                return;
            }
            if (request == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);
            Response.ContentType = "application/json";

            string result = await _keyManagementService.GetUilmFile(request);
            await Response.WriteAsync(result ?? "");
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GenerateUilmFile([FromBody] GenerateUilmFilesRequest request)
        {
            //if (request == null) return BadRequest();
            if (request == null) return BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);
            await _keyManagementService.SendGenerateUilmFilesEvent(request);
            return Ok(new BaseMutationResponse { IsSuccess = true });
        }

        [HttpPost]
        //[Authorize]
        public async Task<IActionResult> TranslateAll(TranslateAllRequest request)
        {
            if (request == null) BadRequest(new BaseMutationResponse());
            _changeControllerContext.ChangeContext(request);

            if (string.IsNullOrWhiteSpace(request.ProjectKey))
            {
                return BadRequest(new BaseMutationResponse
                {
                    IsSuccess = false,
                    Errors = new Dictionary<string, string>
                    {
                        { "ProjectKey", "Invalid or missing ProjectKey" }
                    }
                });
            }

            await _keyManagementService.SendTranslateAllEvent(request);
            return Ok(new BaseMutationResponse { IsSuccess = true });
        }

    }
}
