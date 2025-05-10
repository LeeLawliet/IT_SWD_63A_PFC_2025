using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using LeeXerriPFC_Functions.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeeXerriPFC_Functions.Services
{
    public class PubSubService
    {
        private readonly string _projectId = "pftchome-457811";
        private readonly string _topicId = "tickets-topic";
        private readonly string _subId = "tickets-topic-sub";

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

        public async Task<List<ReceivedMessage>> PullAsync(int maxMessages = 20)
        {
            var client = await SubscriberServiceApiClient.CreateAsync();
            var subName = SubscriptionName.FromProjectSubscription(_projectId, _subId);

            var resp = await client.PullAsync(subName,
                                     returnImmediately: false,
                                     maxMessages: maxMessages);

            return resp.ReceivedMessages.ToList();
        }

        public async Task AcknowledgeAsync(IEnumerable<string> ackIds)
        {
            var client = await SubscriberServiceApiClient.CreateAsync();
            SubscriptionName subName = SubscriptionName.FromProjectSubscription(_projectId, _subId);
            await client.AcknowledgeAsync(subName, ackIds);
        }

        public Ticket Deserialize(ReceivedMessage msg) =>
            JsonConvert.DeserializeObject<Ticket>(msg.Message.Data.ToStringUtf8());

    }
}
