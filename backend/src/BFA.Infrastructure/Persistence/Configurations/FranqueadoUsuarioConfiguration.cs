using BFA.Domain.Franqueados;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class FranqueadoUsuarioConfiguration : IEntityTypeConfiguration<FranqueadoUsuario>
{
    public void Configure(EntityTypeBuilder<FranqueadoUsuario> builder)
    {
        builder.ToTable("franqueados_usuarios");

        builder.HasKey(vinculo => vinculo.Id)
            .HasName("pk_franqueados_usuarios");

        builder.Property(vinculo => vinculo.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(vinculo => vinculo.FranqueadoId)
            .HasColumnName("franqueado_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.Principal)
            .HasColumnName("principal")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(vinculo => vinculo.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(vinculo => vinculo.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(vinculo => vinculo.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(vinculo => new { vinculo.FranqueadoId, vinculo.UsuarioId })
            .IsUnique()
            .HasDatabaseName("uq_franqueados_usuarios_franqueado_id_usuario_id");

        builder.HasIndex(vinculo => vinculo.FranqueadoId)
            .IsUnique()
            .HasFilter("principal = true AND ativo = true")
            .HasDatabaseName("uq_franqueados_usuarios_principal_ativo");

        builder.HasIndex(vinculo => vinculo.UsuarioId)
            .HasDatabaseName("ix_franqueados_usuarios_usuario_id");

        builder.HasOne<Franqueado>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.FranqueadoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_usuarios_franqueado_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_usuarios_usuario_id");
    }
}
