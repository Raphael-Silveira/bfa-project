using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class AlunoResponsavelConfiguration : IEntityTypeConfiguration<AlunoResponsavel>
{
    public void Configure(EntityTypeBuilder<AlunoResponsavel> builder)
    {
        builder.ToTable("alunos_responsaveis", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_aluno_responsavel");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_responsaveis_tipo_relacao_valido",
                "tipo_relacao IN ('Pai', 'Mae', 'ResponsavelLegal', 'Tutor', 'Avo', 'Outro')");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_responsaveis_descricao_relacao_valida",
                "(tipo_relacao = 'Outro' AND descricao_relacao IS NOT NULL "
                + "AND btrim(descricao_relacao) <> '') OR "
                + "(tipo_relacao <> 'Outro' AND descricao_relacao IS NULL)");
        });

        builder.HasKey(vinculo => vinculo.Id)
            .HasName("pk_alunos_responsaveis");

        builder.Property(vinculo => vinculo.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(vinculo => vinculo.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.AlunoId)
            .HasColumnName("aluno_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.ResponsavelId)
            .HasColumnName("responsavel_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(vinculo => vinculo.TipoRelacao)
            .HasColumnName("tipo_relacao")
            .HasColumnType("varchar(30)")
            .HasMaxLength(AlunoResponsavel.TipoRelacaoTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(vinculo => vinculo.DescricaoRelacao)
            .HasColumnName("descricao_relacao")
            .HasColumnType("varchar(100)")
            .HasMaxLength(AlunoResponsavel.DescricaoRelacaoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(vinculo => vinculo.PrincipalContato)
            .HasColumnName("principal_contato")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(vinculo => vinculo.ResponsavelFinanceiro)
            .HasColumnName("responsavel_financeiro")
            .HasColumnType("boolean")
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
            .HasName("uq_alunos_responsaveis_organizacao_id_id");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.AlunoId,
                vinculo.ResponsavelId
            })
            .IsUnique()
            .HasDatabaseName("uq_alunos_responsaveis_aluno_responsavel");

        builder.HasIndex(vinculo => new { vinculo.OrganizacaoId, vinculo.AlunoId })
            .IsUnique()
            .HasFilter("principal_contato = true AND ativo = true")
            .HasDatabaseName("uq_alunos_responsaveis_principal_ativo");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.AlunoId,
                vinculo.Ativo
            })
            .HasDatabaseName("ix_alunos_responsaveis_organizacao_aluno_ativo");

        builder.HasIndex(vinculo => new
            {
                vinculo.OrganizacaoId,
                vinculo.ResponsavelId,
                vinculo.Ativo
            })
            .HasDatabaseName("ix_alunos_responsaveis_organizacao_responsavel_ativo");

        builder.HasOne<Aluno>()
            .WithMany()
            .HasForeignKey(vinculo => new { vinculo.OrganizacaoId, vinculo.AlunoId })
            .HasPrincipalKey(aluno => new { aluno.OrganizacaoId, aluno.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_responsaveis_aluno");

        builder.HasOne<Responsavel>()
            .WithMany()
            .HasForeignKey(vinculo => new
                {
                    vinculo.OrganizacaoId,
                    vinculo.ResponsavelId
                })
            .HasPrincipalKey(responsavel => new
                {
                    responsavel.OrganizacaoId,
                    responsavel.Id
                })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_responsaveis_responsavel");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_responsaveis_organizacao");
    }
}
