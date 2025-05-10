using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Newtonsoft.Json;

Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", @"E:\Repos\IT_SWD_63A_PFC_2025\PFTC_THA\Lee_Xerri_PFC_TCA\Lee_Xerri_PFC_TCA\service-account.json");
FirestoreDb db = FirestoreDb.Create("pftcclass");
var json = File.ReadAllText("E:\\Repos\\IT_SWD_63A_PFC_2025\\PFTC_THA\\Lee_Xerri_PFC_TCA\\Lee_Xerri_PFC_TCA\\appointments.json");
var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
foreach (var item in data)
    await db.Collection("appointments").AddAsync(item);