using LocalAIAgent.API.Api.Controllers;
using LocalAIAgent.API.Api.Controllers.Serialization;
using LocalAIAgent.API.Application.UseCases;
using LocalAIAgent.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace LocalAIAgent.Tests.UnitTests;

public class LoginControllerTests
{
    private static LoginController BuildController(IGetUserUseCase useCase, ClaimsPrincipal user)
    {
        LoginController controller = new(useCase);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "TestAuth"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Fact]
    public async Task GetCurrentUser_NotAuthenticated_ReturnsUnauthorized()
    {
        Mock<IGetUserUseCase> useCase = new(MockBehavior.Strict);
        LoginController sut = BuildController(useCase.Object, Anonymous());

        ActionResult<UserDto> result = await sut.GetCurrentUser();

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCurrentUser_AuthenticatedButMissingNameIdentifier_ReturnsBadRequest()
    {
        Mock<IGetUserUseCase> useCase = new(MockBehavior.Strict);
        // Has an authenticated identity but no NameIdentifier claim.
        LoginController sut = BuildController(useCase.Object, Authenticated(new Claim(ClaimTypes.Name, "alice")));

        ActionResult<UserDto> result = await sut.GetCurrentUser();

        Assert.IsType<BadRequestObjectResult>(result.Result);
        useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCurrentUser_NameIdentifierNotInteger_ReturnsBadRequest()
    {
        Mock<IGetUserUseCase> useCase = new(MockBehavior.Strict);
        LoginController sut = BuildController(useCase.Object,
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "not-a-number")));

        ActionResult<UserDto> result = await sut.GetCurrentUser();

        Assert.IsType<BadRequestObjectResult>(result.Result);
        useCase.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCurrentUser_ValidClaimButUserNotInDb_ReturnsNotFound()
    {
        Mock<IGetUserUseCase> useCase = new();
        useCase.Setup(u => u.GetUserById(42)).ReturnsAsync((User?)null);

        LoginController sut = BuildController(useCase.Object,
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "42")));

        ActionResult<UserDto> result = await sut.GetCurrentUser();

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrentUser_ValidClaimAndUserExists_ReturnsUserDto()
    {
        User domainUser = new()
        {
            Id = 42,
            Fido2Id = new byte[] { 1, 2, 3 },
            Username = "alice",
        };
        Mock<IGetUserUseCase> useCase = new();
        useCase.Setup(u => u.GetUserById(42)).ReturnsAsync(domainUser);

        LoginController sut = BuildController(useCase.Object,
            Authenticated(new Claim(ClaimTypes.NameIdentifier, "42")));

        ActionResult<UserDto> result = await sut.GetCurrentUser();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserDto dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal(42, dto.Id);
        Assert.Equal("alice", dto.Username);
    }
}
