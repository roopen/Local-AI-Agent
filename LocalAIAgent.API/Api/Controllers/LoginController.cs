using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Infrastructure.Mapping;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace LocalAIAgent.API.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController(
        ICreateUserUseCase createUserUseCase,
        IGetUserUseCase getUserUseCase) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(UserRegistrationDto request)
        {
            if (await getUserUseCase.UsernameExists(request.Username))
                return BadRequest("User already exists.");

            User user = await createUserUseCase.CreateUser(request);
            await LogIn(user.MapToDomainUser());

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(UserLoginDto request)
        {
            Domain.User? user = await getUserUseCase.TryLogin(request);

            if (user is null)
                return BadRequest("Invalid credentials.");

            await LogIn(user);

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok("Logged out successfully.");
        }

        [HttpGet("current")]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            ClaimsPrincipal? user = HttpContext.User;

            if (!user.Identity?.IsAuthenticated ?? true)
                return Unauthorized("User is not authenticated.");

            string? userIdAsString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdAsString))
                return BadRequest("UserId not found in claims.");

            if (!int.TryParse(userIdAsString, NumberStyles.Integer, CultureInfo.InvariantCulture, out int userId))
                return BadRequest("Invalid UserId format.");

            Domain.User? domainUser = await getUserUseCase.GetUserById(userId);

            if (domainUser is null)
                return NotFound("User not found.");

            return Ok(new UserDto { Id = domainUser.Id, Username = domainUser.Username });
        }

        private async Task LogIn(Domain.User user)
        {
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.Value.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, "User")
            ];

            ClaimsIdentity claimsIdentity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(3600)
                });
        }
    }
}