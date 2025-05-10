using Google.Cloud.Functions.Framework;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Firestore;
using Google.Cloud.Logging.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LeeXerriPFC_Functions.Models;
using LeeXerriPFC_Functions.Repositories;
using LeeXerriPFC_Functions.Services;
using Google.Cloud.SecretManager.V1;

namespace LeeXerriPFC_Functions
{
    public class ProcessTicketsFunction : IHttpFunction
    {
        private readonly SubscriberServiceApiClient _subscriberClient = SubscriberServiceApiClient.Create();
        private readonly FirestoreDb _firestore;
        private readonly RedisRepository _cache;
        private readonly MailGunService _mailer;

        public ProcessTicketsFunction()
        {
            var redisConnection = SecretRepository.GetSecret("RedisConnection");
            var redisUsername = SecretRepository.GetSecret("RedisUsername");
            var redisPassword = SecretRepository.GetSecret("RedisPassword");

            _cache = new RedisRepository(redisConnection, redisUsername, redisPassword);
            _mailer = new MailGunService(
                SecretRepository.GetSecret("MailGunApiKey"),
                SecretRepository.GetSecret("MailGunDomain")
            );
            
            string projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID")
                               ?? throw new Exception("Missing GOOGLE_PROJECT_ID");
            _firestore = FirestoreDb.Create(projectId);
        }

        public async Task HandleAsync(HttpContext context)
        {
            try
            {
                string projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
                string subscriptionId = Environment.GetEnvironmentVariable("PUBSUB_SUBSCRIPTION_ID");

                if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(subscriptionId))
                    throw new Exception("Missing required environment variables.");

                var subName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
                var pullRequest = new PullRequest
                {
                    SubscriptionAsSubscriptionName = subName,
                    MaxMessages = 100
                };

                var response = await _subscriberClient.PullAsync(pullRequest);
                var allMessages = response.ReceivedMessages;

                if (!allMessages.Any())
                {
                    await context.Response.WriteAsync("No new tickets found.");
                    return;
                }

                var priorityOrder = new[] { "High", "Medium", "Low" };
                int processedCount = 0;

                foreach (var priority in priorityOrder)
                {
                    // Filter new messages of the current priority
                    var messagesToProcess = allMessages
                        .Select(msg => new
                        {
                            Message = msg,
                            Ticket = JsonSerializer.Deserialize<Ticket>(msg.Message.Data.ToStringUtf8())
                        })
                        .Where(x => string.Equals(x.Ticket.Priority, priority, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Process all new messages of the current priority
                    foreach (var item in messagesToProcess)
                    {
                        await _cache.SaveTicketAsync(item.Ticket);

                        var technicianEmail = await GetTechnicianEmailAsync();
                        if (!string.IsNullOrEmpty(technicianEmail))
                        {
                            await _mailer.SendTicketNotificationAsync(item.Ticket, technicianEmail);
                        }

                        await _subscriberClient.AcknowledgeAsync(subName, new[] { item.Message.AckId });
                        processedCount++;
                    }

                    // After processing, check if any non-closed tickets of this priority remain in cache
                    var cachedTickets = await _cache.GetCachedTicketsAsync();
                    bool stillOpen = cachedTickets.Any(t =>
                        string.Equals(t.Priority, priority, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(t.Status, "closed", StringComparison.OrdinalIgnoreCase));

                    if (stillOpen)
                    {
                        await context.Response.WriteAsync($"Processed {processedCount} tickets. Waiting for {priority} tickets in cache to be closed before proceeding.");
                        return;
                    }
                }

                await context.Response.WriteAsync($"Processing complete. {processedCount} new ticket(s) processed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error: {ex}");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error occurred.");
            }
        }

        private async Task<string?> GetTechnicianEmailAsync()
        {
            var users = _firestore.Collection("users");
            var query = users.WhereEqualTo("Role", "Technician");
            var snapshot = await query.GetSnapshotAsync();

            var tech = snapshot.Documents.FirstOrDefault();
            return tech?.GetValue<string>("Email");
        }
    }
}