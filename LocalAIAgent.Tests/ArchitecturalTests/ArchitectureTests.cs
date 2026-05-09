using LocalAIAgent.Application;
using LocalAIAgent.Application.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace LocalAIAgent.Tests.ArchitecturalTests
{
    public class ArchitectureTests
    {
        [Fact]
        public void Every_NewsSource_Declares_A_Supported_Language_Code()
        {
            Type interfaceType = typeof(BaseNewsClientSettings);
            List<BaseNewsClientSettings> sources = [.. interfaceType.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
                .Select(t => (BaseNewsClientSettings)Activator.CreateInstance(t)!)];

            Assert.NotEmpty(sources);

            List<string> bad = [.. sources
                .Where(s => string.IsNullOrEmpty(s.Language) || !Languages.IsSupported(s.Language))
                .Select(s => $"{s.GetType().Name} ({s.Language ?? "<null>"})")];

            Assert.True(bad.Count == 0,
                $"News sources with missing or unsupported language codes: {string.Join(", ", bad)}");
        }

        [Fact]
        public void Every_NewsSource_Has_A_NonEmpty_DisplayName()
        {
            Type interfaceType = typeof(BaseNewsClientSettings);
            List<BaseNewsClientSettings> sources = [.. interfaceType.Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
                .Select(t => (BaseNewsClientSettings)Activator.CreateInstance(t)!)];

            List<string> empty = [.. sources
                .Where(s => string.IsNullOrWhiteSpace(s.DisplayName))
                .Select(s => s.GetType().Name)];

            Assert.True(empty.Count == 0,
                $"News sources with empty DisplayName: {string.Join(", ", empty)}");
        }

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
        public void Application_Project_Should_Not_Reference_Other_Projects()
        {
            // Arrange
            Assembly applicationAssembly = typeof(Application.DependencyRegistrar).Assembly;
            AssemblyName[] referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

            // Act
            // Allow references to Domain project, but prevent other LocalAIAgent project references
            List<AssemblyName> forbiddenReferences = referencedAssemblies
                .Where(a => a.Name != applicationAssembly.GetName().Name
                    && a.Name!.StartsWith("LocalAIAgent")
                    && a.Name != "LocalAIAgent.Domain")
                .ToList();

            // Assert
            Assert.Empty(forbiddenReferences);
        }

        [Fact]
        public void Domain_Project_Should_Have_No_LocalAIAgent_Project_References()
        {
            // Arrange
            Assembly domainAssembly = typeof(Domain.User).Assembly;
            AssemblyName[] referencedAssemblies = domainAssembly.GetReferencedAssemblies();

            // Assert
            // Domain is the bottom of the dependency stack — it must depend on nothing else in this solution.
            List<AssemblyName> forbidden = referencedAssemblies
                .Where(a => a.Name != domainAssembly.GetName().Name && a.Name!.StartsWith("LocalAIAgent"))
                .ToList();

            Assert.Empty(forbidden);
        }

        [Fact]
        public void Every_Controller_Class_Has_Explicit_Authorize_Or_AllowAnonymous()
        {
            // Arrange — find every controller in the API assembly.
            Assembly apiAssembly = typeof(API.Program).Assembly;
            List<Type> controllers = [.. apiAssembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))];

            Assert.NotEmpty(controllers);

            // Act — find controllers that lack a class-level auth attribute.
            // Method-level overrides are fine, but the class itself must declare default intent.
            List<string> missing = [.. controllers
                .Where(t => t.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is null
                         && t.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is null)
                .Select(t => t.Name)];

            // Assert — accidental public endpoints are the bug class this guards against.
            Assert.True(missing.Count == 0,
                $"Controllers missing class-level [Authorize] or [AllowAnonymous]: {string.Join(", ", missing)}");
        }

        [Fact]
        public void UseCase_Types_Follow_Naming_Convention()
        {
            // Arrange — scan every loaded LocalAIAgent assembly for use-case types.
            List<Assembly> assemblies =
            [
                typeof(Application.DependencyRegistrar).Assembly,
                typeof(API.Program).Assembly,
            ];
            List<Type> allTypes = [.. assemblies.SelectMany(a => a.GetTypes())];

            // Rule 1: every interface ending in "UseCase" starts with 'I'.
            List<string> badInterfaces = [.. allTypes
                .Where(t => t.IsInterface && t.Name.EndsWith("UseCase", StringComparison.Ordinal) && !t.Name.StartsWith('I'))
                .Select(t => t.FullName ?? t.Name)];

            Assert.True(badInterfaces.Count == 0,
                $"UseCase interfaces missing 'I' prefix: {string.Join(", ", badInterfaces)}");

            // Rule 2: every concrete class implementing an I*UseCase interface ends in "UseCase".
            List<string> badImplementations = [.. allTypes
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i =>
                    i.Name.StartsWith('I')
                    && i.Name.EndsWith("UseCase", StringComparison.Ordinal)
                    && i.Assembly == t.Assembly))
                .Where(t => !t.Name.EndsWith("UseCase", StringComparison.Ordinal))
                .Select(t => t.FullName ?? t.Name)];

            Assert.True(badImplementations.Count == 0,
                $"Use case implementations not ending in 'UseCase': {string.Join(", ", badImplementations)}");
        }
    }
}
