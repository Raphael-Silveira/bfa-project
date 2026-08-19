using BFA.Domain.Usuarios;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PerfilUsuarioConfiguration : IEntityTypeConfiguration<PerfilUsuario>
{
    public void Configure(EntityTypeBuilder<PerfilUsuario> builder)
    {
        builder.ToTable("perfis_usuario", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_perfis_usuario_nome_completo_nao_vazio",
                "btrim(nome_completo) <> ''");
        });

        builder.HasKey(perfil => perfil.Id)
            .HasName("pk_perfis_usuario");

        builder.Property(perfil => perfil.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(perfil => perfil.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(perfil => perfil.NomeCompleto)
            .HasColumnName("nome_completo")
            .HasColumnType("varchar(150)")
            .HasMaxLength(PerfilUsuario.NomeCompletoTamanhoMaximo)
            .IsRequired();

        builder.Property(perfil => perfil.Telefone)
            .HasColumnName("telefone")
            .HasColumnType("varchar(30)")
            .HasMaxLength(PerfilUsuario.TelefoneTamanhoMaximo)
            .IsRequired(false);

        builder.Property(perfil => perfil.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(perfil => perfil.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(perfil => perfil.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(perfil => perfil.UsuarioId)
            .IsUnique()
            .HasDatabaseName("uq_perfis_usuario_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithOne()
            .HasForeignKey<PerfilUsuario>(perfil => perfil.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_perfis_usuario_usuarios_usuario_id");
    }
}
