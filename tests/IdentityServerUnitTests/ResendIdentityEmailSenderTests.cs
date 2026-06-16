using FluentAssertions;
using IdentityServer.Options;
using IdentityServer.Services;

namespace IdentityServerUnitTests;

public sealed class ResendIdentityEmailSenderTests
{
    #region CreateEmailConfirmationMessage

    [Fact]
    public void CreateEmailConfirmationMessage_BuildsConfirmationEmail()
    {
        var options = new ResendEmailOptions
        {
            From = "MicroservicesDemo <no-reply@example.com>",
            ConfirmationSubject = "Confirm your account"
        };
        const string confirmationLink = "https://identity.example.com/Account/ConfirmEmail?code=abc";

        var message = ResendIdentityEmailSender.CreateEmailConfirmationMessage(
            options,
            "new-user@example.com",
            confirmationLink);

        message.From.ToString().Should().Be("MicroservicesDemo <no-reply@example.com>");
        message.Subject.Should().Be("Confirm your account");
        message.HtmlBody.Should().Contain("Confirm email");
        message.HtmlBody.Should().Contain("https://identity.example.com/Account/ConfirmEmail?code=abc");
        message.TextBody.Should().Contain(confirmationLink);
    }

    #endregion
}
