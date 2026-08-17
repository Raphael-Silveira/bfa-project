using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Identity.Configurations;

public sealed class UsuarioTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("usuario_tokens");

        builder.HasKey(token => new { token.UserId, token.LoginProvider, token.Name })
            .HasName("pk_usuario_tokens");

        builder.Property(token => token.UserId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(token => token.LoginProvider)
            .HasColumnName("provedor")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.Name)
            .HasColumnName("nome")
            .HasColumnType("varchar(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.Value)
            .HasColumnName("valor")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usuario_tokens_usuarios_usuario_id");
    }
}
