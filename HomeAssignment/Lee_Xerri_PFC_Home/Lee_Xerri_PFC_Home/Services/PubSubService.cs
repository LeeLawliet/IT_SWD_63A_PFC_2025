using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Lee_Xerri_PFC_Home.Models;
using Newtonsoft.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lee_Xerri_PFC_Home.Services
{
    public class PubSubService
    {
        private readonly string _projectId = "pftchome-457811";
        private readonly string _topicId = "tickets-topic";

        public async Task<string> PublishTicketAsync(Ticket ticket)
        {
            TopicName topicName = new TopicName(_projectId, _topicId);
            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

            string jsonPayload = JsonConvert.SerializeObject(ticket, Formatting.Indented);
            var message = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(jsonPayload),
                Attributes = { { "priority", ticket.Priority.ToLower() } }
            };

            string result = await publisher.PublishAsync(message);
            return result;
        }
    }
}
