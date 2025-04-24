using Lee_Xerri_PFC_Home.Models;
using Lee_Xerri_PFC_Home.Repositories;
using Lee_Xerri_PFC_Home.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lee_Xerri_PFC_Home.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        private readonly BucketRepository _bucketRepository;
        private readonly FirestoreRepository _firestoreRepository;
        private readonly PubSubService _pubSubService;

        public TicketController(FirestoreRepository firestoreRepository, BucketRepository bucketRepository, PubSubService pubSubService)
        {
            _firestoreRepository = firestoreRepository;
            _bucketRepository = bucketRepository;
            _pubSubService = pubSubService;
        }

        [HttpGet]
        public IActionResult UploadTicket()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadTicket(Ticket ticket, List<IFormFile> images)
        {
            try
            {
                var imageUrls = new List<string>();

                foreach (var image in images)
                {
                    if (image != null && image.Length > 0)
                    {
                        var url = await _bucketRepository.UploadImageAsync(image);
                        imageUrls.Add(url);
                    }
                }

                ticket.ImageUrls = imageUrls;
                ticket.DateUploaded = DateTime.UtcNow;
                ticket.Status = "queued";
                ticket.UserEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "unknown";

                await _firestoreRepository.UpdateOrAddTicket(ticket);
                await _pubSubService.PublishTicketAsync(ticket);

                TempData["SuccessMessage"] = "Ticket submitted successfully.";
                return RedirectToAction("UploadTicket");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Upload failed for the following reasons: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                TempData["ErrorMessage"] = "There was an error uploading your ticket.";
                return RedirectToAction("UploadTicket");
            }
        }
    }
}
