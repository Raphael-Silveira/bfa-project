using BFA.Domain.Acessos;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class VinculoAcessoConfiguration : IEntityTypeConfiguration<VinculoAcesso>
{
    public void Configure(EntityTypeBuilder<VinculoAcesso> builder)
    {
        builder.ToTable("vinculos_acesso", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_vinculos_acesso_perfil_valido",
                "perfil IN ('AdministradorRede', 'AdministradorUnidade', 'Professor', 'Aluno', 'Responsavel')");
            tableBuilder.HasCheckConstraint(
                "ck_vinculos_acesso_escopo_perfil",
                "(perfil = 'AdministradorRede' AND unidade_id IS NULL) OR "
                + "(perfil <> 'AdministradorRede' AND unidade_id IS NOT NULL)");
        });

        builder.HasKey(vinculo => vinculo.Id)
            .HasName("pk_vinculos_acesso");

        builder.Property(vinculo => vinculo.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(vinculo => vinculo.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid");

        builder.Property(vinculo => vinculo.Perfil)
            .HasColumnName("perfil")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .HasConversion<string>()
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

        builder.HasIndex(vinculo => new { vinculo.UsuarioId, vinculo.Ativo })
            .HasDatabaseName("ix_vinculos_acesso_usuario_id_ativo");

        builder.HasIndex(vinculo => new { vinculo.OrganizacaoId, vinculo.UnidadeId })
            .HasDatabaseName("ix_vinculos_acesso_organizacao_id_unidade_id");

        builder.HasIndex(vinculo => vinculo.UnidadeId)
            .HasDatabaseName("ix_vinculos_acesso_unidade_id");

        builder.HasIndex(vinculo => new
            {
                vinculo.UsuarioId,
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Perfil
            })
            .HasDatabaseName("uq_vinculos_acesso_usuario_organizacao_unidade_perfil")
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vinculos_acesso_usuarios_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vinculos_acesso_organizacoes_organizacao_id");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vinculos_acesso_unidades_organizacao_id_unidade_id");
    }
}
