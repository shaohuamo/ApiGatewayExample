using System.Text.Encodings.Web;
using IdentityServer.Options;
using Microsoft.Extensions.Options;
using Resend;

namespace IdentityServer.Services;

public sealed class ResendIdentityEmailSender(
    IResend resend,
    IOptions<ResendEmailOptions> options,
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

        var message = CreateEmailConfirmationMessage(_options, email, confirmationLink);

        await resend.EmailSendAsync(message, cancellationToken);

        logger.LogInformation("Sent email confirmation message to {Email}.", email);
    }

    public static EmailMessage CreateEmailConfirmationMessage(
        ResendEmailOptions options,
        string email,
        string confirmationLink)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationLink);

        if (string.IsNullOrWhiteSpace(options.From))
        {
            throw new InvalidOperationException("ResendEmail:From must be configured.");
        }

        var subject = string.IsNullOrWhiteSpace(options.ConfirmationSubject)
            ? "Confirm your MicroservicesDemo account"
            : options.ConfirmationSubject;

        var encodedLink = HtmlEncoder.Default.Encode(confirmationLink);

        return new EmailMessage
        {
            From = options.From,
            Subject = subject,
            HtmlBody = $"""
                <p>Thanks for registering with MicroservicesDemo.</p>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href="{encodedLink}">Confirm email</a></p>
                <p>If you did not create this account, you can ignore this email.</p>
                """,
            TextBody = $"""
                Thanks for registering with MicroservicesDemo.

                Confirm your email address by opening this link:
                {confirmationLink}

                If you did not create this account, you can ignore this email.
                """
        }.AddRecipient(email);
    }
}

internal static class EmailMessageExtensions
{
    public static EmailMessage AddRecipient(this EmailMessage message, string email)
    {
        message.To.Add(email);
        return message;
    }
}
