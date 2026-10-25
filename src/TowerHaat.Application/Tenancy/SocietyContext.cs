using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;

namespace TowerHaat.Application.Tenancy;

/// <summary>
/// The society for one request: the user's own (from their token) unless an endpoint
/// scopes it with <see cref="Use"/>. Registered once per request as both interfaces.
/// </summary>
public sealed class SocietyContext : ICurrentSociety, ISocietyScope
{
    private Guid? _home;
    private bool _canUseAnySociety;
    private Guid? _scoped;

    public Guid? SocietyId => _scoped ?? _home;

    public void SetHome(Guid? societyId, bool canUseAnySociety)
    {
        _home = societyId;
        _canUseAnySociety = canUseAnySociety;
    }

    public void Use(Guid societyId)
    {
        if (_home is not null && _home != societyId && !_canUseAnySociety)
        {
            throw new AppException(ErrorKind.Forbidden, "You can only use your own society.");
        }

        if (_scoped is not null && _scoped != societyId)
        {
            throw new InvalidOperationException("A request can be scoped to one society only.");
        }

        _scoped = societyId;
    }
}
