using BFA.Domain.Localidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BFA.Infrastructure.Persistence.Configurations;

public sealed class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> builder)
    {
        builder.ToTable("municipios", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_municipios_codigo_ibge_positivo",
                "codigo_ibge > 0");
            tableBuilder.HasCheckConstraint(
                "ck_municipios_nome_nao_vazio",
                "btrim(nome) <> ''");
        });

        builder.HasKey(municipio => municipio.CodigoIbge)
            .HasName("pk_municipios");

        builder.Property(municipio => municipio.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .HasColumnType("integer")
            .ValueGeneratedNever();

        builder.Property(municipio => municipio.EstadoCodigoIbge)
            .HasColumnName("estado_codigo_ibge")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(municipio => municipio.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(150)")
            .HasMaxLength(Municipio.NomeTamanhoMaximo)
            .IsRequired();

        builder.Property(municipio => municipio.Ativo)
            .HasColumnName("ativo")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(municipio => municipio.CriadoEmUtc)
            .HasColumnName("criado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(municipio => municipio.AtualizadoEmUtc)
            .HasColumnName("atualizado_em_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Estado>()
            .WithMany()
            .HasForeignKey(municipio => municipio.EstadoCodigoIbge)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_municipios_estados_estado_codigo_ibge");

        builder.HasIndex(municipio => new
            {
                municipio.EstadoCodigoIbge,
                municipio.Ativo,
                municipio.Nome,
            })
            .HasDatabaseName("ix_municipios_estado_ativo_nome");
    }
}
