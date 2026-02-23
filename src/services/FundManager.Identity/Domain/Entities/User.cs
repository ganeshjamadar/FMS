using FundManager.BuildingBlocks.Domain;

namespace FundManager.Identity.Domain.Entities;

/// <summary>
/// Platform user identified by phone or email (passwordless OTP login).
/// At least one of Phone or Email is required (FR-001).
/// </summary>
public class User : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? ProfilePictureUrl { get; private set; }
    public bool IsActive { get; private set; } = true;

    private User() { } // EF Core

    public static User Create(string name, string? phone, string? email, string? profilePictureUrl = null)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("At least one of phone or email is required (FR-001).");

        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Phone = phone,
            Email = email,
            ProfilePictureUrl = profilePictureUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string? name, string? phone, string? email, string? profilePictureUrl)
    {
        if (name is not null) Name = name;
        if (phone is not null || email is not null)
        {
            // Ensure at least one contact remains
            var newPhone = phone ?? Phone;
            var newEmail = email ?? Email;
            if (string.IsNullOrWhiteSpace(newPhone) && string.IsNullOrWhiteSpace(newEmail))
                throw new InvalidOperationException("At least one of phone or email must remain set.");
            Phone = phone ?? Phone;
            Email = email ?? Email;
        }
        if (profilePictureUrl is not null) ProfilePictureUrl = profilePictureUrl;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
