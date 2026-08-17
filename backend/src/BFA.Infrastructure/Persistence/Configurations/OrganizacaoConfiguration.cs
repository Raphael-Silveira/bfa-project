using BFA.Domain.Organizacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class OrganizacaoConfiguration : IEntityTypeConfiguration<Organizacao>
{
    public void Configure(EntityTypeBuilder<Organizacao> builder)
    {
        builder.ToTable("organizacoes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_organizacoes_nome_nao_vazio",
                "btrim(nome) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_organizacoes_slug_nao_vazio",
                "btrim(slug) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_organizacoes_slug_normalizado",
                "slug = lower(btrim(slug))");
        });

        builder.HasKey(organizacao => organizacao.Id)
            .HasName("pk_organizacoes");

        builder.Property(organizacao => organizacao.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(organizacao => organizacao.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(organizacao => organizacao.Slug)
            .HasColumnName("slug")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(organizacao => organizacao.Ativa)
            .HasColumnName("ativa")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(organizacao => organizacao.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(organizacao => organizacao.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(organizacao => organizacao.Slug)
            .HasName("uq_organizacoes_slug");
    }
}
