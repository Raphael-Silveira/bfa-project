using BFA.Domain.Alunos;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Planos;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class MatriculaConfiguration : IEntityTypeConfiguration<Matricula>
{
    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        builder.ToTable("matriculas", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_matricula");
            tableBuilder.HasTrigger("trg_proteger_matricula_grade_aberta");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_status_valido",
                "status IN ('Ativa', 'Encerrada', 'Cancelada')");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_data_fim_prevista_valida",
                "data_fim_prevista >= data_inicio");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_valor_mensal_positivo",
                "valor_mensal_contratado > 0");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_taxa_valida",
                "(cobra_taxa_matricula = true AND valor_taxa_matricula IS NOT NULL "
                + "AND valor_taxa_matricula > 0) OR "
                + "(cobra_taxa_matricula = false AND valor_taxa_matricula IS NULL)");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_status_data_fim_real",
                "(status = 'Ativa' AND data_fim_real IS NULL) OR "
                + "(status IN ('Encerrada', 'Cancelada') AND data_fim_real IS NOT NULL "
                + "AND data_fim_real >= data_inicio)");
        });

        builder.HasKey(matricula => matricula.Id).HasName("pk_matriculas");
        builder.Property(matricula => matricula.Id)
            .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(matricula => matricula.OrganizacaoId)
            .HasColumnName("organizacao_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.UnidadeId)
            .HasColumnName("unidade_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.AlunoId)
            .HasColumnName("aluno_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.PlanoVersaoId)
            .HasColumnName("plano_versao_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.DataInicio)
            .HasColumnName("data_inicio").HasColumnType("date").IsRequired();
        builder.Property(matricula => matricula.DataFimPrevista)
            .HasColumnName("data_fim_prevista").HasColumnType("date").IsRequired();
        builder.Property(matricula => matricula.DataFimReal)
            .HasColumnName("data_fim_real").HasColumnType("date").IsRequired(false);
        builder.Property(matricula => matricula.Status)
            .HasColumnName("status").HasColumnType("varchar(20)").HasMaxLength(20)
            .HasConversion<string>().IsRequired();
        builder.Property(matricula => matricula.ValorMensalContratado)
            .HasColumnName("valor_mensal_contratado").HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2).IsRequired();
        builder.Property(matricula => matricula.CobraTaxaMatricula)
            .HasColumnName("cobra_taxa_matricula").HasColumnType("boolean").IsRequired();
        builder.Property(matricula => matricula.ValorTaxaMatricula)
            .HasColumnName("valor_taxa_matricula").HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2).IsRequired(false);
        builder.Property(matricula => matricula.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(matricula => matricula.CriadoEmUtc)
            .HasColumnName("criado_em_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(matricula => matricula.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasAlternateKey(matricula => new
            { matricula.OrganizacaoId, matricula.UnidadeId, matricula.Id })
            .HasName("uq_matriculas_organizacao_unidade_id");
        builder.HasIndex(matricula => new
            { matricula.OrganizacaoId, matricula.UnidadeId, matricula.Status })
            .HasDatabaseName("ix_matriculas_organizacao_unidade_status");
        builder.HasIndex(matricula => new
            { matricula.OrganizacaoId, matricula.AlunoId, matricula.Status })
            .HasDatabaseName("ix_matriculas_organizacao_aluno_status");
        builder.HasIndex(matricula => new
            { matricula.OrganizacaoId, matricula.UnidadeId, matricula.AlunoId })
            .HasDatabaseName("ix_matriculas_organizacao_unidade_aluno");
        builder.HasIndex(matricula => new
            { matricula.OrganizacaoId, matricula.PlanoVersaoId })
            .HasDatabaseName("ix_matriculas_organizacao_plano_versao");
        builder.HasIndex(matricula => new
            { matricula.OrganizacaoId, matricula.UnidadeId, matricula.AlunoId })
            .IsUnique().HasFilter("status = 'Ativa'")
            .HasDatabaseName("uq_matriculas_ativa_organizacao_unidade_aluno");
        builder.HasIndex(matricula => matricula.CriadoPorUsuarioId)
            .HasDatabaseName("ix_matriculas_criado_por_usuario_id");
        builder.HasIndex(matricula => matricula.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_matriculas_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>().WithMany()
            .HasForeignKey(matricula => matricula.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_organizacao");
        builder.HasOne<Unidade>().WithMany()
            .HasForeignKey(matricula => new { matricula.OrganizacaoId, matricula.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_unidade");
        builder.HasOne<Aluno>().WithMany()
            .HasForeignKey(matricula => new { matricula.OrganizacaoId, matricula.AlunoId })
            .HasPrincipalKey(aluno => new { aluno.OrganizacaoId, aluno.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_aluno");
        builder.HasOne<PlanoVersao>().WithMany()
            .HasForeignKey(matricula => new { matricula.OrganizacaoId, matricula.PlanoVersaoId })
            .HasPrincipalKey(versao => new { versao.OrganizacaoId, versao.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_plano_versao");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(matricula => matricula.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_criado_por_usuario_id");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(matricula => matricula.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_matriculas_atualizado_por_usuario_id");
    }
}
