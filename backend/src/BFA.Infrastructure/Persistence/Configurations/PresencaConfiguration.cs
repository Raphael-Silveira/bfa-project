using BFA.Domain.Alunos;
using BFA.Domain.Aulas;
using BFA.Domain.Matriculas;
using BFA.Domain.Organizacoes;
using BFA.Domain.Unidades;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class PresencaConfiguration : IEntityTypeConfiguration<Presenca>
{
    public void Configure(EntityTypeBuilder<Presenca> builder)
    {
        builder.ToTable("presencas", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_presenca");
            tableBuilder.HasCheckConstraint(
                "ck_presencas_status_valido",
                "status IN ('Presente', 'Ausente', 'Justificado', 'Isento')");
        });

        builder.HasKey(presenca => presenca.Id)
            .HasName("pk_presencas");

        builder.Property(presenca => presenca.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(presenca => presenca.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.UnidadeId)
            .HasColumnName("unidade_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.AulaId)
            .HasColumnName("aula_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.AlunoId)
            .HasColumnName("aluno_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.MatriculaId)
            .HasColumnName("matricula_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion(
                status => status.ToString(),
                value => Enum.Parse<StatusPresenca>(value))
            .IsRequired();

        builder.Property(presenca => presenca.ChegouAs)
            .HasColumnName("chegou_as")
            .HasColumnType("time without time zone")
            .IsRequired(false);

        builder.Property(presenca => presenca.SaiuAs)
            .HasColumnName("saiu_as")
            .HasColumnType("time without time zone")
            .IsRequired(false);

        builder.Property(presenca => presenca.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(presenca => presenca.RegistradoPorUsuarioId)
            .HasColumnName("registrado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(presenca => presenca.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(presenca => presenca.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.UnidadeId,
                presenca.Id
            })
            .HasName("uq_presencas_organizacao_unidade_id");

        builder.HasIndex(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.AulaId,
                presenca.AlunoId
            })
            .IsUnique()
            .HasDatabaseName("uq_presencas_aula_aluno");

        builder.HasIndex(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.AulaId
            })
            .HasDatabaseName("ix_presencas_organizacao_aula");

        builder.HasIndex(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.AlunoId
            })
            .HasDatabaseName("ix_presencas_organizacao_aluno");

        builder.HasIndex(presenca => presenca.RegistradoPorUsuarioId)
            .HasDatabaseName("ix_presencas_registrado_por_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(presenca => presenca.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_organizacao");

        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(presenca => new { presenca.OrganizacaoId, presenca.UnidadeId })
            .HasPrincipalKey(unidade => new { unidade.OrganizacaoId, unidade.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_unidade");

        builder.HasOne<Aula>()
            .WithMany()
            .HasForeignKey(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.UnidadeId,
                presenca.AulaId
            })
            .HasPrincipalKey(aula => new
            {
                aula.OrganizacaoId,
                aula.UnidadeId,
                aula.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_aula");

        builder.HasOne<Aluno>()
            .WithMany()
            .HasForeignKey(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.AlunoId
            })
            .HasPrincipalKey(aluno => new
            {
                aluno.OrganizacaoId,
                aluno.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_aluno");

        builder.HasOne<Matricula>()
            .WithMany()
            .HasForeignKey(presenca => new
            {
                presenca.OrganizacaoId,
                presenca.UnidadeId,
                presenca.MatriculaId
            })
            .HasPrincipalKey(matricula => new
            {
                matricula.OrganizacaoId,
                matricula.UnidadeId,
                matricula.Id
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_matricula");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(presenca => presenca.RegistradoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_presencas_registrado_por_usuario_id");
    }
}
