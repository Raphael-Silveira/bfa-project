using BFA.Domain.Alunos;
using BFA.Domain.Organizacoes;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class AlunoConfiguration : IEntityTypeConfiguration<Aluno>
{
    public void Configure(EntityTypeBuilder<Aluno> builder)
    {
        builder.ToTable("alunos", tableBuilder =>
        {
            tableBuilder.HasTrigger("trg_proteger_aluno");
            tableBuilder.HasTrigger("trg_proteger_aluno_matriculas");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_nome_completo_nao_vazio",
                "btrim(nome_completo) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_data_nascimento_nao_futura",
                "data_nascimento <= CURRENT_DATE");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_cpf_valido",
                "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_telefone_nao_vazio",
                "telefone IS NULL OR btrim(telefone) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_email_nao_vazio",
                "email IS NULL OR btrim(email) <> ''");
        });

        builder.HasKey(aluno => aluno.Id)
            .HasName("pk_alunos");

        builder.Property(aluno => aluno.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(aluno => aluno.OrganizacaoId)
            .HasColumnName("organizacao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(aluno => aluno.UsuarioId)
            .HasColumnName("usuario_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(aluno => aluno.NomeCompleto)
            .HasColumnName("nome_completo")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Aluno.NomeCompletoTamanhoMaximo)
            .IsRequired();

        builder.Property(aluno => aluno.DataNascimento)
            .HasColumnName("data_nascimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(aluno => aluno.Cpf)
            .HasColumnName("cpf")
            .HasColumnType("varchar(11)")
            .HasMaxLength(Aluno.CpfTamanho)
            .IsRequired(false);

        builder.Property(aluno => aluno.Telefone)
            .HasColumnName("telefone")
            .HasColumnType("varchar(30)")
            .HasMaxLength(Aluno.TelefoneTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Email)
            .HasColumnName("email")
            .HasColumnType("varchar(256)")
            .HasMaxLength(Aluno.EmailTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(aluno => aluno.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(aluno => aluno.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(aluno => new { aluno.OrganizacaoId, aluno.Id })
            .HasName("uq_alunos_organizacao_id_id");

        builder.HasIndex(aluno => new { aluno.OrganizacaoId, aluno.Cpf })
            .IsUnique()
            .HasFilter("cpf IS NOT NULL")
            .HasDatabaseName("uq_alunos_organizacao_cpf");

        builder.HasIndex(aluno => new { aluno.OrganizacaoId, aluno.UsuarioId })
            .IsUnique()
            .HasFilter("usuario_id IS NOT NULL")
            .HasDatabaseName("uq_alunos_organizacao_usuario");

        builder.HasIndex(aluno => new { aluno.OrganizacaoId, aluno.Ativo })
            .HasDatabaseName("ix_alunos_organizacao_ativo");

        builder.HasIndex(aluno => aluno.UsuarioId)
            .HasDatabaseName("ix_alunos_usuario_id");

        builder.HasOne<Organizacao>()
            .WithMany()
            .HasForeignKey(aluno => aluno.OrganizacaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_organizacao");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(aluno => aluno.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_usuario");
    }
}
