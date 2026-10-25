using TowerHaat.Application.Common;
using TowerHaat.Application.Tenancy;

namespace TowerHaat.Tests.Tenancy;

public sealed class SocietyContextTests
{
    private static readonly Guid Home = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();

    [Fact]
    public void Defaults_to_the_users_own_society()
    {
        var context = new SocietyContext();
        context.SetHome(Home, canUseAnySociety: false);

        Assert.Equal(Home, context.SocietyId);
    }

    [Fact]
    public void A_user_without_a_society_can_scope_to_one()
    {
        var context = new SocietyContext();
        context.SetHome(null, canUseAnySociety: false);

        context.Use(Other);

        Assert.Equal(Other, context.SocietyId);
    }

    [Fact]
    public void A_resident_cannot_scope_to_another_society()
    {
        var context = new SocietyContext();
        context.SetHome(Home, canUseAnySociety: false);

        var error = Assert.Throws<AppException>(() => context.Use(Other));
        Assert.Equal(ErrorKind.Forbidden, error.Kind);
        Assert.Equal(Home, context.SocietyId);
    }

    [Fact]
    public void A_platform_admin_can_scope_to_another_society()
    {
        var context = new SocietyContext();
        context.SetHome(Home, canUseAnySociety: true);

        context.Use(Other);

        Assert.Equal(Other, context.SocietyId);
    }

    [Fact]
    public void A_request_cannot_switch_between_two_scoped_societies()
    {
        var context = new SocietyContext();
        context.SetHome(null, canUseAnySociety: true);
        context.Use(Home);

        Assert.Throws<InvalidOperationException>(() => context.Use(Other));
    }
}
