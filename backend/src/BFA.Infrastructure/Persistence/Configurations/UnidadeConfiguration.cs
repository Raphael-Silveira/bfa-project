using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        builder.ToTable("unidades", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_unidades_nome_nao_vazio",
                "btrim(nome) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_unidades_slug_nao_vazio",
                "btrim(slug) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_unidades_slug_normalizado",
                "slug = lower(btrim(slug))");
        });

        builder.HasKey(unidade => unidade.Id)
            .HasName("pk_unidades");

        builder.Property(unidade => unidade.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(unidade => unidade.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(unidade => unidade.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(unidade => unidade.Slug)
            .HasColumnName("slug")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(unidade => unidade.Ativa)
            .HasColumnName("ativa")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(unidade => unidade.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(unidade => unidade.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .HasName("uq_unidades_organizacao_id_id");

        builder.HasIndex(unidade => new { unidade.OrganizacaoId, unidade.Slug })
            .IsUnique()
            .HasDatabaseName("uq_unidades_organizacao_id_slug");

        builder.HasIndex(unidade => unidade.OrganizacaoId)
            .HasDatabaseName("ix_unidades_organizacao_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(unidade => unidade.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_unidades_organizacoes_organizacao_id");
    }
}
