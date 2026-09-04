using BFA.Domain.Alunos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Turmas;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class AulaConfiguration : IEntityTypeConfiguration<Aula>
{
    public void Configure(EntityTypeBuilder<Aula> builder)
    {
        builder.ToTable("aulas", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_aula");
            tableBuilder.HasCheckConstraint(
                "ck_aulas_status_valido",
                "status IN ('Programada', 'Concluida', 'Cancelada')");
            tableBuilder.HasCheckConstraint(
                "ck_aulas_intervalo_valido",
                "hora_inicio < hora_fim");
            tableBuilder.HasCheckConstraint(
                "ck_aulas_capacidade_valida",
                "capacidade > 0");
        });

        builder.HasKey(aula => aula.Id)
            .HasName("pk_aulas");

        builder.Property(aula => aula.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(aula => aula.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.TurmaId)
            .HasColumnName("turma_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.TurmaHorarioId)
            .HasColumnName("turma_horario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.Data)
            .HasColumnName("data")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(aula => aula.HoraInicio)
            .HasColumnName("hora_inicio")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(aula => aula.HoraFim)
            .HasColumnName("hora_fim")
            .HasColumnType("time without time zone")
            .IsRequired();

        builder.Property(aula => aula.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion(
                status => status.ToString(),
                value => Enum.Parse<StatusAula>(value))
            .IsRequired();

        builder.Property(aula => aula.Capacidade)
            .HasColumnName("capacidade")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(aula => aula.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(aula => aula.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aula => aula.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(aula => aula.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(aula => new
            {
                aula.OrganizacaoId,
                aula.UnidadeId,
                aula.Id
            })
            .HasName("uq_aulas_organizacao_unidade_id");

        builder.HasIndex(aula => new
            {
                aula.OrganizacaoId,
                aula.TurmaId,
                aula.Data,
                aula.HoraInicio
            })
            .IsUnique()
            .HasDatabaseName("uq_aulas_organizacao_turma_data_hora");

        builder.HasIndex(aula => new
            {
                aula.OrganizacaoId,
                aula.UnidadeId,
                aula.Data
            })
            .HasDatabaseName("ix_aulas_organizacao_unidade_data");

        builder.HasIndex(aula => new
            {
                aula.OrganizacaoId,
                aula.TurmaId,
                aula.Data
            })
            .HasDatabaseName("ix_aulas_organizacao_turma_data");

        builder.HasIndex(aula => new
            {
                aula.OrganizacaoId,
                aula.TurmaHorarioId
            })
            .HasDatabaseName("ix_aulas_organizacao_turma_horario");

        builder.HasIndex(aula => aula.CriadoPorUsuarioId)
            .HasDatabaseName("ix_aulas_criado_por_usuario_id");

        builder.HasIndex(aula => aula.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_aulas_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(aula => aula.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(aula => new { aula.OrganizacaoId, aula.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_unidade");

        builder.HasOne<Turma>()
            .WithMany()
            .HasForeignKey(aula => new
            {
                aula.OrganizacaoId,
                aula.UnidadeId,
                aula.TurmaId
            })
            .HasPrincipalKey(turma => new
            {
                turma.OrganizacaoId,
                turma.UnidadeId,
                turma.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_turma");

        builder.HasOne<TurmaHorario>()
            .WithMany()
            .HasForeignKey(aula => new
            {
                aula.OrganizacaoId,
                aula.UnidadeId,
                aula.TurmaHorarioId
            })
            .HasPrincipalKey(horario => new
            {
                horario.OrganizacaoId,
                horario.UnidadeId,
                horario.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_turma_horario");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(aula => aula.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_criado_por_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(aula => aula.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_aulas_atualizado_por_usuario_id");
    }
}
