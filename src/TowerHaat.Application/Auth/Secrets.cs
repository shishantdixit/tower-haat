using System.Security.Cryptography;
using System.Text;

namespace TowerHaat.Application.Auth;

internal static class Secrets
{
    public static string NewOtpCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string NewRefreshToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string HashOtp(string hashKey, string phone, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(hashKey), Encoding.UTF8.GetBytes($"{phone}:{code}")));

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool HashesMatch(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
