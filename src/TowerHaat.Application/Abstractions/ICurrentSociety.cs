namespace TowerHaat.Application.Abstractions;

/// <summary>The society of the current request. Null when there is no signed-in resident.</summary>
public interface ICurrentSociety
{
    Guid? SocietyId { get; }
}
