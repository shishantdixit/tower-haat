using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TowerHaat.Application.Abstractions;

namespace TowerHaat.Api.Auth;

internal sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider time) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(AccessTokenClaims claims)
    {
        var jwt = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var expiresAt = now + jwt.AccessTokenLifetime;

        var subject = new List<Claim>
        {
            new(AppClaims.UserId, claims.UserId.ToString()),
            new(AppClaims.Phone, claims.Phone),
        };

        if (claims.SocietyId is { } societyId)
        {
            subject.Add(new Claim(AppClaims.SocietyId, societyId.ToString()));
        }

        if (claims.IsPlatformAdmin)
        {
            subject.Add(new Claim(AppClaims.Role, AppClaims.PlatformAdminRole));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(subject),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey));
}
