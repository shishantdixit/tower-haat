using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Application.Residents;
using TowerHaat.Domain.Auth;
using TowerHaat.Domain.Users;

namespace TowerHaat.Application.Auth;

/// <summary>Phone + OTP sign-in and refresh-token rotation (C-01).</summary>
public sealed class AuthService(
    IAppDbContext db,
    ISmsSender sms,
    MeService me,
    IOptions<AuthOptions> options,
    TimeProvider time)
{
    private readonly AuthOptions _options = options.Value;

    public async Task<OtpRequestedResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        var phone = NormalizePhone(request.Phone);
        var now = time.GetUtcNow().UtcDateTime;
        var otp = _options.Otp;

        var recent = await db.OtpRequests
            .Where(o => o.Phone == phone && o.CreatedAt > now.AddHours(-1))
            .Select(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        if (recent.Count >= otp.MaxRequestsPerHour)
        {
            var retryAfter = recent.Min().AddHours(1) - now;
            throw new AppException(ErrorKind.TooManyRequests, "Too many codes requested. Please try again later.", retryAfter);
        }

        if (recent.Count > 0 && recent.Max() + otp.ResendCooldown > now)
        {
            var retryAfter = recent.Max() + otp.ResendCooldown - now;
            throw new AppException(
                ErrorKind.TooManyRequests,
                $"Please wait {Math.Ceiling(retryAfter.TotalSeconds)} seconds before requesting another code.",
                retryAfter);
        }

        var code = Secrets.NewOtpCode();
        db.OtpRequests.Add(new OtpRequest(phone, Secrets.HashOtp(otp.HashKey, phone, code), now, now + otp.Lifetime));
        await db.SaveChangesAsync(cancellationToken);

        await sms.SendOtpAsync(phone, code, cancellationToken);

        return new OtpRequestedResponse(
            (int)otp.Lifetime.TotalSeconds,
            (int)otp.ResendCooldown.TotalSeconds,
            otp.ExposeCodeInResponse ? code : null);
    }

    public async Task<AuthResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var phone = NormalizePhone(request.Phone);
        var now = time.GetUtcNow().UtcDateTime;
        var maxAttempts = _options.Otp.MaxAttempts;

        // Only the latest code counts; requesting a new one replaces the old.
        var otp = await db.OtpRequests
            .Where(o => o.Phone == phone)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null || !otp.IsUsable(now, maxAttempts))
        {
            throw new AppException(ErrorKind.Validation, "This code has expired. Please request a new one.");
        }

        var hash = Secrets.HashOtp(_options.Otp.HashKey, phone, (request.Code ?? string.Empty).Trim());
        if (!Secrets.HashesMatch(hash, otp.CodeHash))
        {
            otp.RecordFailedAttempt();
            await db.SaveChangesAsync(cancellationToken);

            var left = maxAttempts - otp.Attempts;
            throw new AppException(
                ErrorKind.Validation,
                left > 0 ? $"Wrong code. {left} attempt{(left == 1 ? "" : "s")} left." : "Wrong code. Please request a new one.");
        }

        otp.Consume(now);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        if (user is null)
        {
            user = new User(phone, now);
            db.Users.Add(user);
        }

        if (user.IsBlocked)
        {
            await db.SaveChangesAsync(cancellationToken);
            throw new AppException(ErrorKind.Forbidden, "This account is blocked. Please contact the TowerHaat team.");
        }

        if (!user.IsPlatformAdmin && _options.AdminPhones.Contains(phone))
        {
            user.MakePlatformAdmin();
        }

        var refresh = NewRefreshToken(user.Id, now, out var refreshToken);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(cancellationToken);

        return await CreateResultAsync(user, refreshToken, refresh.ExpiresAt, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var current = await FindAsync(refreshToken, cancellationToken)
            ?? throw SignInAgain();

        if (!current.IsActive(now))
        {
            if (current.ReplacedById is not null)
            {
                // A rotated token came back: someone else has a copy. End every session of this user.
                await RevokeAllAsync(current.UserId, now, cancellationToken);
            }

            throw SignInAgain();
        }

        var user = await db.Users.SingleAsync(u => u.Id == current.UserId, cancellationToken);
        if (user.IsBlocked)
        {
            await RevokeAllAsync(user.Id, now, cancellationToken);
            throw SignInAgain();
        }

        var next = NewRefreshToken(user.Id, now, out var nextToken);
        current.ReplaceWith(next, now);
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(cancellationToken);

        return await CreateResultAsync(user, nextToken, next.ExpiresAt, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        var token = await FindAsync(refreshToken, cancellationToken);
        if (token is not null)
        {
            token.Revoke(time.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalizePhone(string? input) =>
        PhoneNumber.TryNormalize(input, out var phone)
            ? phone
            : throw new AppException(ErrorKind.Validation, "Enter a valid 10-digit mobile number.");

    private static AppException SignInAgain() => new(ErrorKind.Unauthorized, "Please sign in again.");

    private RefreshToken NewRefreshToken(Guid userId, DateTime now, out string token)
    {
        token = Secrets.NewRefreshToken();
        return new RefreshToken(userId, Secrets.HashToken(token), now, now + _options.RefreshTokenLifetime);
    }

    private async Task<RefreshToken?> FindAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        var hash = Secrets.HashToken(refreshToken);
        return await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private async Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        active.ForEach(t => t.Revoke(now));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> CreateResultAsync(
        User user, string refreshToken, DateTime refreshExpiresAt, CancellationToken cancellationToken)
    {
        var (access, profile) = await me.IssueAccessTokenAsync(user, cancellationToken);
        return new AuthResult(access.Token, access.ExpiresAt, refreshToken, refreshExpiresAt, profile);
    }
}
