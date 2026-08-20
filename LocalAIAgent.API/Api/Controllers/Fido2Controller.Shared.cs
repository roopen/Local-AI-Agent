using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;
using System.Security.Claims;

namespace LocalAIAgent.API.Api.Controllers
{
    public partial class Fido2Controller
    {
        private const string _credentialOptionsCacheKey = "fido2.credentialOptions";
        private const string _assertionOptionsCacheKey = "fido2.assertionOptions";
        private const string _userCacheKey = "fido2.user";
        private const string _existingUserCacheKey = "fido2.existingUser";

        private async Task LogIn(User user)
        {
            if (user.IsDisabled)
                throw new UnauthorizedAccessException("User is disabled.");

            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("amr", "mfa"),
                new Claim("amr", "passwordless")
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    // CookieAuthenticationOptions.ExpireTimeSpan defines the ticket lifetime.
                    IsPersistent = true
                });
        }
    }
}
