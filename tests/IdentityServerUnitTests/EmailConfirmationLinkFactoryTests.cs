using FluentAssertions;
using IdentityServer.Models;
using IdentityServer.Options;
using IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;

namespace IdentityServerUnitTests;

public sealed class EmailConfirmationLinkFactoryTests
{
    #region CreateEmailConfirmationLink

    [Fact]
    public void CreateEmailConfirmationLink_IncludesReturnUrl()
    {
        var factory = new EmailConfirmationLinkFactory(Options.Create(new ResendEmailOptions
        {
            PublicBaseUrl = "https://auth.example.com"
        }));
        var user = new ApplicationUser { Id = "user-1" };
        var url = new Mock<IUrlHelper>();
        UrlRouteContext? routeContext = null;

        url.SetupGet(x => x.ActionContext).Returns(new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        });
        url.Setup(x => x.RouteUrl(It.IsAny<UrlRouteContext>()))
            .Callback<UrlRouteContext>(context => routeContext = context)
            .Returns("/Account/ConfirmEmail?userId=user-1&code=encoded-token&returnUrl=%2Fconnect%2Fauthorize%2Fcallback%3Fstate%3Dlong-oidc-state");

        var confirmationLink = factory.CreateEmailConfirmationLink(
            url.Object,
            user,
            "confirmation-token",
            "/connect/authorize/callback?state=long-oidc-state");

        confirmationLink.Should().Be("https://auth.example.com/Account/ConfirmEmail?userId=user-1&code=encoded-token&returnUrl=%2Fconnect%2Fauthorize%2Fcallback%3Fstate%3Dlong-oidc-state");

        var routeValues = new RouteValueDictionary(routeContext!.Values);
        routeValues.Should().ContainKey("userId");
        routeValues.Should().ContainKey("code");
        routeValues.Should().ContainKey("returnUrl").WhoseValue.Should().Be("/connect/authorize/callback?state=long-oidc-state");
    }

    #endregion
}
