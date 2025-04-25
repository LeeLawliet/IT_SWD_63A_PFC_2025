using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;

namespace Lee_Xerri_PFC_Home.Repositories
{
    public class BucketRepository
    {
        private readonly StorageClient _client;
        private readonly string _bucketName = "ticket-imgs";

        public BucketRepository(IConfiguration config)
        {
            _bucketName = config["BucketId"] ?? throw new ArgumentNullException("BucketId");

            // Try to read a JSON credentials path; if not provided, fall back to ADC
            string credPath = config["GoogleCloud:CredentialsFilePath"];
            GoogleCredential credential = string.IsNullOrEmpty(credPath)
                ? GoogleCredential.GetApplicationDefault()
                : GoogleCredential.FromFile(credPath);

            _client = StorageClient.Create(credential);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("Invalid file");

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            using var stream = file.OpenReadStream();
            await _client.UploadObjectAsync(_bucketName, fileName, file.ContentType ?? "application/octet-stream", stream);

            return $"https://storage.googleapis.com/{_bucketName}/{fileName}";
        }
    }
}