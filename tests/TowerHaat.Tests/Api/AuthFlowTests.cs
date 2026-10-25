using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TowerHaat.Application.Auth;
using TowerHaat.Application.Residents;
using TowerHaat.Application.Societies;
using TowerHaat.Domain.Societies;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

/// <summary>Sign-in, sign-up and admin list management over HTTP, end to end.</summary>
public sealed class AuthFlowTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resident_signs_in_signs_up_refreshes_and_logs_out()
    {
        var admin = factory.CreateHttpsClient();
        await SignInAsync(admin, TowerHaatFactory.AdminPhone);
        var import = await admin.PostAsJsonAsync(
            $"/api/admin/societies/{Society.AceDivinoId}/flats/import", new ImportFlatsRequest("A,101\nA,102"), _ct);
        import.EnsureSuccessStatusCode();

        var client = factory.CreateHttpsClient();
        var signIn = await SignInAsync(client, "+919876511111");
        Assert.Null(signIn.User.Profile);

        // Signed in without a society: tenant data is not reachable yet, the picker is.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/societies", _ct)).StatusCode);
        var societies = await client.GetFromJsonAsync<List<SocietyResponse>>("/api/societies", _ct);
        Assert.Contains(societies!, s => s.Id == Society.AceDivinoId);
        var towers = await client.GetFromJsonAsync<List<TowerResponse>>($"/api/societies/{Society.AceDivinoId}/towers", _ct);
        var flat = towers!.Single(t => t.Name == "A").Flats.Single(f => f.Number == "102");

        var saved = await client.PutAsJsonAsync(
            "/api/me/profile", new SaveProfileRequest("Asha", Society.AceDivinoId, flat.Id, null, null, true), _ct);
        saved.EnsureSuccessStatusCode();
        var profile = (await saved.Content.ReadFromJsonAsync<SaveProfileResponse>(_ct))!;
        Assert.Equal("102", profile.User.Profile!.Flat);
        Authorize(client, profile.AccessToken);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", _ct);
        Assert.Equal("Asha", me!.Name);

        // The refresh cookie keeps the session; the new access token carries the society.
        var refresh = await client.PostAsync("/api/auth/refresh", null, _ct);
        refresh.EnsureSuccessStatusCode();
        var refreshed = (await refresh.Content.ReadFromJsonAsync<AuthResponse>(_ct))!;
        Assert.Equal(Society.AceDivinoId, refreshed.User.Profile!.SocietyId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null, _ct)).StatusCode);
    }

    [Fact]
    public async Task Refresh_token_is_an_http_only_cookie_and_not_in_the_body()
    {
        var client = factory.CreateHttpsClient();
        await RequestOtpAsync(client, "+919876522222");

        var verify = await client.PostAsJsonAsync(
            "/api/auth/verify", new VerifyOtpRequest("+919876522222", factory.Sms.LastCodeFor("+919876522222")), _ct);

        var cookie = Assert.Single(verify.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", await verify.Content.ReadAsStringAsync(_ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Code_is_never_returned_outside_development()
    {
        var client = factory.CreateHttpsClient();

        var response = await RequestOtpAsync(client, "+919876533333");

        Assert.Null(response.DevelopmentCode);
    }

    [Fact]
    public async Task Errors_are_problem_details_with_a_readable_message()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/otp", new RequestOtpRequest("123"), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("10-digit", await response.Content.ReadAsStringAsync(_ct));
    }

    [Fact]
    public async Task Resend_too_soon_returns_429_with_retry_after()
    {
        var client = factory.CreateHttpsClient();
        await RequestOtpAsync(client, "+919876544444");

        var response = await client.PostAsJsonAsync("/api/auth/otp", new RequestOtpRequest("+919876544444"), _ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task Protected_endpoints_need_a_token()
    {
        var client = factory.CreateHttpsClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/societies", _ct)).StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_read_another_societys_towers_through_the_picker()
    {
        var admin = factory.CreateHttpsClient();
        await SignInAsync(admin, TowerHaatFactory.SecondAdminPhone);
        var other = await (await admin.PostAsJsonAsync("/api/admin/societies", new CreateSocietyRequest("Picker Test", "Noida"), _ct))
            .Content.ReadFromJsonAsync<AdminSocietyResponse>(_ct);

        var client = factory.CreateHttpsClient();
        await SignInAsync(client, "+919876555555");
        var towers = await client.GetFromJsonAsync<List<TowerResponse>>($"/api/societies/{Society.AceDivinoId}/towers", _ct);
        var saved = await client.PutAsJsonAsync(
            "/api/me/profile", new SaveProfileRequest("Ravi", Society.AceDivinoId, null, "Z", "9", true), _ct);
        Authorize(client, (await saved.Content.ReadFromJsonAsync<SaveProfileResponse>(_ct))!.AccessToken);

        var response = await client.GetAsync($"/api/societies/{other!.Id}/towers", _ct);

        Assert.NotNull(towers);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<OtpRequestedResponse> RequestOtpAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/auth/otp", new RequestOtpRequest(phone), _ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OtpRequestedResponse>(_ct))!;
    }

    private async Task<AuthResponse> SignInAsync(HttpClient client, string phone)
    {
        await RequestOtpAsync(client, phone);
        var response = await client.PostAsJsonAsync(
            "/api/auth/verify", new VerifyOtpRequest(phone, factory.Sms.LastCodeFor(phone)), _ct);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(_ct))!;
        Authorize(client, auth.AccessToken);
        return auth;
    }

    private static void Authorize(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
