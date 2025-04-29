using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PFTC_CLASS_SOLUTION.Controllers
{
    public class AccountController : Controller
    {
        [Authorize]
        public IActionResult Index()
        {
            // this is only for logged in users
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        //[HttpGet("login-google")]
        //public IActionResult LoginWithGoogle()
        //{
        //    var redirectUrl = Url.Action("GoogleResponse", "Account");
        //    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        //    return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        //}

        //[HttpGet("signin-google")]
        //public async Task<IActionResult> GoogleResponse()
        //{
        //    var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        //    if (!result.Succeeded)
        //        return RedirectToAction("Index", "Home");

        //    return RedirectToAction("Index", "Account");
        //}
    }
}
