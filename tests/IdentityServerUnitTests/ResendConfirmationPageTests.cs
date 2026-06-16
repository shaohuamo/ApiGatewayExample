using FluentAssertions;
using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ResendInputModel = IdentityServer.Pages.Account.ResendConfirmation.InputModel;
using ResendPage = IdentityServer.Pages.Account.ResendConfirmation.Index;

namespace IdentityServerUnitTests;

public sealed class ResendConfirmationPageTests
{
    #region OnPost

    [Fact]
    public async Task OnPost_WhenUserExistsAndEmailIsNotConfirmed_SendsConfirmationEmail()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var emailConfirmationService = new Mock<IEmailConfirmationService>();
        var emailVerificationRateLimiter = new Mock<IEmailVerificationRateLimiter>();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "new-user",
            Email = "new-user@example.com"
        };

        userManager.Setup(x => x.FindByEmailAsync("new-user@example.com")).ReturnsAsync(user);
        userManager.Setup(x => x.IsEmailConfirmedAsync(user)).ReturnsAsync(false);
        emailVerificationRateLimiter
            .Setup(x => x.CheckResendAsync(
                It.IsAny<HttpContext>(),
                "new-user@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationRateLimitResult.Allowed);
        emailConfirmationService
            .Setup(x => x.SendConfirmationEmailAsync(
                user,
                "/connect/authorize",
                It.IsAny<IUrlHelper>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var page = new ResendPage(
            userManager.Object,
            emailConfirmationService.Object,
            emailVerificationRateLimiter.Object,
            NullLogger<ResendPage>.Instance)
        {
            Input = new ResendInputModel
            {
                Email = "new-user@example.com",
                ReturnUrl = "/connect/authorize"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        result.Should().BeOfType<PageResult>();
        page.EmailSent.Should().BeTrue();
        emailConfirmationService.Verify(x => x.SendConfirmationEmailAsync(
            user,
            "/connect/authorize",
            It.IsAny<IUrlHelper>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPost_WhenUserDoesNotExist_DoesNotRevealAccountState()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var emailConfirmationService = new Mock<IEmailConfirmationService>();
        var emailVerificationRateLimiter = new Mock<IEmailVerificationRateLimiter>();

        userManager
            .Setup(x => x.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        emailVerificationRateLimiter
            .Setup(x => x.CheckResendAsync(
                It.IsAny<HttpContext>(),
                "missing@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationRateLimitResult.Allowed);

        var page = new ResendPage(
            userManager.Object,
            emailConfirmationService.Object,
            emailVerificationRateLimiter.Object,
            NullLogger<ResendPage>.Instance)
        {
            Input = new ResendInputModel
            {
                Email = "missing@example.com",
                ReturnUrl = "/connect/authorize"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        result.Should().BeOfType<PageResult>();
        page.EmailSent.Should().BeTrue();
        emailConfirmationService.Verify(x => x.SendConfirmationEmailAsync(
            It.IsAny<ApplicationUser>(),
            It.IsAny<string?>(),
            It.IsAny<IUrlHelper>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPost_WhenRateLimited_DoesNotRevealAccountStateOrSendEmail()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var emailConfirmationService = new Mock<IEmailConfirmationService>();
        var emailVerificationRateLimiter = new Mock<IEmailVerificationRateLimiter>();

        emailVerificationRateLimiter
            .Setup(x => x.CheckResendAsync(
                It.IsAny<HttpContext>(),
                "new-user@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationRateLimitResult.Limited("resend_email_cooldown"));

        var page = new ResendPage(
            userManager.Object,
            emailConfirmationService.Object,
            emailVerificationRateLimiter.Object,
            NullLogger<ResendPage>.Instance)
        {
            Input = new ResendInputModel
            {
                Email = "new-user@example.com",
                ReturnUrl = "/connect/authorize"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        result.Should().BeOfType<PageResult>();
        page.EmailSent.Should().BeTrue();
        userManager.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        emailConfirmationService.Verify(x => x.SendConfirmationEmailAsync(
            It.IsAny<ApplicationUser>(),
            It.IsAny<string?>(),
            It.IsAny<IUrlHelper>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
