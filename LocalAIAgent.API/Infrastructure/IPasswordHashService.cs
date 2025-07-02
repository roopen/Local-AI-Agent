namespace LocalAIAgent.API.Infrastructure
{
    public interface IPasswordHashService
    {
        string Hash(string password);
        bool Verify(string passwordHash, string password);
    }
}