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

    /// <summary>Set when the person deleted their account; the row stays because past orders point to it.</summary>
    public DateTime? DeletedAt { get; private set; }

    public void Rename(string name) => Name = name;

    public void MakePlatformAdmin() => IsPlatformAdmin = true;

    public void Block() => IsBlocked = true;

    public void Unblock() => IsBlocked = false;

    /// <summary>
    /// Forgets the person: name and phone go (the phone is replaced so it can sign up again as new).
    /// Orders keep their own copies of name, phone and flat, as the spec requires.
    /// </summary>
    public void Delete(string phonePlaceholder, DateTime now)
    {
        Phone = phonePlaceholder;
        Name = null;
        IsPlatformAdmin = false;
        DeletedAt = now;
    }
}
