using LocalAIAgent.Application.Chat;
using Serilog;

namespace LocalAIAgent.Application.News.AI
{
    public interface ILoadLLMUseCase
    {
        Task<bool> LoadLLMUseCaseAsync(CancellationToken cancellationToken = default);
    }

    internal class LoadLLMUseCase(ILlmRuntimeManager runtimeManager) : ILoadLLMUseCase
    {
        public async Task<bool> LoadLLMUseCaseAsync(CancellationToken cancellationToken = default)
        {
            if (!runtimeManager.IsConfigured)
                return false;

            LlmRuntimeSnapshot runtime = runtimeManager.GetRequiredSnapshot();
            try
            {
                await runtimeManager.WarmUpAsync(runtime, cancellationToken);
                Log.Information("AI model {ModelId} warm-up completed", runtime.Options.ModelId);
                return true;
            }
            catch (LlmConnectionException ex)
            {
                Log.Warning("AI model {ModelId} warm-up failed: {Message}",
                    runtime.Options.ModelId,
                    ex.Message);
                return false;
            }
        }
    }
}
