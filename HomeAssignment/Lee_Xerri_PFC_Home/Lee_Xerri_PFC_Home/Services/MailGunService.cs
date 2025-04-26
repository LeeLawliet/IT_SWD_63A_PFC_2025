using Lee_Xerri_PFC_Home.Models;
using RestSharp;
using RestSharp.Authenticators;
using System.Net.Http.Headers;
using System.Text;

namespace Lee_Xerri_PFC_Home.Services
{
    public class MailGunService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _domain;

        public MailGunService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["MailGunApiKey"];
            _domain = config["MailGunDomain"];
        }

        public async Task<RestResponse> SendTicketNotificationAsync(Ticket ticket, string to)
        {
            var options = new RestClientOptions("https://api.mailgun.net")
            {
                Authenticator = new HttpBasicAuthenticator("api", _apiKey)
            };

            var client = new RestClient(options);
            var request = new RestRequest("/v3/sandbox93cc4d0a83bc45448cde7066d9a008f8.mailgun.org/messages", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddParameter("from", "Mailgun Sandbox <postmaster@sandbox93cc4d0a83bc45448cde7066d9a008f8.mailgun.org>");
            request.AddParameter("to", ticket.UserEmail);
            request.AddParameter("subject", "ProcessTickets");
            request.AddParameter("text", $"Ticket: {ticket.TicketId}\nDescription: {ticket.Description}\nPriority: {ticket.Priority}\nFiled by: {ticket.UserEmail}\nDate: {ticket.DateUploaded:yyyy-MM-dd HH:mm}");

            var response = await client.ExecuteAsync(request);
            Console.WriteLine($"Mailgun replied {response.StatusCode} / {response.StatusDescription}");
            Console.WriteLine($"Body: {response.Content}");
            foreach (var h in response.Headers)
                Console.WriteLine($"{h.Name}: {h.Value}");
            return response;
        }

        public async Task<RestResponse> SendPendingTickets(int pending, string to)
        {
            var options = new RestClientOptions("https://api.mailgun.net")
            {
                Authenticator = new HttpBasicAuthenticator("api", _apiKey)
            };

            var client = new RestClient(options);
            var request = new RestRequest("/v3/sandbox93cc4d0a83bc45448cde7066d9a008f8.mailgun.org/messages", Method.Post);
            request.AlwaysMultipartFormData = true;
            request.AddParameter("from", "Mailgun Sandbox <postmaster@sandbox93cc4d0a83bc45448cde7066d9a008f8.mailgun.org>");
            request.AddParameter("to", to);
            request.AddParameter("subject", "ProcessTickets");
            request.AddParameter("text", $"Date: {DateTime.UtcNow}\nPending tickets: {pending}");

            var response = await client.ExecuteAsync(request);
            Console.WriteLine($"Mailgun replied {response.StatusCode} / {response.StatusDescription}");
            Console.WriteLine($"Body: {response.Content}");
            foreach (var h in response.Headers)
                Console.WriteLine($"{h.Name}: {h.Value}");
            return response;
        }
    }
}
