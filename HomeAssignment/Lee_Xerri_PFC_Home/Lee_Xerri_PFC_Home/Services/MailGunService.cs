using Lee_Xerri_PFC_Home.Models;
using System.Net.Http.Headers;
using System.Text;

namespace Lee_Xerri_PFC_Home.Services
{
    public class MailGunService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _domain;
        private readonly string _fromAddress;

        public MailGunService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["MailGun:ApiKey"];
            _domain = config["MailGun:Domain"];
            _fromAddress = config["MailGun:From"];
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"api:{_apiKey}"))); // Ensure System.Text.Encoding is used
        }

        public async Task SendTicketNotificationAsync(Ticket ticket, string to)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                    new KeyValuePair<string,string>("from", _fromAddress),
                    new KeyValuePair<string,string>("to", to),
                    new KeyValuePair<string,string>("subject", $"[{ticket.Priority}] #{ticket.TicketId} {ticket.Title}"),
                    new KeyValuePair<string,string>("text", ticket.Description)
                });
            var resp = await _http.PostAsync($"https://api.mailgun.net/v3/{_domain}/messages", content);
            resp.EnsureSuccessStatusCode();
        }
    }
}
