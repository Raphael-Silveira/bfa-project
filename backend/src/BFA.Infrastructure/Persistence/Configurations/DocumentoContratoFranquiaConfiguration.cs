using BFA.Domain.Contratos;
using BFA.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class DocumentoContratoFranquiaConfiguration
    : IEntityTypeConfiguration<DocumentoContratoFranquia>
{
    public void Configure(EntityTypeBuilder<DocumentoContratoFranquia> builder)
    {
        builder.ToTable("documentos_contrato_franquia", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_tipo_valido",
                "tipo_documento IN ('Contrato', 'Aditivo', 'Anexo', 'Outro')");
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_nome_nao_vazio",
                "btrim(nome_original) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_chave_nao_vazia",
                "btrim(chave_armazenamento) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_content_type_nao_vazio",
                "btrim(content_type) <> ''");
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_tamanho_positivo",
                "tamanho_bytes > 0");
            tableBuilder.HasCheckConstraint(
                "ck_documentos_contrato_franquia_hash_valido",
                "hash_sha256 IS NULL OR hash_sha256 ~ '^[0-9a-f]{64}$'");
        });

        builder.HasKey(documento => documento.Id)
            .HasName("pk_documentos_contrato_franquia");

        builder.Property(documento => documento.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(documento => documento.ContratoFranquiaVersaoId)
            .HasColumnName("contrato_franquia_versao_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(documento => documento.TipoDocumento)
            .HasColumnName("tipo_documento")
            .HasColumnType("varchar(30)")
            .HasMaxLength(DocumentoContratoFranquia.TipoDocumentoTamanhoMaximo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(documento => documento.NomeOriginal)
            .HasColumnName("nome_original")
            .HasColumnType("varchar(255)")
            .HasMaxLength(DocumentoContratoFranquia.NomeOriginalTamanhoMaximo)
            .IsRequired();

        builder.Property(documento => documento.ChaveArmazenamento)
            .HasColumnName("chave_armazenamento")
            .HasColumnType("varchar(500)")
            .HasMaxLength(DocumentoContratoFranquia.ChaveArmazenamentoTamanhoMaximo)
            .IsRequired();

        builder.Property(documento => documento.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("varchar(100)")
            .HasMaxLength(DocumentoContratoFranquia.ContentTypeTamanhoMaximo)
            .IsRequired();

        builder.Property(documento => documento.TamanhoBytes)
            .HasColumnName("tamanho_bytes")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(documento => documento.HashSha256)
            .HasColumnName("hash_sha256")
            .HasColumnType("varchar(64)")
            .HasMaxLength(DocumentoContratoFranquia.HashSha256Tamanho)
            .IsRequired(false);

        builder.Property(documento => documento.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(documento => documento.EnviadoPorUsuarioId)
            .HasColumnName("enviado_por_usuario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(documento => documento.ContratoFranquiaVersaoId)
            .HasDatabaseName("ix_documentos_contrato_franquia_versao_id");

        builder.HasIndex(documento => documento.ChaveArmazenamento)
            .IsUnique()
            .HasDatabaseName("uq_documentos_contrato_franquia_chave_armazenamento");

        builder.HasIndex(documento => documento.EnviadoPorUsuarioId)
            .HasDatabaseName("ix_documentos_contrato_franquia_enviado_por_usuario_id");

        builder.HasOne<ContratoFranquiaVersao>()
            .WithMany()
            .HasForeignKey(documento => documento.ContratoFranquiaVersaoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_documentos_contrato_franquia_versao_id");

        builder.HasOne<UsuarioIdentity>()
            .WithMany()
            .HasForeignKey(documento => documento.EnviadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_documentos_contrato_franquia_enviado_por_usuario_id");
    }
}
