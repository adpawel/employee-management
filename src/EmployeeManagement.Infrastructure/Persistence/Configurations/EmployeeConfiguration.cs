using EmployeeManagement.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", t => t.HasCheckConstraint(
            "CK_Employees_Status",
            $"[Status] IN ('{EmployeeStatus.Active}', '{EmployeeStatus.Inactive}')"));

        // A random GUID as the clustered key causes page splits, so the table is clustered by CreatedAt instead.
        builder.HasKey(e => e.Id).IsClustered(false);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => e.CreatedAt).IsClustered();

        builder.Property(e => e.Name).HasMaxLength(EmployeeConstraints.NameMaxLength).IsRequired();
        builder.Property(e => e.HireDate).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(EmployeeConstraints.EmailMaxLength).IsRequired();
        builder.Property(e => e.PhoneNo).HasMaxLength(EmployeeConstraints.PhoneNoMaxLength).IsRequired();
        builder.Property(e => e.ProfilePictureUrl).HasMaxLength(EmployeeConstraints.ProfilePictureUrlMaxLength);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(EmployeeConstraints.AddressMaxLength).IsRequired();
        builder.Property(e => e.State).HasMaxLength(EmployeeConstraints.RegionMaxLength).IsRequired();
        builder.Property(e => e.Country).HasMaxLength(EmployeeConstraints.RegionMaxLength).IsRequired();
        builder.Property(e => e.City).HasMaxLength(EmployeeConstraints.RegionMaxLength).IsRequired();
        builder.Property(e => e.Pincode).HasMaxLength(EmployeeConstraints.PincodeMaxLength).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // The app-level check only gives a friendly error; this index is the real guarantee under concurrency.
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
