using EmployeeManagement.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    // Lengths come from external standards where one exists (RFC 5321 for email, E.164 for phone numbers).
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 254;
    public const int PhoneNoMaxLength = 16;
    public const int ProfilePictureUrlMaxLength = 2048;
    public const int AddressMaxLength = 500;
    public const int RegionMaxLength = 100;
    public const int PincodeMaxLength = 20;

    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", t => t.HasCheckConstraint(
            "CK_Employees_Status",
            $"[Status] IN ('{EmployeeStatus.Active}', '{EmployeeStatus.Inactive}')"));

        // A random GUID as the clustered key would insert rows at random pages and cause page splits.
        // The table is clustered by insertion time instead; the GUID stays the (non-clustered) primary key.
        builder.HasKey(e => e.Id).IsClustered(false);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => e.CreatedAt).IsClustered();

        builder.Property(e => e.Name).HasMaxLength(NameMaxLength).IsRequired();
        builder.Property(e => e.HireDate).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(EmailMaxLength).IsRequired();
        builder.Property(e => e.PhoneNo).HasMaxLength(PhoneNoMaxLength).IsRequired();
        builder.Property(e => e.ProfilePictureUrl).HasMaxLength(ProfilePictureUrlMaxLength);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(AddressMaxLength).IsRequired();
        builder.Property(e => e.State).HasMaxLength(RegionMaxLength).IsRequired();
        builder.Property(e => e.Country).HasMaxLength(RegionMaxLength).IsRequired();
        builder.Property(e => e.City).HasMaxLength(RegionMaxLength).IsRequired();
        builder.Property(e => e.Pincode).HasMaxLength(PincodeMaxLength).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // The application checks uniqueness first to return a friendly error, but only the database
        // can guarantee it when two requests with the same email arrive concurrently.
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
