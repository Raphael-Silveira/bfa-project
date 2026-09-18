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
            tableBuilder.HasCheckConstraint(
                "ck_alunos_cep_formato",
                "cep IS NULL OR cep ~ '^[0-9]{8}$'");
            tableBuilder.HasCheckConstraint(
                "ck_alunos_municipio_estado_consistente",
                "municipio_codigo_ibge IS NULL OR estado_codigo_ibge IS NOT NULL");
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

        builder.Property(aluno => aluno.Apelido)
            .HasColumnName("apelido")
            .HasColumnType("varchar(80)")
            .HasMaxLength(Aluno.ApelidoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Cep)
            .HasColumnName("cep")
            .HasColumnType("varchar(8)")
            .HasMaxLength(Aluno.CepTamanho)
            .IsRequired(false);

        builder.Property(aluno => aluno.EstadoCodigoIbge)
            .HasColumnName("estado_codigo_ibge")
            .HasColumnType("integer")
            .IsRequired(false);

        builder.Property(aluno => aluno.MunicipioCodigoIbge)
            .HasColumnName("municipio_codigo_ibge")
            .HasColumnType("integer")
            .IsRequired(false);

        builder.Property(aluno => aluno.Bairro)
            .HasColumnName("bairro")
            .HasColumnType("varchar(120)")
            .HasMaxLength(Aluno.BairroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Logradouro)
            .HasColumnName("logradouro")
            .HasColumnType("varchar(180)")
            .HasMaxLength(Aluno.LogradouroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Numero)
            .HasColumnName("numero")
            .HasColumnType("varchar(20)")
            .HasMaxLength(Aluno.NumeroTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.Complemento)
            .HasColumnName("complemento")
            .HasColumnType("varchar(120)")
            .HasMaxLength(Aluno.ComplementoTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.FotoPerfilChave)
            .HasColumnName("foto_perfil_chave")
            .HasColumnType("varchar(300)")
            .HasMaxLength(Aluno.FotoPerfilChaveTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.FotoPerfilContentType)
            .HasColumnName("foto_perfil_content_type")
            .HasColumnType("varchar(50)")
            .HasMaxLength(Aluno.FotoPerfilContentTypeTamanhoMaximo)
            .IsRequired(false);

        builder.Property(aluno => aluno.FotoPerfilAtualizadaEmUtc)
            .HasColumnName("foto_perfil_atualizada_em_utc")
            .HasColumnType("timestamp with time zone")
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

        builder.HasOne<BFA.Domain.Localidades.Estado>()
            .WithMany()
            .HasForeignKey(aluno => aluno.EstadoCodigoIbge)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_estado");

        builder.HasOne<BFA.Domain.Localidades.Municipio>()
            .WithMany()
            .HasForeignKey(aluno => aluno.MunicipioCodigoIbge)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_alunos_municipio");
    }
}
