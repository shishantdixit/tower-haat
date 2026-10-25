namespace TowerHaat.Application.Abstractions;

/// <summary>
/// Points the tenancy filter at a society chosen by the request rather than the user's own:
/// the sign-up picker (no society yet) and platform admin screens. Everything stays filtered.
/// </summary>
public interface ISocietyScope
{
    void Use(Guid societyId);
}
