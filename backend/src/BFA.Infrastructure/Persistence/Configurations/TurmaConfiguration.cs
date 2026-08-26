using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Domain.Turmas;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class TurmaConfiguration : IEntityTypeConfiguration<Turma>
{
    public void Configure(EntityTypeBuilder<Turma> builder)
    {
        builder.ToTable("turmas", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_estado_turma");
            tableBuilder.HasCheckConstraint(
                "ck_turmas_nome_nao_vazio",
                "btrim(nome) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_turmas_capacidade_valida",
                "capacidade > 0");
        });

        builder.HasKey(turma => turma.Id)
            .HasName("pk_turmas");

        builder.Property(turma => turma.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(turma => turma.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(turma => turma.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(turma => turma.ProfessorUnidadeId)
            .HasColumnName("professor_unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(turma => turma.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Turma.NomeTamanhoMaximo)
            .IsRequired();

        builder.Property(turma => turma.Capacidade)
            .HasColumnName("capacidade")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(turma => turma.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(turma => turma.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(turma => turma.AtualizadoPorUsuarioId)
            .HasColumnName("atualizado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(turma => turma.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(turma => turma.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(turma => new
            {
                turma.OrganizacaoId,
                turma.UnidadeId,
                turma.Id
            })
            .HasName("uq_turmas_organizacao_unidade_id");

        builder.HasIndex(turma => new
            {
                turma.OrganizacaoId,
                turma.UnidadeId,
                turma.Ativo
            })
            .HasDatabaseName("ix_turmas_organizacao_unidade_ativo");

        builder.HasIndex(turma => new
            {
                turma.OrganizacaoId,
                turma.ProfessorUnidadeId,
                turma.Ativo
            })
            .HasDatabaseName("ix_turmas_organizacao_professor_unidade_ativo");

        builder.HasIndex(turma => turma.CriadoPorUsuarioId)
            .HasDatabaseName("ix_turmas_criado_por_usuario_id");

        builder.HasIndex(turma => turma.AtualizadoPorUsuarioId)
            .HasDatabaseName("ix_turmas_atualizado_por_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(turma => turma.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_organizacoes_organizacao_id");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(turma => new { turma.OrganizacaoId, turma.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_unidade");

        builder.HasOne<ProfessorUnidade>()
            .WithMany()
            .HasForeignKey(turma => new
            {
                turma.OrganizacaoId,
                turma.UnidadeId,
                turma.ProfessorUnidadeId
            })
            .HasPrincipalKey(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_professor_unidade");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(turma => turma.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_criado_por_usuario_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(turma => turma.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_turmas_atualizado_por_usuario_id");
    }
}
