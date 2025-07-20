using DomainService.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StorageDriver;

namespace DomainService.Services.HelperService
{
    public class StorageHelper
    {
        private readonly ILogger<StorageHelper> _logger;
        private readonly IStorageDriverService _storageDriverService;
        private HttpClient _httpClient;

        public StorageHelper(
            ILogger<StorageHelper> logger,
            IStorageDriverService storageDriverService)
        {
            _logger = logger;
            _storageDriverService = storageDriverService;
        }

        public async Task<bool> SaveIntoStorage(MemoryStream inputStream, string fileId, string fileName, Dictionary<string, object> metaData, string parentDirectoryId)
        {
            _logger.LogInformation($"SaveIntoStorage: Saving file to storage -- fileId -- {fileId} -- fileName -- {fileName}");

            //var token = await GetTokenFromContext();

            Stream stream = new MemoryStream();
            stream.Write(inputStream.ToArray(), 0, inputStream.ToArray().Length);
            stream.Seek(0, SeekOrigin.Begin);

            var payload = new GetPreSignedUrlForUploadRequest
            {
                ItemId = fileId,
                MetaData = JsonConvert.SerializeObject(metaData),
                Name = fileName,
                ParentDirectoryId = parentDirectoryId,
                Tags = "[\"File\"]",
            };
            var fileInfo = await _storageDriverService.GetPerSignedUrlForUploadAsync(payload);// serviceClient.SendToHttpAsync<FileData>(HttpMethod.Post, appSettings.StorageServiceBaseUrl, storageServiceVersion, "StorageService/StorageQuery/GetPreSignedUrlForUpload", payload, token);

            _logger.LogInformation("SaveIntoStorage: Upload url - {url}", fileInfo?.UploadUrl);

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, fileInfo?.UploadUrl) { Content = new StreamContent(stream) })
            {
                AddAzureBlobHeaders(httpRequestMessage);
                //var httpResponseMessage = await serviceClient.SendToHttpAsync(httpRequestMessage);
                _httpClient = new HttpClient();

                using var request = new HttpRequestMessage(HttpMethod.Put, fileInfo.UploadUrl)
                {
                    Content = new StreamContent(stream)
                };

                request.Headers.Add("x-ms-blob-type", "BlockBlob");

                var httpResponseMessage = await _httpClient.SendAsync(request);
                stream.Close();
                return httpResponseMessage.IsSuccessStatusCode;
            }
        }

        public void AddAzureBlobHeaders(HttpRequestMessage httpRequestMessage)
        {
            try
            {
                httpRequestMessage.Headers.Add("x-ms-blob-type", "BlockBlob");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        //public async Task<Stream> GetFileStream(FileData fileData, string token = null)
        //{
        //    token ??= await GetTokenFromContext();

        //    var fileUrl = fileData.Url;

        //    var fileDataRequest = new HttpRequestMessage(HttpMethod.Get, fileUrl);

        //    if (fileData.AccessModifier == 2)
        //    {
        //        fileDataRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
        //    }

        //    var httpResponseMessage = await serviceClient.SendToHttpAsync(fileDataRequest);

        //    if (!httpResponseMessage.IsSuccessStatusCode)
        //    {
        //        return Stream.Null;
        //    }

        //    var memoryStream = new MemoryStream();

        //    await httpResponseMessage.Content.CopyToAsync(memoryStream);

        //    return memoryStream;
        //}

        //public async Task<FileData[]> GetFileInfosAsync(IEnumerable<string> filesToBeZipped, string token = null)
        //{
        //    token = await GetTokenFromContext();

        //    var payload = new { FileIds = filesToBeZipped };

        //    var fileInfos = await serviceClient.SendToHttpAsync<FileData[]>(HttpMethod.Post, appSettings.StorageServiceBaseUrl, storageServiceVersion, "StorageService/StorageQuery/GetFiles", payload, token);

        //    return fileInfos;
        //}

        //private async Task<string> GetTokenFromContext()
        //{
        //    return await _accessTokenProviderService.GetTheAccessTokenOfAdmin(_securityContextProvider.GetSecurityContext().TenantId);
        //}

        //public class FileInfo : FileData
        //{
        //    public MemoryStream fileStream { get; set; }
        //}
    }
}

