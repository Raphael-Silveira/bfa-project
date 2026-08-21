using BFA.Domain.Contratos;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ContratoFranquiaVersaoConfiguration
    : IEntityTypeConfiguration<ContratoFranquiaVersao>
{
    public void Configure(EntityTypeBuilder<ContratoFranquiaVersao> builder)
    {
        builder.ToTable("contratos_franquia_versoes", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_versao_contrato_formalizada");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_numero_positivo",
                "numero_versao >= 1");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_vigencia_valida",
                "data_fim IS NULL OR data_fim >= data_inicio");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_royalties_valido",
                "percentual_royalties >= 0 AND percentual_royalties <= 100");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_mensalidade_valida",
                "mensalidade_fixa >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_taxa_adesao_valida",
                "taxa_adesao IS NULL OR taxa_adesao >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_dia_vencimento_valido",
                "dia_vencimento IS NULL OR dia_vencimento BETWEEN 1 AND 31");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_status_valido",
                "status IN ('Rascunho', 'Vigente', 'Substituida', 'Cancelada')");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_motivo_nao_vazio",
                "motivo_alteracao IS NULL OR btrim(motivo_alteracao) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_contratos_franquia_versoes_observacoes_nao_vazias",
                "observacoes IS NULL OR btrim(observacoes) <> ''");
        });

        builder.HasKey(versao => versao.Id)
            .HasName("pk_contratos_franquia_versoes");

        builder.Property(versao => versao.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(versao => versao.ContratoFranquiaId)
            .HasColumnName("contrato_franquia_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(versao => versao.NumeroVersao)
            .HasColumnName("numero_versao")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(versao => versao.DataInicio)
            .HasColumnName("data_inicio")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(versao => versao.DataFim)
            .HasColumnName("data_fim")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(versao => versao.PercentualRoyalties)
            .HasColumnName("percentual_royalties")
            .HasColumnType("numeric(5,2)")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(versao => versao.MensalidadeFixa)
            .HasColumnName("mensalidade_fixa")
            .HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(versao => versao.TaxaAdesao)
            .HasColumnName("taxa_adesao")
            .HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2)
            .IsRequired(false);

        builder.Property(versao => versao.DiaVencimento)
            .HasColumnName("dia_vencimento")
            .HasColumnType("smallint")
            .HasConversion<short>()
            .IsRequired(false);

        builder.Property(versao => versao.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(30)")
            .HasMaxLength(ContratoFranquiaVersao.StatusTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(versao => versao.MotivoAlteracao)
            .HasColumnName("motivo_alteracao")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(ContratoFranquiaVersao.MotivoAlteracaoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(versao => versao.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("varchar(4000)")
            .HasMaxLength(ContratoFranquiaVersao.ObservacoesTamanhoMaximo)
            .IsRequired(false);

        builder.Property(versao => versao.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(versao => versao.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(versao => new
            {
                versao.ContratoFranquiaId,
                versao.NumeroVersao
            })
            .IsUnique()
            .HasDatabaseName("uq_contratos_franquia_versoes_contrato_numero");

        builder.HasIndex(versao => versao.ContratoFranquiaId)
            .IsUnique()
            .HasFilter("status = 'Vigente'")
            .HasDatabaseName("uq_contratos_franquia_versoes_vigente");

        builder.HasIndex(versao => versao.CriadoPorUsuarioId)
            .HasDatabaseName("ix_contratos_franquia_versoes_criado_por_usuario_id");

        builder.HasOne<ContratoFranquia>()
            .WithMany()
            .HasForeignKey(versao => versao.ContratoFranquiaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contratos_franquia_versoes_contrato_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(versao => versao.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contratos_franquia_versoes_criado_por_usuario_id");
    }
}
