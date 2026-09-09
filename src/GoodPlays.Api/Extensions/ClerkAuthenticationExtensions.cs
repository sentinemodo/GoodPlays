using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GoodPlays.Api.Extensions;

public static class ClerkAuthenticationExtensions
{
    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var clerkSection = configuration.GetSection("Clerk");
        var authority = clerkSection["Authority"];
        var secretKey = clerkSection["SecretKey"];

        if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(secretKey))
        {
            return services;
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.Authority = authority;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = true,
                        NameClaimType = "sub"
                    };
                }
                else
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey!))
                    };
                }
            });

        services.AddAuthorization();
        return services;
    }

    public static bool IsClerkConfigured(IConfiguration configuration)
    {
        var clerkSection = configuration.GetSection("Clerk");
        return !string.IsNullOrWhiteSpace(clerkSection["Authority"])
            || !string.IsNullOrWhiteSpace(clerkSection["SecretKey"]);
    }
}
