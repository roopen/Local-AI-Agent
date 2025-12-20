using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LocalAIAgent.SemanticKernel.News.AI
{
    public interface ILoadLLMUseCase
    {
        Task<bool> LoadLLMUseCaseAsync();
    }

    internal class LoadLLMUseCase(
        [FromKeyedServices("General")] IChatCompletionService chatCompletion,
        Kernel kernel) : ILoadLLMUseCase
    {
        public async Task<bool> LoadLLMUseCaseAsync()
        {
            try
            {
                await chatCompletion.GetChatMessageContentAsync(string.Empty, kernel: kernel);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
