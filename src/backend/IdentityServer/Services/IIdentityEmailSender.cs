namespace IdentityServer.Services;

public interface IIdentityEmailSender
{
    Task SendEmailConfirmationAsync(
        string email,
        string confirmationLink,
        CancellationToken cancellationToken = default);
}
