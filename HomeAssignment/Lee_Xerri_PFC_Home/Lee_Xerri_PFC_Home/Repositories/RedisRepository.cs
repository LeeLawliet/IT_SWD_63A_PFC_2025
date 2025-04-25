using Lee_Xerri_PFC_Home.Models;
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
            var json = JsonConvert.SerializeObject(ticket);
            // Hash key = "tickets", field = ticket.TicketId
            await _db.HashSetAsync("tickets", ticket.TicketId, json);
        }

        public async Task<List<Ticket>> GetCachedTicketsAsync()
        {
            //var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            //var keys = server.Keys(pattern: "*");
            //var list = new List<Ticket>();
            //foreach (var key in keys)
            //{
            //    var json = await _db.StringGetAsync(key);
            //    if (json.HasValue)
            //        list.Add(JsonConvert.DeserializeObject<Ticket>(json));
            //}
            //return list;
            var entries = await _db.HashGetAllAsync("tickets");
            return entries
                .Select(e => JsonConvert.DeserializeObject<Ticket>(e.Value))
                .Where(t => t != null)
                .ToList()!;
        }

        public Task RemoveTicketAsync(string ticketId) =>
            _db.HashDeleteAsync("tickets", ticketId);
    }
}
