using LocalAIAgent.App.News;

namespace LocalAIAgent.Tests.ArchitecturalTests
{
    public class ArchitectureTests
    {
        [Fact]
        public void All_INewsClientSettings_Implementations_Should_End_With_NewsSettings()
        {
            // Arrange
            Type interfaceType = typeof(INewsClientSettings);

            // Act
            IEnumerable<Type> types = interfaceType.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t));

            // Assert
            foreach (Type? type in types)
            {
                Assert.EndsWith("NewsSettings", type.Name);
            }
        }
    }
}
