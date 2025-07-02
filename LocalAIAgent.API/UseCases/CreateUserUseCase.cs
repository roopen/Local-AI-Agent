using LocalAIAgent.API.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.API.UseCases
{
    public interface ICreateUserUseCase
    {
        Task<User> CreateUser(UserRegistrationDto request);
    }

    public class CreateUserUseCase(
        IPasswordHashService passwordHasher,
        UserContext context) : ICreateUserUseCase
    {
        public async Task<User> CreateUser(UserRegistrationDto request)
        {
            User user = new()
            {
                Username = request.Username,
                PasswordHash = passwordHasher.Hash(request.Password)
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }
    }
}
