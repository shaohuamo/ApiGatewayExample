using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using FluentAssertions;
using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RegisterInputModel = IdentityServer.Pages.Account.Register.InputModel;
using RegisterPage = IdentityServer.Pages.Account.Register.Index;

namespace IdentityServerUnitTests;

public sealed class RegisterPageTests
{
    #region OnPost

    [Fact]
    public async Task OnPost_WhenRegistrationSucceeds_SendsConfirmationEmailAndRedirectsToConfirmationPage()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var interaction = new Mock<IIdentityServerInteractionService>();
        var emailConfirmationService = new Mock<IEmailConfirmationService>();
        var emailVerificationRateLimiter = new Mock<IEmailVerificationRateLimiter>();
        ApplicationUser? createdUser = null;

        interaction
            .Setup(x => x.GetAuthorizationContextAsync("/connect/authorize"))
            .ReturnsAsync((AuthorizationRequest?)null);
        emailVerificationRateLimiter
            .Setup(x => x.CheckRegisterAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationRateLimitResult.Allowed);
        userManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "P@ssw0rd123!"))
            .Callback<ApplicationUser, string>((user, _) => createdUser = user)
            .ReturnsAsync(IdentityResult.Success);
        emailConfirmationService
            .Setup(x => x.SendConfirmationEmailAsync(
                It.IsAny<ApplicationUser>(),
                "/connect/authorize",
                It.IsAny<IUrlHelper>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var page = new RegisterPage(
            userManager.Object,
            interaction.Object,
            emailConfirmationService.Object,
            emailVerificationRateLimiter.Object,
            NullLogger<RegisterPage>.Instance,
            PageModelTestHelpers.Localizer)
        {
            Input = new RegisterInputModel
            {
                Username = "new-user",
                Email = "new-user@example.com",
                Password = "P@ssw0rd123!",
                ConfirmPassword = "P@ssw0rd123!",
                ReturnUrl = "/connect/authorize"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        createdUser.Should().NotBeNull();
        createdUser!.UserName.Should().Be("new-user");
        createdUser.Email.Should().Be("new-user@example.com");
        createdUser.EmailConfirmed.Should().BeFalse();

        emailConfirmationService.Verify(x => x.SendConfirmationEmailAsync(
            createdUser,
            "/connect/authorize",
            It.IsAny<IUrlHelper>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var redirect = result.Should().BeOfType<RedirectToPageResult>().Subject;
        redirect.PageName.Should().Be("/Account/RegisterConfirmation/Index");
        redirect.RouteValues.Should().ContainKey("email").WhoseValue.Should().Be("new-user@example.com");
        redirect.RouteValues.Should().ContainKey("returnUrl").WhoseValue.Should().Be("/connect/authorize");
    }

    [Fact]
    public async Task OnPost_WhenRegistrationIsRateLimited_DoesNotCreateUserOrSendEmail()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var interaction = new Mock<IIdentityServerInteractionService>();
        var emailConfirmationService = new Mock<IEmailConfirmationService>();
        var emailVerificationRateLimiter = new Mock<IEmailVerificationRateLimiter>();

        interaction
            .Setup(x => x.GetAuthorizationContextAsync("/connect/authorize"))
            .ReturnsAsync((AuthorizationRequest?)null);
        emailVerificationRateLimiter
            .Setup(x => x.CheckRegisterAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationRateLimitResult.Limited("register_ip_window"));

        var page = new RegisterPage(
            userManager.Object,
            interaction.Object,
            emailConfirmationService.Object,
            emailVerificationRateLimiter.Object,
            NullLogger<RegisterPage>.Instance,
            PageModelTestHelpers.Localizer)
        {
            Input = new RegisterInputModel
            {
                Username = "new-user",
                Email = "new-user@example.com",
                Password = "P@ssw0rd123!",
                ConfirmPassword = "P@ssw0rd123!",
                ReturnUrl = "/connect/authorize"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        result.Should().BeOfType<PageResult>();
        page.ModelState.IsValid.Should().BeFalse();
        page.ModelState[string.Empty]!.Errors
            .Should()
            .Contain(error => error.ErrorMessage == "Too many registration attempts. Please try again later.");
        userManager.Verify(
            x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
        emailConfirmationService.Verify(x => x.SendConfirmationEmailAsync(
            It.IsAny<ApplicationUser>(),
            It.IsAny<string?>(),
            It.IsAny<IUrlHelper>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region OnGetIsEmailAvailable

    [Fact]
    public async Task OnGetIsEmailAvailable_WhenEmailDoesNotExist_ReturnsTrue()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var page = CreatePage(userManager.Object);

        userManager
            .Setup(x => x.FindByEmailAsync("new-user@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await page.OnGetIsEmailAvailable("new-user@example.com");

        result.Should().BeOfType<JsonResult>()
            .Subject.Value.Should().Be(true);
    }

    [Fact]
    public async Task OnGetIsEmailAvailable_WhenEmailExists_ReturnsErrorMessage()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "existing-user",
            Email = "existing@example.com"
        };
        var page = CreatePage(userManager.Object);

        userManager
            .Setup(x => x.FindByEmailAsync("existing@example.com"))
            .ReturnsAsync(user);

        var result = await page.OnGetIsEmailAvailable("existing@example.com");

        result.Should().BeOfType<JsonResult>()
            .Subject.Value.Should().Be("Email is already registered.");
    }

    #endregion

    private static RegisterPage CreatePage(UserManager<ApplicationUser> userManager)
    {
        var page = new RegisterPage(
            userManager,
            Mock.Of<IIdentityServerInteractionService>(),
            Mock.Of<IEmailConfirmationService>(),
            Mock.Of<IEmailVerificationRateLimiter>(),
            NullLogger<RegisterPage>.Instance,
            PageModelTestHelpers.Localizer);
        PageModelTestHelpers.ConfigurePage(page);

        return page;
    }
}
