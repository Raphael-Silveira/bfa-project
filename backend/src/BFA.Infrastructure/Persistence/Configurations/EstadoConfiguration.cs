using BFA.Domain.Localidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class EstadoConfiguration : IEntityTypeConfiguration<Estado>
{
    public void Configure(EntityTypeBuilder<Estado> builder)
    {
        builder.ToTable("estados", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_estados_codigo_ibge_positivo",
                "codigo_ibge > 0");
            tableBuilder.HasCheckConstraint(
                "ck_estados_sigla_formato",
                "sigla ~ '^[A-Z]{2}$'");
            tableBuilder.HasCheckConstraint(
                "ck_estados_nome_nao_vazio",
                "btrim(nome) <> ''");
        });

        builder.HasKey(estado => estado.CodigoIbge)
            .HasName("pk_estados");

        builder.Property(estado => estado.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .HasColumnType("integer")
            .ValueGeneratedNever();

        builder.Property(estado => estado.Sigla)
            .HasColumnName("sigla")
            .HasColumnType("varchar(2)")
            .HasMaxLength(Estado.SiglaTamanho)
            .IsRequired();

        builder.Property(estado => estado.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(100)")
            .HasMaxLength(Estado.NomeTamanhoMaximo)
            .IsRequired();

        builder.Property(estado => estado.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(estado => estado.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(estado => estado.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasAlternateKey(estado => estado.Sigla)
            .HasName("uq_estados_sigla");
    }
}
