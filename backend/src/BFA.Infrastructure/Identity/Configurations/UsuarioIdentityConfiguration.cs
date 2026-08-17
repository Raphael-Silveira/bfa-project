using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Identity.Configurations;

public sealed class UsuarioIdentityConfiguration : IEntityTypeConfiguration<UsuarioIdentity>
{
    public void Configure(EntityTypeBuilder<UsuarioIdentity> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(usuario => usuario.Id)
            .HasName("pk_usuarios");

        builder.Property(usuario => usuario.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedOnAdd();

        builder.Property(usuario => usuario.UserName)
            .HasColumnName("nome_usuario")
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(usuario => usuario.NormalizedUserName)
            .HasColumnName("nome_usuario_normalizado")
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(usuario => usuario.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(usuario => usuario.NormalizedEmail)
            .HasColumnName("email_normalizado")
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(usuario => usuario.EmailConfirmed)
            .HasColumnName("email_confirmado")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(usuario => usuario.PasswordHash)
            .HasColumnName("hash_senha")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(usuario => usuario.SecurityStamp)
            .HasColumnName("selo_seguranca")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(usuario => usuario.ConcurrencyStamp)
            .HasColumnName("selo_concorrencia")
            .HasColumnType("text")
            .IsConcurrencyToken()
            .IsRequired(false);

        builder.Property(usuario => usuario.PhoneNumber)
            .HasColumnName("telefone")
            .HasColumnType("varchar(256)")
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(usuario => usuario.PhoneNumberConfirmed)
            .HasColumnName("telefone_confirmado")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(usuario => usuario.TwoFactorEnabled)
            .HasColumnName("dois_fatores_habilitado")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(usuario => usuario.LockoutEnd)
            .HasColumnName("fim_bloqueio")
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(usuario => usuario.LockoutEnabled)
            .HasColumnName("bloqueio_habilitado")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(usuario => usuario.AccessFailedCount)
            .HasColumnName("contagem_falhas_acesso")
            .HasColumnType("integer")
            .IsRequired();

        builder.HasIndex(usuario => usuario.NormalizedUserName)
            .HasDatabaseName("ix_usuarios_nome_usuario_normalizado")
            .IsUnique();

        builder.HasIndex(usuario => usuario.NormalizedEmail)
            .HasDatabaseName("ix_usuarios_email_normalizado");
    }
}
