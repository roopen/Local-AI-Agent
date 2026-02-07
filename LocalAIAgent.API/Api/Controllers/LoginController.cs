using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
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
        IGetUserUseCase getUserUseCase) : ControllerBase
    {
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
    }
}