using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Lee_Xerri_PFC_Home.Models
{
    [FirestoreData]
    public class Ticket
    {
        [FirestoreProperty] public string TicketId { get; set; }
        [FirestoreProperty] public string Title { get; set; }
        [FirestoreProperty] public string Description { get; set; }
        [BindNever] [FirestoreProperty] public DateTime DateUploaded { get; set; }
        [BindNever] [FirestoreProperty] public string UserEmail { get; set; }
        
        [FirestoreProperty] public string Priority { get; set; }
        [BindNever] [FirestoreProperty] public string Status { get; set; } = "queued";
        [BindNever] [FirestoreProperty] public List<string> ImageUrls { get; set; } = new();
    }
}
