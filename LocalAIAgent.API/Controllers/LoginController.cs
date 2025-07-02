using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly UserContext _context;
        private readonly IPasswordHasher _passwordHasher;

        public LoginController(UserContext context, IPasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(UserRegistrationDto request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("User already exists.");
            }

            var user = new User
            {
                Username = request.Username,
                PasswordHash = _passwordHasher.Hash(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(UserLoginDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
            {
                return BadRequest("Invalid credentials.");
            }

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }
    }
}