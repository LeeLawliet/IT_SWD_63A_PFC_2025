using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Cloud.SecretManager.V1;

namespace LeeXerriPFC_Functions.Repositories
{
    public static class SecretRepository
    {
        private const string ProjectId = "pftchome-457811";

        public static string GetSecret(string secretId)
        {
            SecretManagerServiceClient client = SecretManagerServiceClient.Create();
            var name = new SecretVersionName(ProjectId, secretId, "latest");
            var result = client.AccessSecretVersion(name);
            return result.Payload.Data.ToStringUtf8();
        }
    }
}