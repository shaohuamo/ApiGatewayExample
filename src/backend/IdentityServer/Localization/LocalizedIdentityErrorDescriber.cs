using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace IdentityServer.Localization;

public sealed class LocalizedIdentityErrorDescriber(
    IStringLocalizer<SharedResource> localizer) : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Username '{0}' is already taken.", userName);

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Email '{0}' is already registered.", email);

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Email '{0}' is invalid.", email ?? string.Empty);

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Username '{0}' is invalid.", userName ?? string.Empty);

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Incorrect password.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Passwords must have at least one digit ('0'-'9').");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Passwords must have at least one lowercase letter ('a'-'z').");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Passwords must have at least one non alphanumeric character.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Passwords must have at least one uppercase letter ('A'-'Z').");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), "Passwords must be at least {0} characters.", length);

    private IdentityError Error(string code, string description, params object[] arguments) =>
        new()
        {
            Code = code,
            Description = localizer[description, arguments].Value,
        };
}
