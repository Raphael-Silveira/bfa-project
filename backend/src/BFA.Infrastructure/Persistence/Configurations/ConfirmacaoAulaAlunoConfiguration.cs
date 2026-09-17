using BFA.Domain.Alunos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ConfirmacaoAulaAlunoConfiguration
    : IEntityTypeConfiguration<ConfirmacaoAulaAluno>
{
    public void Configure(EntityTypeBuilder<ConfirmacaoAulaAluno> builder)
    {
        builder.ToTable("confirmacoes_aula_aluno");
        builder.HasKey(item => item.Id).HasName("pk_confirmacoes_aula_aluno");

        builder.Property(item => item.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(item => item.OrganizacaoId).HasColumnName("organizacao_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.UnidadeId).HasColumnName("unidade_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.AulaId).HasColumnName("aula_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.AlunoId).HasColumnName("aluno_id").HasColumnType("uuid").IsRequired();
        builder.Property(item => item.Ativa).HasColumnName("ativa").HasColumnType("boolean").IsRequired();
        builder.Property(item => item.ConfirmadaEmUtc).HasColumnName("confirmada_em_utc").HasColumnType("timestamp with time zone");
        builder.Property(item => item.CriadoEmUtc).HasColumnName("criado_em_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(item => item.AtualizadoEmUtc).HasColumnName("atualizado_em_utc").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasAlternateKey(item => new { item.OrganizacaoId, item.UnidadeId, item.Id })
            .HasName("uq_confirmacoes_aula_aluno_organizacao_unidade_id");
        builder.HasIndex(item => new { item.OrganizacaoId, item.UnidadeId, item.AulaId, item.AlunoId })
            .IsUnique().HasDatabaseName("uq_confirmacoes_aula_aluno_identidade");
        builder.HasIndex(item => new { item.OrganizacaoId, item.UnidadeId, item.AlunoId })
            .HasDatabaseName("ix_confirmacoes_aula_aluno_aluno");

        builder.HasOne<Organizacao>().WithMany().HasForeignKey(item => item.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_confirmacoes_aula_aluno_organizacao");
        builder.HasOne<Unidade>().WithMany()
            .HasForeignKey(item => new { item.OrganizacaoId, item.UnidadeId })
            .HasPrincipalKey(item => new { item.OrganizacaoId, item.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_confirmacoes_aula_aluno_unidade");
        builder.HasOne<Aula>().WithMany()
            .HasForeignKey(item => new { item.OrganizacaoId, item.UnidadeId, item.AulaId })
            .HasPrincipalKey(item => new { item.OrganizacaoId, item.UnidadeId, item.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_confirmacoes_aula_aluno_aula");
        builder.HasOne<Aluno>().WithMany()
            .HasForeignKey(item => new { item.OrganizacaoId, item.AlunoId })
            .HasPrincipalKey(item => new { item.OrganizacaoId, item.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_confirmacoes_aula_aluno_aluno");
    }
}
