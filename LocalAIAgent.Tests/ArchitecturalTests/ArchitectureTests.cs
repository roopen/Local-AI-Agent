using LocalAIAgent.SemanticKernel.News;

namespace LocalAIAgent.Tests.ArchitecturalTests
{
    public class ArchitectureTests
    {
        [Fact]
        public void All_BaseNewsClientSettings_Implementations_Should_End_With_NewsSettings()
        {
            // Arrange
            Type interfaceType = typeof(BaseNewsClientSettings);

            // Act
            IEnumerable<Type> types = interfaceType.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t));

            // Assert
            foreach (Type? type in types)
            {
                Assert.EndsWith("NewsSettings", type.Name);
            }
        }

        [Fact]
        public void SemanticKernel_Project_Should_Not_Reference_Other_Projects()
        {
            // Arrange
            System.Reflection.Assembly semanticKernelAssembly = typeof(SemanticKernel.DependencyRegistrar).Assembly;
            System.Reflection.AssemblyName[] referencedAssemblies = semanticKernelAssembly.GetReferencedAssemblies();

            // Act
            // Assuming all project assemblies in the solution start with "LocalAIAgent" except "SemanticKernel"
            List<System.Reflection.AssemblyName> forbiddenReferences = referencedAssemblies
                .Where(a => a.Name != semanticKernelAssembly.GetName().Name && a.Name!.StartsWith("LocalAIAgent"))
                .ToList();

            // Assert
            Assert.Empty(forbiddenReferences);
        }

    }
}
