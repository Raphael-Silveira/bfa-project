using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ProfessorRemuneracaoConfiguration
    : IEntityTypeConfiguration<ProfessorRemuneracao>
{
    public void Configure(EntityTypeBuilder<ProfessorRemuneracao> builder)
    {
        builder.ToTable("professores_remuneracoes", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_remuneracao_professor");
            tableBuilder.HasCheckConstraint(
                "ck_professores_remuneracoes_modalidade_valida",
                "modalidade IN ('Mensal', 'PorAula', 'PorHora')");
            tableBuilder.HasCheckConstraint(
                "ck_professores_remuneracoes_valor_valido",
                "valor >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_professores_remuneracoes_vigencia_valida",
                "vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
            tableBuilder.HasCheckConstraint(
                "ck_professores_remuneracoes_observacao_nao_vazia",
                "observacao IS NULL OR btrim(observacao) <> ''");
        });

        builder.HasKey(remuneracao => remuneracao.Id)
            .HasName("pk_professores_remuneracoes");

        builder.Property(remuneracao => remuneracao.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(remuneracao => remuneracao.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(remuneracao => remuneracao.ProfessorUnidadeId)
            .HasColumnName("professor_unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(remuneracao => remuneracao.Modalidade)
            .HasColumnName("modalidade")
            .HasColumnType("varchar(30)")
            .HasMaxLength(ProfessorRemuneracao.ModalidadeTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(remuneracao => remuneracao.Valor)
            .HasColumnName("valor")
            .HasColumnType("numeric(12,2)")
            .HasPrecision(12, 2)
            .IsRequired();

        builder.Property(remuneracao => remuneracao.VigenciaInicio)
            .HasColumnName("vigencia_inicio")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(remuneracao => remuneracao.VigenciaFim)
            .HasColumnName("vigencia_fim")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(remuneracao => remuneracao.Observacao)
            .HasColumnName("observacao")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(ProfessorRemuneracao.ObservacaoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(remuneracao => remuneracao.CriadoPorUsuarioId)
            .HasColumnName("criado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(remuneracao => remuneracao.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(remuneracao => remuneracao.ProfessorUnidadeId)
            .IsUnique()
            .HasFilter("vigencia_fim IS NULL")
            .HasDatabaseName("uq_professores_remuneracoes_aberta");

        builder.HasIndex(remuneracao => new
            {
                remuneracao.OrganizacaoId,
                remuneracao.ProfessorUnidadeId,
                remuneracao.VigenciaInicio
            })
            .IsUnique()
            .HasDatabaseName("uq_professores_remuneracoes_vigencia_inicio");

        builder.HasIndex(remuneracao => remuneracao.CriadoPorUsuarioId)
            .HasDatabaseName("ix_professores_remuneracoes_criado_por_usuario_id");

        builder.HasOne<ProfessorUnidade>()
            .WithMany()
            .HasForeignKey(remuneracao => new
            {
                remuneracao.OrganizacaoId,
                remuneracao.ProfessorUnidadeId
            })
            .HasPrincipalKey(vinculo => new { vinculo.OrganizacaoId, vinculo.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_remuneracoes_professor_unidade");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(remuneracao => remuneracao.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_remuneracoes_organizacao");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(remuneracao => remuneracao.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_remuneracoes_criado_por_usuario_id");
    }
}
