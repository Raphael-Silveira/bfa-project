using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class TurmaHorarioConfiguration : IEntityTypeConfiguration<TurmaHorario>
{
    public void Configure(EntityTypeBuilder<TurmaHorario> builder)
    {
        builder.ToTable("turmas_horarios", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_turma_horario");
            tableBuilder.HasTrigger("trg_proteger_turma_horario_grade_aberta");
            tableBuilder.HasCheckConstraint(
                "ck_turmas_horarios_dia_semana_valido",
                "dia_semana BETWEEN 1 AND 7");
            tableBuilder.HasCheckConstraint(
                "ck_turmas_horarios_intervalo_valido",
                "hora_inicio < hora_fim");
            tableBuilder.HasCheckConstraint(
                "ck_turmas_horarios_vigencia_valida",
                "vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
        });

        builder.HasKey(horario => horario.Id)
            .HasName("pk_turmas_horarios");

        builder.Property(horario => horario.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(horario => horario.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.TurmaId)
            .HasColumnName("turma_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.ProfessorUnidadeId)
            .HasColumnName("professor_unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.DiaSemana)
            .HasColumnName("dia_semana")
            .HasColumnType("smallint")
            .HasConversion<short>()
            .IsRequired();

        builder.Property(horario => horario.HoraInicio)
            .HasColumnName("hora_inicio")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(horario => horario.HoraFim)
            .HasColumnName("hora_fim")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(horario => horario.VigenciaInicio)
            .HasColumnName("vigencia_inicio")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(horario => horario.VigenciaFim)
            .HasColumnName("vigencia_fim")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(horario => horario.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(horario => horario.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(horario => horario.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(horario => horario.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(horario => new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                horario.Id
            })
            .HasName("uq_turmas_horarios_organizacao_unidade_id");

        builder.HasIndex(horario => new
            {
                horario.OrganizacaoId,
                horario.TurmaId,
                horario.DiaSemana,
                horario.HoraInicio,
                horario.HoraFim,
                horario.VigenciaInicio
            })
            .IsUnique()
            .HasDatabaseName("uq_turmas_horarios_regra");

        builder.HasIndex(horario => new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                horario.DiaSemana,
                horario.Ativo
            })
            .HasDatabaseName("ix_turmas_horarios_organizacao_unidade_dia_ativo");

        builder.HasIndex(horario => new
            {
                horario.OrganizacaoId,
                horario.TurmaId,
                horario.Ativo
            })
            .HasDatabaseName("ix_turmas_horarios_organizacao_turma_ativo");

        builder.HasIndex(horario => new
            {
                horario.OrganizacaoId,
                horario.ProfessorUnidadeId,
                horario.DiaSemana,
                horario.Ativo,
                horario.HoraInicio,
                horario.HoraFim
            })
            .HasDatabaseName("ix_turmas_horarios_conflito_professor");

        builder.HasIndex(horario => horario.CriadoPorUsuarioId)
            .HasDatabaseName("ix_turmas_horarios_criado_por_usuario_id");

        builder.HasIndex(horario => horario.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_turmas_horarios_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(horario => horario.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(horario => new { horario.OrganizacaoId, horario.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_unidade");

        builder.HasOne<Turma>()
            .WithMany()
            .HasForeignKey(horario => new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                horario.TurmaId
            })
            .HasPrincipalKey(turma => new
            {
                turma.OrganizacaoId,
                turma.UnidadeId,
                turma.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_turma");

        builder.HasOne<ProfessorUnidade>()
            .WithMany()
            .HasForeignKey(horario => new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                horario.ProfessorUnidadeId
            })
            .HasPrincipalKey(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_professor_unidade");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(horario => horario.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_criado_por_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(horario => horario.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_horarios_atualizado_por_usuario_id");
    }
}
