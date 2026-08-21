using BFA.Domain.Contratos;
using BFA.Domain.Franqueados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ContratoFranquiaConfiguration : IEntityTypeConfiguration<ContratoFranquia>
{
    public void Configure(EntityTypeBuilder<ContratoFranquia> builder)
    {
        builder.ToTable("contratos_franquia", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_contrato_franquia");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_status_valido",
                "status IN ('Rascunho', 'Ativo', 'Encerrado', 'Cancelado')");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_numero_nao_vazio",
                "numero IS NULL OR btrim(numero) <> ''");
        });

        builder.HasKey(contrato => contrato.Id)
            .HasName("pk_contratos_franquia");

        builder.Property(contrato => contrato.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(contrato => contrato.FranqueadoUnidadeId)
            .HasColumnName("franqueado_unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(contrato => contrato.Numero)
            .HasColumnName("numero")
            .HasColumnType("varchar(100)")
            .HasMaxLength(ContratoFranquia.NumeroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(contrato => contrato.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(30)")
            .HasMaxLength(ContratoFranquia.StatusTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(contrato => contrato.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(contrato => contrato.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(
                contrato => contrato.FranqueadoUnidadeId,
                "IX_ContratoFranquia_FranqueadoUnidadeId")
            .HasDatabaseName("ix_contratos_franquia_franqueado_unidade_id");

        builder.HasIndex(
                contrato => contrato.FranqueadoUnidadeId,
                "UQ_ContratoFranquia_FranqueadoUnidadeAtivo")
            .IsUnique()
            .HasFilter("status = 'Ativo'")
            .HasDatabaseName("uq_contratos_franquia_franqueado_unidade_ativo");

        builder.HasOne<FranqueadoUnidade>()
            .WithMany()
            .HasForeignKey(contrato => contrato.FranqueadoUnidadeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contratos_franquia_franqueado_unidade_id");
    }
}
