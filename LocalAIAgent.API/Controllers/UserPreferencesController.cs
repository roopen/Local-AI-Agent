using LocalAIAgent.API.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserPreferencesController(UserContext context) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserPreferenceDto>> GetPreferences(int userId)
    {
        UserPreference? preferences = await context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences == null)
        {
            return NotFound();
        }
        return Ok(new UserPreferenceDto { Id = preferences.Id, Prompt = preferences.Prompt, Interests = preferences.Interests, Dislikes = preferences.Dislikes });
    }

    [HttpPost("/api/SavePreferences")]
    public async Task<IActionResult> SavePreferences([FromBody] UserPreferenceDto preferences)
    {
        User? user = await context.Users.Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == preferences.UserId);
        if (user == null)
        {
            return NotFound();
        }

        if (user.Preferences == null)
        {
            user.Preferences = new UserPreference { Prompt = preferences.Prompt, Interests = preferences.Interests, Dislikes = preferences.Dislikes };
            context.UserPreferences.Add(user.Preferences);
        }
        else
        {
            user.Preferences.Prompt = preferences.Prompt;
            user.Preferences.Interests = preferences.Interests;
            user.Preferences.Dislikes = preferences.Dislikes;
        }

        await context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("/api/user")]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserDto userDto)
    {
        if (string.IsNullOrEmpty(userDto.Username))
        {
            return BadRequest("Username is required.");
        }
        User user = new User { Username = userDto.Username };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return Ok(new UserDto { Id = user.Id, Username = user.Username });
    }
}