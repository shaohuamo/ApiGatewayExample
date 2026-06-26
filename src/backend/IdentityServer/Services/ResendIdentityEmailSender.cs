using System.Text.Encodings.Web;
using IdentityServer.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using Resend;

namespace IdentityServer.Services;

public sealed class ResendIdentityEmailSender(
    IResend resend,
    IOptions<ResendEmailOptions> options,
    IStringLocalizer<SharedResource> localizer,
    ILogger<ResendIdentityEmailSender> logger) : IIdentityEmailSender
{
    private readonly ResendEmailOptions _options = options.Value;

    public async Task SendEmailConfirmationAsync(
        string email,
        string confirmationLink,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationLink);

        var message = CreateEmailConfirmationMessage(_options, email, confirmationLink, localizer);

        await resend.EmailSendAsync(message, cancellationToken);

        logger.LogInformation("Sent email confirmation message to {Email}.", email);
    }

    public static EmailMessage CreateEmailConfirmationMessage(
        ResendEmailOptions options,
        string email,
        string confirmationLink,
        IStringLocalizer<SharedResource>? localizer = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationLink);

        if (string.IsNullOrWhiteSpace(options.From))
        {
            throw new InvalidOperationException("ResendEmail:From must be configured.");
        }

        var subject = string.IsNullOrWhiteSpace(options.ConfirmationSubject)
            || options.ConfirmationSubject == "Confirm your MicroservicesDemo account"
            ? Localize(localizer, "Confirm your MicroservicesDemo account")
            : options.ConfirmationSubject;

        var encodedLink = HtmlEncoder.Default.Encode(confirmationLink);
        var htmlIntro = Localize(localizer, "Thanks for registering with MicroservicesDemo.");
        var htmlInstruction = Localize(localizer, "Please confirm your email address by clicking the link below:");
        var htmlButton = Localize(localizer, "Confirm email");
        var htmlIgnore = Localize(localizer, "If you did not create this account, you can ignore this email.");
        var textInstruction = Localize(localizer, "Confirm your email address by opening this link:");

        return new EmailMessage
        {
            From = options.From,
            Subject = subject,
            HtmlBody = $"""
                <p>{htmlIntro}</p>
                <p>{htmlInstruction}</p>
                <p><a href="{encodedLink}">{htmlButton}</a></p>
                <p>{htmlIgnore}</p>
                """,
            TextBody = $"""
                {htmlIntro}

                {textInstruction}
                {confirmationLink}

                {htmlIgnore}
                """
        }.AddRecipient(email);
    }

    private static string Localize(IStringLocalizer<SharedResource>? localizer, string key) =>
        localizer?[key].Value ?? key;
}

internal static class EmailMessageExtensions
{
    public static EmailMessage AddRecipient(this EmailMessage message, string email)
    {
        message.To.Add(email);
        return message;
    }
}
