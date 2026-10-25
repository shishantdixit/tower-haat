using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TowerHaat.Application.Businesses;
using TowerHaat.Application.Catalog;
using TowerHaat.Application.Directory;
using TowerHaat.Domain.Businesses;
using TowerHaat.Tests.Support;

namespace TowerHaat.Tests.Api;

/// <summary>Register, approve, add a menu and browse it, over HTTP.</summary>
public sealed class BusinessFlowTests(TowerHaatFactory factory) : IClassFixture<TowerHaatFactory>
{
    private static readonly Guid Food = BusinessHarness.Food;
    private static readonly Guid HomeKitchen = BusinessHarness.HomeKitchen;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_registers_admin_approves_and_residents_see_the_menu()
    {
        var owner = factory.CreateHttpsClient();
        await factory.SignUpAsync(owner, "+919811100001", "Asha", _ct);

        var types = await owner.GetFromJsonAsync<List<BusinessTypeResponse>>("/api/business-types", _ct);
        Assert.Contains(types!, t => t.Id == Food);

        var registered = await owner.PostAsJsonAsync(
            "/api/my/business",
            new SaveBusinessRequest(Food, HomeKitchen, "Asha's Kitchen", "Ghar ka khana", "asha@okaxis", null, null),
            _ct);
        registered.EnsureSuccessStatusCode();
        var business = (await registered.Content.ReadFromJsonAsync<OwnerBusinessResponse>(HttpSignIn.Json, _ct))!;
        Assert.Contains("\"status\":\"Pending\"", await registered.Content.ReadAsStringAsync(_ct));

        (await owner.PostAsJsonAsync("/api/my/business/items", new SaveItemRequest("Veg thali", null, 120, true, 20, true, false, 0), _ct))
            .EnsureSuccessStatusCode();

        // Customers do not see it until it is approved.
        var customer = factory.CreateHttpsClient();
        await factory.SignUpAsync(customer, "+919811100002", "Ravi", _ct);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/directory/businesses/{business.Id}", _ct)).StatusCode);

        var admin = factory.CreateHttpsClient();
        await factory.SignInAsync(admin, TowerHaatFactory.AdminPhone, _ct);
        var queue = await admin.GetFromJsonAsync<List<AdminBusinessResponse>>("/api/admin/businesses?status=Pending", HttpSignIn.Json, _ct);
        Assert.Contains(queue!, b => b.Id == business.Id && b.OwnerName == "Asha");
        (await admin.PostAsJsonAsync($"/api/admin/businesses/{business.Id}/approve", new BusinessDecisionRequest(null), _ct))
            .EnsureSuccessStatusCode();

        var directory = await customer.GetFromJsonAsync<DirectoryResponse>("/api/directory/businesses", _ct);
        Assert.Contains(directory!.Businesses, b => b.Id == business.Id);
        var page = await customer.GetFromJsonAsync<BusinessPage>($"/api/directory/businesses/{business.Id}", _ct);
        Assert.Equal(("Veg thali", 120m, 20), (page!.Items[0].Name, page.Items[0].Price, page.Items[0].RemainingToday));

        var mine = await owner.GetFromJsonAsync<OwnerBusinessResponse>("/api/my/business", HttpSignIn.Json, _ct);
        Assert.Equal(BusinessStatus.Active, mine!.Status);
    }

    [Fact]
    public async Task Photo_upload_is_served_from_media_with_long_caching()
    {
        var owner = factory.CreateHttpsClient();
        await factory.SignUpAsync(owner, "+919811100003", "Meena", _ct);
        (await owner.PostAsJsonAsync(
            "/api/my/business", new SaveBusinessRequest(Food, HomeKitchen, "Meena's Bakes", null, null, null, null), _ct))
            .EnsureSuccessStatusCode();

        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(TestImages.Jpeg().ToArray());
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "file", "cake.jpg");
        var upload = await owner.PostAsync("/api/my/business/photos", form, _ct);
        upload.EnsureSuccessStatusCode();
        var photo = (await upload.Content.ReadFromJsonAsync<PhotoResponse>(_ct))!;

        var anonymous = factory.CreateHttpsClient();
        var served = await anonymous.GetAsync(photo.Url, _ct);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/jpeg", served.Content.Headers.ContentType?.MediaType);
        Assert.True(served.Headers.CacheControl?.Public);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/media/2026/10/../../secrets.jpg", _ct)).StatusCode);
    }

    [Fact]
    public async Task Owner_and_directory_endpoints_need_a_signed_up_resident()
    {
        var signedIn = factory.CreateHttpsClient();
        await factory.SignInAsync(signedIn, "+919811100004", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await signedIn.GetAsync("/api/directory/businesses", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await signedIn.GetAsync("/api/my/business", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await signedIn.GetAsync("/api/admin/businesses", _ct)).StatusCode);
    }
}
