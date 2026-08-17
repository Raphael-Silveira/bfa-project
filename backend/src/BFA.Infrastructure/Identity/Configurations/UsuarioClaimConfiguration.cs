using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Identity.Configurations;

public sealed class UsuarioClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("usuario_claims");

        builder.HasKey(claim => claim.Id)
            .HasName("pk_usuario_claims");

        builder.Property(claim => claim.Id)
            .HasColumnName("id")
            .HasColumnType("integer")
            .UseIdentityByDefaultColumn();

        builder.Property(claim => claim.UserId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(claim => claim.ClaimType)
            .HasColumnName("tipo")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(claim => claim.ClaimValue)
            .HasColumnName("valor")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(claim => claim.UserId)
            .HasDatabaseName("ix_usuario_claims_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(claim => claim.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usuario_claims_usuarios_usuario_id");
    }
}
