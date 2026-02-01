using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Infrastructure;
using LocalAIAgent.API.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LocalAIAgent.API.Application.UseCases
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
            if (context.Users.Any(u => u.Username == request.Username))
            {
                throw new InvalidOperationException("Username already exists.");
            }

            User user = new()
            {
                Fido2Id = GenerateCredentialId(),
                Username = request.Username,
                PasswordHash = passwordHasher.Hash(request.Password),
                Preferences = new UserPreferences()
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static byte[] GenerateCredentialId(int length = 32)
        {
            var credentialId = new byte[length];
            RandomNumberGenerator.Fill(credentialId);
            return credentialId;
        }
    }
}
