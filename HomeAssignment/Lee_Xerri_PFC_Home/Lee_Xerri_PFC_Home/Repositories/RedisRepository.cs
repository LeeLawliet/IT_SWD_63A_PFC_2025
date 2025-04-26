using Lee_Xerri_PFC_Home.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Lee_Xerri_PFC_Home.Repositories
{
    public class RedisRepository
    {
        private readonly IDatabase _db;

        public RedisRepository(IConnectionMultiplexer muxer)
        {
            _db = muxer.GetDatabase();
        }

        public async Task SaveTicketAsync(Ticket ticket)
        {
            // Save each ticket with its own key
            var json = JsonConvert.SerializeObject(ticket);
            string key = $"ticket:{ticket.TicketId}"; // unique key for each ticket
            await _db.StringSetAsync(key, json);
        }

        public async Task<List<Ticket>> GetCachedTicketsAsync()
        {
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: "ticket:*"); // only fetch keys for tickets

            var list = new List<Ticket>();
            foreach (var key in keys)
            {
                var json = await _db.StringGetAsync(key);
                if (json.HasValue)
                    list.Add(JsonConvert.DeserializeObject<Ticket>(json!));
            }
            return list;
        }

        public Task RemoveTicketAsync(string ticketId)
        {
            string key = $"ticket:{ticketId}";
            return _db.KeyDeleteAsync(key);
        }
    }
}
