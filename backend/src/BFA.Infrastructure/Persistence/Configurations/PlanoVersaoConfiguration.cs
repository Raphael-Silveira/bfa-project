using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PlanoVersaoConfiguration : IEntityTypeConfiguration<PlanoVersao>
{
    public void Configure(EntityTypeBuilder<PlanoVersao> builder)
    {
        builder.ToTable("planos_versoes", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_plano_versao");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_numero_positivo",
                "numero_versao > 0");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_duracao_positiva",
                "duracao_meses > 0");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_frequencia_valida",
                "frequencia_semanal BETWEEN 1 AND 7");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_valor_mensal_positivo",
                "valor_mensal > 0");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_matricula_valida",
                "(cobra_matricula = true AND valor_matricula IS NOT NULL "
                + "AND valor_matricula > 0) "
                + "OR (cobra_matricula = false AND valor_matricula IS NULL)");
            tableBuilder.HasCheckConstraint(
                "ck_planos_versoes_vigencia_valida",
                "vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
        });

        builder.HasKey(versao => versao.Id)
            .HasName("pk_planos_versoes");

        builder.Property(versao => versao.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(versao => versao.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(versao => versao.PlanoId)
            .HasColumnName("plano_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(versao => versao.NumeroVersao)
            .HasColumnName("numero_versao")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(versao => versao.DuracaoMeses)
            .HasColumnName("duracao_meses")
            .HasColumnType("smallint")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(versao => versao.FrequenciaSemanal)
            .HasColumnName("frequencia_semanal")
            .HasColumnType("smallint")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(versao => versao.ValorMensal)
            .HasColumnName("valor_mensal")
            .HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(versao => versao.CobraMatricula)
            .HasColumnName("cobra_matricula")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(versao => versao.ValorMatricula)
            .HasColumnName("valor_matricula")
            .HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2)
            .IsRequired(false);

        builder.Property(versao => versao.VigenciaInicio)
            .HasColumnName("vigencia_inicio")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(versao => versao.VigenciaFim)
            .HasColumnName("vigencia_fim")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(versao => versao.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(versao => versao.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(versao => new { versao.OrganizacaoId, versao.Id })
            .HasName("uq_planos_versoes_organizacao_id_id");

        builder.HasIndex(versao => new { versao.PlanoId, versao.NumeroVersao })
            .IsUnique()
            .HasDatabaseName("uq_planos_versoes_plano_numero");

        builder.HasIndex(versao => versao.PlanoId)
            .IsUnique()
            .HasFilter("vigencia_fim IS NULL")
            .HasDatabaseName("uq_planos_versoes_aberta");

        builder.HasIndex(versao => new
            {
                versao.OrganizacaoId,
                versao.PlanoId,
                versao.VigenciaInicio,
                versao.VigenciaFim
            })
            .HasDatabaseName("ix_planos_versoes_organizacao_plano_vigencia");

        builder.HasIndex(versao => versao.CriadoPorUsuarioId)
            .HasDatabaseName("ix_planos_versoes_criado_por_usuario_id");

        builder.HasOne<Plano>()
            .WithMany()
            .HasForeignKey(versao => new { versao.OrganizacaoId, versao.PlanoId })
            .HasPrincipalKey(plano => new { plano.OrganizacaoId, plano.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_versoes_plano");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(versao => versao.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_versoes_organizacao");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(versao => versao.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_planos_versoes_criado_por_usuario_id");
    }
}
