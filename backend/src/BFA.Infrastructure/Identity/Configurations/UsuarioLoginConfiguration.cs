using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Identity.Configurations;

public sealed class UsuarioLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("usuario_logins");

        builder.HasKey(login => new { login.LoginProvider, login.ProviderKey })
            .HasName("pk_usuario_logins");

        builder.Property(login => login.LoginProvider)
            .HasColumnName("provedor")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(login => login.ProviderKey)
            .HasColumnName("chave_provedor")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(login => login.ProviderDisplayName)
            .HasColumnName("nome_exibicao_provedor")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(login => login.UserId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(login => login.UserId)
            .HasDatabaseName("ix_usuario_logins_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usuario_logins_usuarios_usuario_id");
    }
}
