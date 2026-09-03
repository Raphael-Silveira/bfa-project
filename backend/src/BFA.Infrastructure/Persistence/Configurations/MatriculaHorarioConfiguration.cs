using BFA.Domain.Matriculas;
using BFA.Domain.Turmas;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class MatriculaHorarioConfiguration
    : IEntityTypeConfiguration<MatriculaHorario>
{
    public void Configure(EntityTypeBuilder<MatriculaHorario> builder)
    {
        builder.ToTable("matriculas_horarios", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_matricula_horario");
            tableBuilder.HasCheckConstraint(
                "ck_matriculas_horarios_vigencia_valida",
                "vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
        });

        builder.HasKey(item => item.Id).HasName("pk_matriculas_horarios");
        builder.Property(item => item.Id)
            .HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.OrganizacaoId)
            .HasColumnName("organizacao_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.UnidadeId)
            .HasColumnName("unidade_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.MatriculaId)
            .HasColumnName("matricula_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.TurmaHorarioId)
            .HasColumnName("turma_horario_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.VigenciaInicio)
            .HasColumnName("vigencia_inicio").HasColumnType("date").IsRequired();
        builder.Property(item => item.VigenciaFim)
            .HasColumnName("vigencia_fim").HasColumnType("date").IsRequired(false);
        builder.Property(item => item.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.CriadoEmUtc)
            .HasColumnName("criado_em_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasAlternateKey(item => new
            { item.OrganizacaoId, item.UnidadeId, item.Id })
            .HasName("uq_matriculas_horarios_organizacao_unidade_id");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.UnidadeId, item.MatriculaId, item.TurmaHorarioId })
            .IsUnique().HasFilter("vigencia_fim IS NULL")
            .HasDatabaseName("uq_matriculas_horarios_aberto");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.MatriculaId, item.TurmaHorarioId, item.VigenciaInicio })
            .IsUnique().HasDatabaseName("uq_matriculas_horarios_historico");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.UnidadeId, item.MatriculaId })
            .HasDatabaseName("ix_matriculas_horarios_organizacao_unidade_matricula");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.UnidadeId, item.TurmaHorarioId })
            .HasDatabaseName("ix_matriculas_horarios_organizacao_unidade_turma_horario");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.UnidadeId, item.MatriculaId })
            .HasFilter("vigencia_fim IS NULL")
            .HasDatabaseName("ix_matriculas_horarios_abertos_matricula");
        builder.HasIndex(item => new
            { item.OrganizacaoId, item.UnidadeId, item.TurmaHorarioId, item.VigenciaInicio })
            .HasFilter("vigencia_fim IS NULL")
            .HasDatabaseName("ix_matriculas_horarios_abertos_turma_horario");
        builder.HasIndex(item => item.CriadoPorUsuarioId)
            .HasDatabaseName("ix_matriculas_horarios_criado_por_usuario_id");
        builder.HasIndex(item => item.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_matriculas_horarios_atualizado_por_usuario_id");

        builder.HasOne<Matricula>().WithMany()
            .HasForeignKey(item => new
                { item.OrganizacaoId, item.UnidadeId, item.MatriculaId })
            .HasPrincipalKey(matricula => new
                { matricula.OrganizacaoId, matricula.UnidadeId, matricula.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_matriculas_horarios_matricula");
        builder.HasOne<TurmaHorario>().WithMany()
            .HasForeignKey(item => new
                { item.OrganizacaoId, item.UnidadeId, item.TurmaHorarioId })
            .HasPrincipalKey(horario => new
                { horario.OrganizacaoId, horario.UnidadeId, horario.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_matriculas_horarios_turma_horario");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(item => item.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_matriculas_horarios_criado_por_usuario_id");
        builder.HasOne<UsuarioIdentity>().WithMany()
            .HasForeignKey(item => item.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_matriculas_horarios_atualizado_por_usuario_id");
    }
}
