using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Lee_Xerri_PFC_Home.Repositories;
using System.Security.Claims;

namespace Lee_Xerri_PFC_Home.Controllers
{
    public class AccountController : Controller
    {
        private readonly FirestoreRepository _firestoreRepository;

        public AccountController(FirestoreRepository firestoreRepository)
        {
            _firestoreRepository = firestoreRepository;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("login-google")]
        public IActionResult LoginWithGoogle()
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account");
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return RedirectToAction("Index", "Home");

            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var firstName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value;
            var lastName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value;

            if (!string.IsNullOrEmpty(email))
            {
                var existingUser = await _firestoreRepository.GetUserByEmailAsync(email);

                var user = new Models.User
                {
                    Email = email,
                    FirstName = firstName ?? existingUser?.FirstName ?? "",
                    LastName = lastName ?? existingUser?.LastName ?? "",
                    Role = existingUser?.Role ?? "User" // keep existing role if found
                };

                await _firestoreRepository.UpdateOrAddUser(user);
            }

            return RedirectToAction("Index", "Account");
        }
    }
}
