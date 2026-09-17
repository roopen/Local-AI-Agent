using System.Security.Claims;
using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.API.Metrics;
using LocalAIAgent.Application.News.AI;
using LocalAIAgent.Application.News.Reader;
using LocalAIAgent.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Diagnostics.Metrics;
using Infra = LocalAIAgent.API.Infrastructure.Models;

namespace LocalAIAgent.Tests.UnitTests;

public class NewsControllerReaderTests : InMemoryDbTestBase
{
    [Fact]
    public async Task ResolvesSavedLanguageAndIdentityServerSide()
    {
        Infra.User user = new() { Username = "reader", PasswordHash = "h", Fido2Id = [1],
            Preferences = new() { TargetLanguage = "fi", Prompt = "p", Interests = [], Dislikes = [] } };
        Db.Users.Add(user);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using ServiceProvider provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        NewsController controller = new(Mock.Of<INewsChatUseCase>(), Mock.Of<IGetDatasetUseCase>(),
            new NewsMetrics(provider.GetRequiredService<IMeterFactory>()), Db)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test")) } },
        };
        ReadArticleRequest request = new("https://example.com/article");
        Mock<IReadArticleUseCase> reader = new(MockBehavior.Strict);
        reader.Setup(r => r.ReadAsync(request, user.Id, user.Preferences.Id, "fi", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadArticleResult { Original = new(), TargetLanguage = "fi" });
        ActionResult<ReadArticleResult> response = await controller.ReadArticle(request, reader.Object, TestContext.Current.CancellationToken);
        Assert.IsType<OkObjectResult>(response.Result);
        reader.VerifyAll();

        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<UnauthorizedResult>((await controller.ReadArticle(request, reader.Object, TestContext.Current.CancellationToken)).Result);
    }
}
