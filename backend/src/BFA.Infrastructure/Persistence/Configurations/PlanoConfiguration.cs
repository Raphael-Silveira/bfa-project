using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PlanoConfiguration : IEntityTypeConfiguration<Plano>
{
    public void Configure(EntityTypeBuilder<Plano> builder)
    {
        builder.ToTable("planos", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_plano");
            tableBuilder.HasCheckConstraint(
                "ck_planos_nome_nao_vazio",
                "btrim(nome) <> ''");
        });

        builder.HasKey(plano => plano.Id)
            .HasName("pk_planos");

        builder.Property(plano => plano.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(plano => plano.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(plano => plano.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(plano => plano.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Plano.NomeTamanhoMaximo)
            .IsRequired();

        builder.Property(plano => plano.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(plano => plano.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(plano => plano.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(plano => plano.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(plano => plano.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(plano => new { plano.OrganizacaoId, plano.Id })
            .HasName("uq_planos_organizacao_id_id");

        builder.HasIndex(plano => new
            {
                plano.OrganizacaoId,
                plano.UnidadeId,
                plano.Ativo
            })
            .HasDatabaseName("ix_planos_organizacao_unidade_ativo");

        builder.HasIndex(plano => plano.CriadoPorUsuarioId)
            .HasDatabaseName("ix_planos_criado_por_usuario_id");

        builder.HasIndex(plano => plano.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_planos_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(plano => plano.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(plano => new { plano.OrganizacaoId, plano.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_unidade");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(plano => plano.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_criado_por_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(plano => plano.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_atualizado_por_usuario_id");
    }
}
