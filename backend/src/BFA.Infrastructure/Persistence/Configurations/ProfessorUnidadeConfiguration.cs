using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Domain.Unidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ProfessorUnidadeConfiguration : IEntityTypeConfiguration<ProfessorUnidade>
{
    public void Configure(EntityTypeBuilder<ProfessorUnidade> builder)
    {
        builder.ToTable("professores_unidades", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_estado_professor_unidade");
            tableBuilder.HasTrigger("trg_proteger_professor_unidade_turmas");
        });

        builder.HasKey(vinculo => vinculo.Id)
            .HasName("pk_professores_unidades");

        builder.Property(vinculo => vinculo.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(vinculo => vinculo.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.ProfessorId)
            .HasColumnName("professor_id")
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

        builder.HasAlternateKey(vinculo => new { vinculo.OrganizacaoId, vinculo.Id })
            .HasName("uq_professores_unidades_organizacao_id_id");

        builder.HasAlternateKey(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Id
            })
            .HasName("uq_professores_unidades_organizacao_unidade_id");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.ProfessorId,
                vinculo.UnidadeId
            })
            .IsUnique()
            .HasDatabaseName("uq_professores_unidades_professor_unidade");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.UnidadeId,
                vinculo.Ativo
            })
            .HasDatabaseName("ix_professores_unidades_organizacao_unidade_ativo");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.ProfessorId,
                vinculo.Ativo
            })
            .HasDatabaseName("ix_professores_unidades_organizacao_professor_ativo");

        builder.HasOne<Professor>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.ProfessorId })
            .HasPrincipalKey(professor => new { professor.OrganizacaoId, professor.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_unidades_professor");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_unidades_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_unidades_unidade");
    }
}
