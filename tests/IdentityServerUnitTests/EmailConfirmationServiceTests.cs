using FluentAssertions;
using IdentityServer.Models;
using IdentityServer.Options;
using IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace IdentityServerUnitTests;

public sealed class EmailConfirmationServiceTests
{
    #region SendConfirmationEmailAsync

    [Fact]
    public async Task SendConfirmationEmailAsync_StoresReturnUrlAndSendsShortContextLink()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var emailSender = new Mock<IIdentityEmailSender>();
        var url = new Mock<IUrlHelper>();
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "new-user@example.com"
        };
        string? storedTokenName = null;
        string? storedTokenValue = null;

        userManager
            .Setup(x => x.GenerateEmailConfirmationTokenAsync(user))
            .ReturnsAsync("confirmation-token");
        userManager
            .Setup(x => x.SetAuthenticationTokenAsync(
                user,
                EmailConfirmationReturnUrlContext.LoginProvider,
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<ApplicationUser, string, string, string>((_, _, tokenName, tokenValue) =>
            {
                storedTokenName = tokenName;
                storedTokenValue = tokenValue;
            })
            .ReturnsAsync(IdentityResult.Success);
        url.SetupGet(x => x.ActionContext).Returns(new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        });
        url.Setup(x => x.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Returns<UrlRouteContext>(context =>
            {
                var values = new RouteValueDictionary(context.Values);
                return $"/Account/ConfirmEmail?userId={values["userId"]}&code={values["code"]}&contextId={values["contextId"]}";
            });

        var service = new EmailConfirmationService(
            userManager.Object,
            new EmailConfirmationLinkFactory(Options.Create(new ResendEmailOptions
            {
                PublicBaseUrl = "https://auth.example.com"
            })),
            emailSender.Object);

        await service.SendConfirmationEmailAsync(
            user,
            "/connect/authorize?client_id=web&state=long-state",
            url.Object);

        storedTokenName.Should().StartWith("return-url:");
        EmailConfirmationReturnUrlContext.TryDeserialize(storedTokenValue, out var storedReturnUrl)
            .Should()
            .BeTrue();
        storedReturnUrl.Should().Be("/connect/authorize?client_id=web&state=long-state");

        emailSender.Verify(x => x.SendEmailConfirmationAsync(
            "new-user@example.com",
            It.Is<string>(link =>
                link.StartsWith("https://auth.example.com/Account/ConfirmEmail?", StringComparison.Ordinal) &&
                link.Contains("contextId=", StringComparison.Ordinal) &&
                !link.Contains("returnUrl=", StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
