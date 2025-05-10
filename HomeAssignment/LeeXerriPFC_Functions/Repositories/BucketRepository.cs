using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LeeXerriPFC_Functions.Repositories
{
    public class BucketRepository
    {
        private readonly StorageClient _client;
        private readonly string _bucketName = "ticket-imgs";

        public BucketRepository(IConfiguration config)
        {
            _bucketName = config["BucketId"] ?? throw new ArgumentNullException("BucketId");

            // try to read a JSON credentials path; if not provided, fall back to ADC
            string credPath = config["GoogleCloud:CredentialsFilePath"];
            GoogleCredential credential = string.IsNullOrEmpty(credPath)
                ? GoogleCredential.GetApplicationDefault()
                : GoogleCredential.FromFile(credPath);

            _client = StorageClient.Create(credential);
        }

        public async Task<string> UploadImageAsync(IFormFile file, string uploaderEmail, IEnumerable<string> technicianEmails)
        {
            if (file == null || file.Length == 0)
                throw new Exception("Invalid file");

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            using var stream = file.OpenReadStream();
            var obj = await _client.UploadObjectAsync(
                _bucketName,
                fileName,
                file.ContentType ?? "application/octet-stream",
                stream
            );

            var readers = new[] { uploaderEmail }
                .Concat(technicianEmails)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Set the ACL for the uploader
            obj.Acl = readers.Select(email => new Google.Apis.Storage.v1.Data.ObjectAccessControl
            {
                Entity = $"user-{email}",
                Role = "READER"
            }).ToList();

            await _client.UpdateObjectAsync(obj);

            return $"https://storage.googleapis.com/{_bucketName}/{fileName}";
        }
    }
}