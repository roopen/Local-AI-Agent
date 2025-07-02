using LocalAIAgent.API.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure.Models;
using LocalAIAgent.API.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace LocalAIAgent.API.Controllers
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

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(UserLoginDto request)
        {
            Domain.User? user = await getUserUseCase.TryLogin(request);

            if (user is null)
                return BadRequest("Invalid credentials.");

            return Ok(new UserDto { Id = user.Id, Username = user.Username });
        }
    }
}