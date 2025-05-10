using Google.Cloud.Firestore;

namespace LeeXerriPFC_Functions.Models
{
    [FirestoreData]
    public class User
    {
        [FirestoreProperty] public string Email { get; set; }
        [FirestoreProperty] public string FirstName { get; set; }
        [FirestoreProperty] public string LastName { get; set; }
        [FirestoreProperty] public string Role { get; set; } = "User";
    }
}
