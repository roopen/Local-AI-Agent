namespace LocalAIAgent.API.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string passwordHash, string password);
    }
}