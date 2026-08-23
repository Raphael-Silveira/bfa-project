using BFA.Domain.Organizacoes;
using BFA.Domain.Professores;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class ProfessorConfiguration : IEntityTypeConfiguration<Professor>
{
    public void Configure(EntityTypeBuilder<Professor> builder)
    {
        builder.ToTable("professores", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_inativacao_professor");
            tableBuilder.HasCheckConstraint(
                "ck_professores_nome_completo_nao_vazio",
                "btrim(nome_completo) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_professores_cpf_valido",
                "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
            tableBuilder.HasCheckConstraint(
                "ck_professores_telefone_nao_vazio",
                "telefone IS NULL OR btrim(telefone) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_professores_email_nao_vazio",
                "email IS NULL OR btrim(email) <> ''");
        });

        builder.HasKey(professor => professor.Id)
            .HasName("pk_professores");

        builder.Property(professor => professor.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(professor => professor.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(professor => professor.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(professor => professor.NomeCompleto)
            .HasColumnName("nome_completo")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Professor.NomeCompletoTamanhoMaximo)
            .IsRequired();

        builder.Property(professor => professor.Cpf)
            .HasColumnName("cpf")
            .HasColumnType("varchar(11)")
            .HasMaxLength(Professor.CpfTamanho)
            .IsRequired(false);

        builder.Property(professor => professor.Telefone)
            .HasColumnName("telefone")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Professor.TelefoneTamanhoMaximo)
            .IsRequired(false);

        builder.Property(professor => professor.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(256)")
            .HasMaxLength(Professor.EmailTamanhoMaximo)
            .IsRequired(false);

        builder.Property(professor => professor.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(professor => professor.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(professor => professor.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(professor => new { professor.OrganizacaoId, professor.Id })
            .HasName("uq_professores_organizacao_id_id");

        builder.HasIndex(professor => new { professor.OrganizacaoId, professor.Cpf })
            .IsUnique()
            .HasFilter("cpf IS NOT NULL")
            .HasDatabaseName("uq_professores_organizacao_cpf");

        builder.HasIndex(professor => new { professor.OrganizacaoId, professor.UsuarioId })
            .IsUnique()
            .HasFilter("usuario_id IS NOT NULL")
            .HasDatabaseName("uq_professores_organizacao_usuario");

        builder.HasIndex(professor => new { professor.OrganizacaoId, professor.Ativo })
            .HasDatabaseName("ix_professores_organizacao_ativo");

        builder.HasIndex(professor => professor.UsuarioId)
            .HasDatabaseName("ix_professores_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(professor => professor.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_organizacoes_organizacao_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(professor => professor.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_professores_usuarios_usuario_id");
    }
}
