using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Mapping;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalAIAgent.API.Application.UseCases
{
    public interface IGetUserUseCase
    {
        Task<bool> UsernameExists(string username);

        Task<Domain.User?> TryLogin(UserLoginDto request);

        Task<Domain.User?> GetUserById(int userId);
        Task<Domain.User?> GetUserByName(string username);
    }

    internal sealed class GetUserUseCase(
        UserContext context,
        IPasswordHashService passwordHashUseCase) : IGetUserUseCase
    {
        public async Task<bool> UsernameExists(string username)
        {
            return await context.Users.AsNoTracking().AnyAsync(u => u.Username == username);
        }

        public async Task<Domain.User?> TryLogin(UserLoginDto request)
        {
            User? user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !passwordHashUseCase.Verify(user.PasswordHash, request.Password))
            {
                return null;
            }

            return user?.MapToDomainUser();
        }

        public async Task<Domain.User?> GetUserById(int userId)
        {
            User? user = await context.Users.AsNoTracking().Include(u => u.Preferences).FirstOrDefaultAsync(u => u.Id == userId);

            return user?.MapToDomainUser();
        }

        public Task<Domain.User?> GetUserByName(string username)
        {
            User? user = context.Users.AsNoTracking().Include(u => u.Preferences).FirstOrDefault(u => u.Username == username);

            return Task.FromResult(user?.MapToDomainUser());
        }
    }
}
