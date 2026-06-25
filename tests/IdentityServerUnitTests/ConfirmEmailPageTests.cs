using FluentAssertions;
using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using ConfirmEmailPage = IdentityServer.Pages.Account.ConfirmEmail.Index;

namespace IdentityServerUnitTests;

public sealed class ConfirmEmailPageTests
{
    #region OnGet

    [Fact]
    public async Task OnGet_WhenTokenIsValid_ConfirmsEmail()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "new-user",
            Email = "new-user@example.com"
        };
        var token = "token with +/=";
        var encodedToken = EmailConfirmationLinkFactory.EncodeToken(token);

        userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(x => x.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        userManager.Setup(x => x.ConfirmEmailAsync(user, token)).ReturnsAsync(IdentityResult.Success);

        var page = new ConfirmEmailPage(userManager.Object, PageModelTestHelpers.Localizer);

        await page.OnGet("user-1", encodedToken, "/connect/authorize");

        page.View.IsSuccess.Should().BeTrue();
        page.View.ReturnUrl.Should().Be("/connect/authorize");
        page.View.Email.Should().Be("new-user@example.com");
        userManager.Verify(x => x.ConfirmEmailAsync(user, token), Times.Once);
    }

    [Fact]
    public async Task OnGet_WhenTokenIsInvalid_ShowsFailure()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "new-user",
            Email = "new-user@example.com"
        };

        userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        userManager.Setup(x => x.IsEmailConfirmedAsync(user)).ReturnsAsync(false);

        var page = new ConfirmEmailPage(userManager.Object, PageModelTestHelpers.Localizer);

        await page.OnGet("user-1", "not valid base64", "/connect/authorize");

        page.View.IsSuccess.Should().BeFalse();
        page.View.Message.Should().Contain("invalid");
        userManager.Verify(x => x.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    #endregion
}
