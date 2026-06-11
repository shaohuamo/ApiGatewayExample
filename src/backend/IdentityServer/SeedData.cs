using System.Security.Claims;
using IdentityModel;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Serilog;

namespace IdentityServer;

public class SeedData
{
    public static void EnsureSeedData(WebApplication app)
    {
        using var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var postgresOptions = scope.ServiceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
        var seedUserOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedUserOptions>>().Value;

        var retryPolicy = Policy
            .Handle<Exception>(IsTransientDatabaseException)
            .WaitAndRetry(
                retryCount: postgresOptions.MaxRetryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Min(
                        Math.Pow(2, retryAttempt),
                        postgresOptions.MaxRetryDelaySeconds)),
                onRetry: (exception, delay, retryAttempt, _) =>
                {
                    Log.Warning(
                        exception,
                        "Transient IdentityServer database initialization error. Retrying in {DelaySeconds}s. Attempt {RetryAttempt}/{MaxRetryCount}.",
                        delay.TotalSeconds,
                        retryAttempt,
                        postgresOptions.MaxRetryCount);
                });

        retryPolicy.Execute(() =>
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();

            if (!seedUserOptions.Enabled)
            {
                Log.Information("IdentityServer seed user creation is disabled.");
                return;
            }

            if (string.IsNullOrWhiteSpace(seedUserOptions.UserName)
                || string.IsNullOrWhiteSpace(seedUserOptions.Email)
                || string.IsNullOrWhiteSpace(seedUserOptions.Password))
            {
                throw new InvalidOperationException(
                    "Seed user creation is enabled, but SeedUser:UserName, SeedUser:Email, and SeedUser:Password must all be configured.");
            }

            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = userMgr.FindByNameAsync(seedUserOptions.UserName).Result;
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = seedUserOptions.UserName,
                    Email = seedUserOptions.Email,
                    EmailConfirmed = true,
                };
                var result = userMgr.CreateAsync(user, seedUserOptions.Password).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = userMgr.AddClaimsAsync(user, new Claim[]{
                            new Claim(JwtClaimTypes.Name, seedUserOptions.UserName),
                            new Claim(JwtClaimTypes.PreferredUserName, seedUserOptions.UserName),
                        }).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Information("IdentityServer seed user {UserName} created.", seedUserOptions.UserName);
            }
            else
            {
                var claims = userMgr.GetClaimsAsync(user).Result;
                var missingClaims = new[]
                    {
                        new Claim(JwtClaimTypes.Name, seedUserOptions.UserName),
                        new Claim(JwtClaimTypes.PreferredUserName, seedUserOptions.UserName),
                    }
                    .Where(claim => !claims.Any(existingClaim =>
                        existingClaim.Type == claim.Type && existingClaim.Value == claim.Value))
                    .ToArray();

                if (missingClaims.Length > 0)
                {
                    var result = userMgr.AddClaimsAsync(user, missingClaims).Result;
                    if (!result.Succeeded)
                    {
                        throw new Exception(result.Errors.First().Description);
                    }
                }

                Log.Information("IdentityServer seed user {UserName} already exists.", seedUserOptions.UserName);
            }
        });
    }

    private static bool IsTransientDatabaseException(Exception exception)
    {
        return exception is NpgsqlException
            or TimeoutException
            or OperationCanceledException
            || exception is InvalidOperationException invalidOperationException
                && invalidOperationException.Message.Contains("transient", StringComparison.OrdinalIgnoreCase);
    }
}
