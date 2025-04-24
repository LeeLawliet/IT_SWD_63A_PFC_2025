using Google.Cloud.Firestore;
using Lee_Xerri_PFC_Home.Models;
using System.Threading.Tasks;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace Lee_Xerri_PFC_Home.Repositories
{
    public class FirestoreRepository
    {
        private readonly FirestoreDb _db;

        public FirestoreRepository(IConfiguration config)
        {
            string projectId = config["ProjectId"];
            _db = FirestoreDb.Create(projectId);
        }

        public async Task<WriteResult> UpdateOrAddUser(User user)
        {
            DocumentReference docRef = _db.Collection("users").Document(user.Email);
            return await docRef.SetAsync(user);
        }

        public async Task<bool> UserExists(string email)
        {
            DocumentReference docRef = _db.Collection("users").Document(email);
            DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists;
        }

        public async Task<WriteResult> UpdateOrAddTicket(Ticket ticket)
        {
            try
            {
                // Ticket id will be Email + (Count of tickets submitted by user + 1)
                var userTicketsQuery = await this.GetTicketsByUser(ticket.UserEmail);
                int ticketCount = userTicketsQuery.Count;

                ticket.TicketId = $"{ticket.UserEmail}_Ticket{ticketCount+1}";
                DocumentReference docRef = _db.Collection("tickets").Document(ticket.TicketId);
                return await docRef.SetAsync(ticket);
            }
            catch (Exception e)
            {
                Console.WriteLine("Firestore write failed: " + e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }

        public async Task<List<Ticket>> GetTicketsByUser(string email)
        {
            var query = _db.Collection("tickets")
                           .WhereEqualTo("UserEmail", email);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<Ticket>()).ToList();
        }
    }

    public class GcsService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName = "ticket-screenshots";

        public GcsService()
        {
            _storageClient = StorageClient.Create();
        }

        public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType)
        {
            var obj = await _storageClient.UploadObjectAsync(_bucketName, fileName, contentType, fileStream);
            return $"https://storage.googleapis.com/{_bucketName}/{fileName}";
        }
    }
}
