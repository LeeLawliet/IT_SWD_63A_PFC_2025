﻿using Google.Cloud.Firestore.V1;
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

                ticket.TicketId = Guid.NewGuid().ToString();
                ticket.ImageUrls = imageUrls;
                ticket.DateUploaded = DateTime.UtcNow;
                ticket.Status = "queued";
                ticket.UserEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "unknown";
                var technicians = await _firestoreRepository.GetTechniciansAsync();
                var technicianEmails = technicians.Select(t => t.Email).ToList();

                foreach (var image in images)
                {
                    if (image != null && image.Length > 0)
                    {
                        var url = await _bucketRepository.UploadImageAsync(image, ticket.UserEmail, technicianEmails); // push images into bucket
                        imageUrls.Add(url);
                    }
                }

                await _pubSubService.PublishTicketAsync(ticket); // push ticket to pubsub

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
            var all = await _cache.GetCachedTicketsAsync();
            var cutoff = DateTime.UtcNow.AddDays(-7);

            // Archive tickets that are closed *and* older than a week
            foreach (var t in all.Where(t =>
                t.DateUploaded < cutoff))
            {
                await _cache.RemoveTicketAsync(t.TicketId);
                await _firestoreRepository.UpdateOrAddTicket(t);
            }

            return View("TechnicianDashboard", all);
        }

        // Retrieve Tickets
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PullTickets()
        {
            var all = await _cache.GetCachedTicketsAsync();
            var cutoff = DateTime.UtcNow.AddDays(-7);

            var filtered = all
                .Where(t => t.DateUploaded >= cutoff)
                .ToList();

            // Archive tickets that are closed and older than 7 days
            foreach (var t in all.Where(t =>
                t.Status.Equals("closed", StringComparison.OrdinalIgnoreCase) &&
                t.DateUploaded < cutoff))
            {
                await _cache.RemoveTicketAsync(t.TicketId);
                await _firestoreRepository.UpdateOrAddTicket(t);
            }

            return View("TechnicianDashboard", filtered);
        }

        // Close Tickets
        [Authorize]
        [HttpPost("CloseTicket/{id}")]
        public async Task<IActionResult> CloseTicket(string id)
        {
            var tickets = await _cache.GetCachedTicketsAsync();
            var ticket = tickets.FirstOrDefault(t => t.TicketId == id);

            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found in cache.";
                return RedirectToAction("TechnicianDashboard");
            }

            ticket.Status = "closed";

            // Setting technician name into 'closedBy' property
            var firstName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
            var lastName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;
            var name = !string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName)
                            ? $"{firstName} {lastName}".Trim()
                            : User.Identity?.Name ?? "Unknown Technician";

            ticket.ClosedBy = name;


            // When closing ticket, check if ticket is older than 1 week.
            if (ticket.DateUploaded < DateTime.UtcNow.AddDays(-7))
            {
                await _cache.RemoveTicketAsync(ticket.TicketId);
                await _firestoreRepository.UpdateOrAddTicket(ticket);
            }
            else
            {
                _cache.SaveTicketAsync(ticket);
            }


            TempData["SuccessMessage"] = "Ticket closed.";
            return RedirectToAction("TechnicianDashboard");
        }
    }
}
