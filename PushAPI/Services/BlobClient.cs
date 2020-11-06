using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Sas;
using Blobs = Azure.Storage.Blobs;

namespace PushAPI.Services
{
    public class BlobClient
    {
        private readonly string connectionString;
        private readonly string containerName;
        private readonly string accountName;
        private readonly string accountKey;
        private readonly Blobs.BlobContainerClient blobContainerClient;

        public BlobClient(string connectionString, string containerName, string accountName, string accountKey)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new System.ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new System.ArgumentException($"'{nameof(containerName)}' cannot be null or whitespace", nameof(containerName));
            }

            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new ArgumentException($"'{nameof(accountName)}' cannot be null or whitespace", nameof(accountName));
            }

            if (string.IsNullOrWhiteSpace(accountKey))
            {
                throw new ArgumentException($"'{nameof(accountKey)}' cannot be null or whitespace", nameof(accountKey));
            }

            this.connectionString = connectionString;
            this.containerName = containerName;
            this.accountName = accountName;
            this.accountKey = accountKey;
            var client = new Blobs.BlobServiceClient(this.connectionString);
            this.blobContainerClient = client.GetBlobContainerClient(this.containerName);
        }

        public Task CreateEntityAsync(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new System.ArgumentException($"'{nameof(entityName)}' cannot be null or whitespace", nameof(entityName));
            }

            return this.blobContainerClient.CreateIfNotExistsAsync();
        }

        internal async Task<string> PostRowsAsync(string entityName, string row)
        {
            var path = $"{entityName}/{DateTimeOffset.UtcNow.ToString("yyyy.MM.dd.hh.mm")}/{System.Guid.NewGuid().ToString()}";
            var client = this.blobContainerClient.GetBlobClient(path);
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(row));
            await client.UploadAsync(stream);

            return path;
        }

        private string GetBlobSasUri(string uri)
        {
            var adHocSAS = new BlobSasBuilder
            {
                BlobContainerName = this.containerName,
                ExpiresOn = DateTime.UtcNow.AddHours(1),
            };

            adHocSAS.SetPermissions(BlobSasPermissions.All);
            var sasToken = adHocSAS.ToSasQueryParameters(new Azure.Storage.StorageSharedKeyCredential(this.accountName, this.accountKey));

            return uri + sasToken;
        }
    }
}