using BFA.Domain.Franqueados;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class FranqueadoUnidadeConfiguration : IEntityTypeConfiguration<FranqueadoUnidade>
{
    public void Configure(EntityTypeBuilder<FranqueadoUnidade> builder)
    {
        builder.ToTable("franqueados_unidades");

        builder.HasKey(vinculo => vinculo.Id)
            .HasName("pk_franqueados_unidades");

        builder.Property(vinculo => vinculo.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(vinculo => vinculo.FranqueadoId)
            .HasColumnName("franqueado_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
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

        builder.HasIndex(vinculo => vinculo.FranqueadoId)
            .HasDatabaseName("ix_franqueados_unidades_franqueado_id");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.FranqueadoId,
                vinculo.UnidadeId
            })
            .IsUnique()
            .HasDatabaseName("uq_franqueados_unidades_franqueado_unidade");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Ativo
            })
            .HasDatabaseName("ix_franqueados_unidades_organizacao_unidade_ativo");

        builder.HasIndex(vinculo => new { vinculo.OrganizacaoId, vinculo.UnidadeId })
            .IsUnique()
            .HasFilter("ativo = true")
            .HasDatabaseName("uq_franqueados_unidades_unidade_ativa");

        builder.HasOne<Franqueado>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.FranqueadoId })
            .HasPrincipalKey(franqueado => new { franqueado.OrganizacaoId, franqueado.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_unidades_franqueado");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_unidades_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_franqueados_unidades_unidade");
    }
}
