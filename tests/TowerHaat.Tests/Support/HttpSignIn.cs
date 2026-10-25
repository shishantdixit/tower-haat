using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Residents;
using TowerHaat.Domain.Societies;

namespace TowerHaat.Tests.Support;

internal static class HttpSignIn
{
    /// <summary>Matches the API: enums travel as names.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<AuthResponse> SignInAsync(this TowerHaatFactory factory, HttpClient client, string phone, CancellationToken ct)
    {
        (await client.PostAsJsonAsync("/api/auth/otp", new RequestOtpRequest(phone), ct)).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/auth/verify", new VerifyOtpRequest(phone, factory.Sms.LastCodeFor(phone)), ct);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(ct))!;
        Authorize(client, auth.AccessToken);
        return auth;
    }

    /// <summary>Signs in and signs up in Ace Divino with a typed flat.</summary>
    public static async Task SignUpAsync(this TowerHaatFactory factory, HttpClient client, string phone, string name, CancellationToken ct)
    {
        await factory.SignInAsync(client, phone, ct);
        var saved = await client.PutAsJsonAsync("/api/me/profile", new SaveProfileRequest(name, Society.AceDivinoId, null, "A", phone[^3..], true), ct);
        saved.EnsureSuccessStatusCode();
        Authorize(client, (await saved.Content.ReadFromJsonAsync<SaveProfileResponse>(ct))!.AccessToken);
    }

    public static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
