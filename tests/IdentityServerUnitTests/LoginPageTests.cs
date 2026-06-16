using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using FluentAssertions;
using IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;
using LoginInputModel = IdentityServer.Pages.Account.Login.InputModel;
using LoginPage = IdentityServer.Pages.Account.Login.Index;

namespace IdentityServerUnitTests;

public sealed class LoginPageTests
{
    #region OnPost

    [Fact]
    public async Task OnPost_WhenEmailIsNotConfirmed_ShowsConfirmationPrompt()
    {
        var userManager = PageModelTestHelpers.CreateUserManager();
        var schemeProvider = new Mock<IAuthenticationSchemeProvider>();
        var signInManager = PageModelTestHelpers.CreateSignInManager(userManager.Object, schemeProvider.Object);
        var interaction = new Mock<IIdentityServerInteractionService>();
        var events = new Mock<IEventService>();
        var identityProviderStore = new Mock<IIdentityProviderStore>();
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "new-user",
            Email = "new-user@example.com"
        };

        interaction
            .Setup(x => x.GetAuthorizationContextAsync("/connect/authorize"))
            .ReturnsAsync((AuthorizationRequest?)null);
        signInManager
            .Setup(x => x.PasswordSignInAsync("new-user", "P@ssw0rd123!", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);
        userManager
            .Setup(x => x.FindByNameAsync("new-user"))
            .ReturnsAsync(user);
        events
            .Setup(x => x.RaiseAsync(It.IsAny<Event>()))
            .Returns(Task.CompletedTask);
        schemeProvider
            .Setup(x => x.GetAllSchemesAsync())
            .ReturnsAsync(Array.Empty<AuthenticationScheme>());
        identityProviderStore
            .Setup(x => x.GetAllSchemeNamesAsync())
            .ReturnsAsync(Array.Empty<IdentityProviderName>());

        var page = new LoginPage(
            interaction.Object,
            schemeProvider.Object,
            identityProviderStore.Object,
            events.Object,
            userManager.Object,
            signInManager.Object)
        {
            Input = new LoginInputModel
            {
                Username = "new-user",
                Password = "P@ssw0rd123!",
                RememberLogin = false,
                ReturnUrl = "/connect/authorize",
                Button = "login"
            }
        };
        PageModelTestHelpers.ConfigurePage(page);

        var result = await page.OnPost();

        result.Should().BeOfType<PageResult>();
        page.View.RequiresEmailConfirmation.Should().BeTrue();
        page.View.UnconfirmedEmail.Should().Be("new-user@example.com");
        page.ModelState[string.Empty]!.Errors.Single().ErrorMessage.Should().Contain("confirm your email");
    }

    #endregion
}
