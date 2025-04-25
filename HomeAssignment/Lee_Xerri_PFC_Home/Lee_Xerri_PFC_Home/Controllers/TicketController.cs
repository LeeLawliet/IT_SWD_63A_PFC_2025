using Google.Cloud.Firestore.V1;
using Lee_Xerri_PFC_Home.Models;
using Lee_Xerri_PFC_Home.Repositories;
using Lee_Xerri_PFC_Home.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Security.Claims;

namespace Lee_Xerri_PFC_Home.Controllers
{
    public class TicketController : Controller
    {
        private readonly BucketRepository _bucketRepository;
        private readonly FirestoreRepository _firestoreRepository;
        private readonly RedisRepository _cache;
        private readonly PubSubService _pubSubService;
        private readonly MailGunService _mailer;
        private readonly ILogger<TicketController> _logger;

        public TicketController(FirestoreRepository firestoreRepository, BucketRepository bucketRepository, PubSubService pubSubService, RedisRepository cache, MailGunService mailer, ILogger<TicketController> logger)
        {
            _firestoreRepository = firestoreRepository;
            _bucketRepository = bucketRepository;
            _pubSubService = pubSubService;
            _cache = cache;
            _mailer = mailer;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public IActionResult UploadTicket()
        {
            return View();
        }

        [Authorize]
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

                TempData.Keep("SuccessMessage");
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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> TechnicianDashboard()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(email) || !await _firestoreRepository.IsTechnicianAsync(email))
            {
                TempData["ErrorMessage"] = "You are not authorized to access this page.";
                return View(new List<Ticket>()); // return empty list
            }

            var tickets = await _cache.GetCachedTicketsAsync();
            return View(tickets);
        }

        // Retrieve Tickets
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PullTickets()
        {
            var tickets = await _cache.GetCachedTicketsAsync();
            return View("TechnicianDashboard", tickets);
        }

        // Close Tickets
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CloseTicket(string id)
        {
            var tickets = await _cache.GetCachedTicketsAsync();
            var ticket = tickets.FirstOrDefault(t => t.TicketId == id);

            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found in cache.";
                return RedirectToAction("PullTickets");
            }

            ticket.Status = "closed";
            _cache.SaveTicketAsync(ticket);

            TempData["SuccessMessage"] = "Ticket closed.";
            return RedirectToAction("PullTickets");
        }

        // HTTP Function
        [AllowAnonymous]
        [HttpGet("ProcessTickets")] // TESTING PURPOSES
        [HttpPost("ProcessTickets")]
        public async Task<IActionResult> ProcessTickets()
        {
            // Pull all tickets
            var received = await _pubSubService.PullAsync();
            if (!received.Any()) return Ok("No tickets to process.");

            // Convert received tickets to Ticket objects
            //var all = received
            //    .Select(r => (Ticket: _pubSubService.Deserialize(r), AckId: r.AckId))
            //    .ToList();

            //// pull raw messages
            //var received = await _pubSubService.PullAsync();

            // build a list of (Ticket, AckId) but skip nulls
            var all = new List<(Ticket Ticket, string AckId)>();
            foreach (var msg in received)
            {
                // get the raw JSON
                var raw = msg.Message.Data.ToStringUtf8();

                Ticket? t = null;
                try
                {
                    t = JsonConvert.DeserializeObject<Ticket>(raw);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Deserialize failed for ackId {AckId}: {Raw}", msg.AckId, raw);
                }

                if (t == null)
                {
                    _logger.LogWarning("Skipping null ticket for ackId {AckId}. Raw: {Raw}", msg.AckId, raw);
                    continue;
                }

                all.Add((t, msg.AckId));
            }

            var prioritized = new List<(Ticket Ticket, string AckId)>();
            foreach (var p in new[] { "high", "medium", "low" })
            {
                prioritized = all
                    .Where(x => x.Ticket.Priority?.Equals(p, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                if (prioritized.Any())
                    break;
            }

            if (!prioritized.Any())
            {
                // No tickets in expected priorities
                _logger.LogInformation("No tickets matched any priority.");
                await _pubSubService.AcknowledgeAsync(all.Select(x => x.AckId));
                return Ok("No queued tickets in expected priorities, check if submitted tickets have a priority.");
            }

            var toAck = new List<string>();
            foreach (var item in prioritized)
            {
                _cache.SaveTicketAsync(item.Ticket);

                List<User> technicians = _firestoreRepository.GetTechniciansAsync().Result;
                if (!technicians.Any())
                {
                    _logger.LogWarning("No technicians found in Firestore to notify.");
                }
                else
                {
                    foreach (var tech in technicians)
                    {
                        //await _mailer.SendTicketNotificationAsync(item.Ticket, tech.Email);

                        _logger.LogInformation(
                            $"Notified {tech.FirstName} about Ticket {item.Ticket.TicketId} at {DateTime.UtcNow}");
                    }
                }

                toAck.Add(item.AckId);
            }

            await _pubSubService.AcknowledgeAsync(toAck);

            var cached = await _cache.GetCachedTicketsAsync();
            foreach (var t in cached.Where(t =>
                t.Status.Equals("closed", StringComparison.OrdinalIgnoreCase) &&
                t.DateUploaded < DateTime.UtcNow.AddDays(-7)))
            {
                await _cache.RemoveTicketAsync(t.TicketId);
                await _firestoreRepository.UpdateOrAddTicket(t);
            }

            return Ok($"Processed {toAck.Count} tickets.");
        }
    }
}
