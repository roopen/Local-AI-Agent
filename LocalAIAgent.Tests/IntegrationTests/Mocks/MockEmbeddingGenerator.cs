using Microsoft.Extensions.AI;

namespace LocalAIAgent.Tests.IntegrationTests.Mocks
{
    internal class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private int _counter = 0;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(value =>
                    {
                        float[] vector = new float[]
                        {
                            (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble(),
                            (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble(),
                            (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble() * (float)Random.Shared.NextDouble()
                        };

                        // Alternate between normal and opposite vectors
                        if (_counter++ % 2 == 1)
                        {
                            vector = vector.Select(v => -v).ToArray(); // Negate the vector
                        }

                        return new Embedding<float>(vector);
                    })
                ));
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            throw new NotImplementedException();
        }
    }
}
