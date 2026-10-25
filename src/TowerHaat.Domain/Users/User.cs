using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Users;

/// <summary>One account per phone number, shared across societies.</summary>
public class User : Entity
{
    private User()
    {
    }

    public User(string phone, DateTime createdAt)
    {
        Phone = phone;
        CreatedAt = createdAt;
    }

    public string Phone { get; private set; } = null!;

    public string? Name { get; private set; }

    public bool IsPlatformAdmin { get; private set; }

    public bool IsBlocked { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Rename(string name) => Name = name;

    public void MakePlatformAdmin() => IsPlatformAdmin = true;

    public void Block() => IsBlocked = true;

    public void Unblock() => IsBlocked = false;
}
