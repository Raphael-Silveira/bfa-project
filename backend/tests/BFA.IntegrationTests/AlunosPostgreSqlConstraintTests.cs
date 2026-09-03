using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace BFA.IntegrationTests;

public sealed class AlunosPostgreSqlConstraintTests
{
    [Fact]
    public async Task PostgreSQL_rejeita_cpf_invalido_do_aluno()
    {
        await using var connection = await AbrirCenarioAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirAlunoAsync(
            connection,
            Guid.NewGuid(),
            cpf: "1234567890A"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_teste_alunos_cpf_valido", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_data_de_nascimento_futura()
    {
        await using var connection = await AbrirCenarioAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirAlunoAsync(
            connection,
            Guid.NewGuid(),
            dataNascimento: DateOnly.FromDateTime(DateTime.Today).AddDays(1)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_teste_alunos_data_nascimento_nao_futura", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_cpf_duplicado_de_aluno_na_mesma_organizacao()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, cpf: "12345678901");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirAlunoAsync(
            connection,
            organizacaoId,
            cpf: "12345678901"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("uq_teste_alunos_organizacao_cpf", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_permite_mesmo_cpf_de_aluno_em_outra_organizacao()
    {
        await using var connection = await AbrirCenarioAsync();

        Assert.Equal(1, await InserirAlunoAsync(
            connection,
            Guid.NewGuid(),
            cpf: "12345678901"));
        Assert.Equal(1, await InserirAlunoAsync(
            connection,
            Guid.NewGuid(),
            cpf: "12345678901"));
    }

    [Fact]
    public async Task PostgreSQL_exige_usuario_unico_por_organizacao_para_aluno()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, usuarioId: usuarioId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirAlunoAsync(
            connection,
            organizacaoId,
            usuarioId: usuarioId));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("uq_teste_alunos_organizacao_usuario", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_permite_mesma_identidade_como_aluno_e_responsavel()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        Assert.Equal(1, await InserirAlunoAsync(connection, organizacaoId, usuarioId: usuarioId));
        Assert.Equal(
            1,
            await InserirResponsavelAsync(connection, organizacaoId, usuarioId: usuarioId));
    }

    [Fact]
    public async Task PostgreSQL_permite_associar_usuario_ao_aluno_depois_da_criacao()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, alunoId: alunoId);

        var affected = await ExecuteAsync(
            connection,
            "UPDATE teste_alunos SET usuario_id = @usuario_id WHERE id = @id;",
            ("usuario_id", Guid.NewGuid()),
            ("id", alunoId));

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task PostgreSQL_exige_telefone_ou_email_do_responsavel()
    {
        await using var connection = await AbrirCenarioAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirResponsavelAsync(
            connection,
            Guid.NewGuid(),
            telefone: null,
            email: null));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_teste_responsaveis_contato_obrigatorio", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_permite_cpf_nulo_e_rejeita_duplicado_do_responsavel()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        Assert.Equal(1, await InserirResponsavelAsync(connection, organizacaoId, cpf: null));
        Assert.Equal(1, await InserirResponsavelAsync(connection, organizacaoId, cpf: null));
        await InserirResponsavelAsync(connection, organizacaoId, cpf: "10987654321");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirResponsavelAsync(
            connection,
            organizacaoId,
            cpf: "10987654321"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("uq_teste_responsaveis_organizacao_cpf", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_aceita_vinculo_valido_e_rejeita_duplicidade_logica()
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);
        await InserirVinculoAsync(connection, organizacaoId, alunoId, responsavelId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("uq_teste_alunos_responsaveis_pessoas", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_vinculo_entre_tenants()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoAlunoId = Guid.NewGuid();
        var organizacaoResponsavelId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoAlunoId, alunoId: alunoId);
        await InserirResponsavelAsync(
            connection,
            organizacaoResponsavelId,
            responsavelId: responsavelId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirVinculoAsync(
            connection,
            organizacaoAlunoId,
            alunoId,
            responsavelId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_permite_reativar_o_mesmo_vinculo_inativo()
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);
        var vinculoId = Guid.NewGuid();
        await InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId,
            vinculoId: vinculoId);

        await ExecuteAsync(
            connection,
            "UPDATE teste_alunos_responsaveis SET ativo = false WHERE id = @id;",
            ("id", vinculoId));
        var affected = await ExecuteAsync(
            connection,
            "UPDATE teste_alunos_responsaveis SET ativo = true WHERE id = @id;",
            ("id", vinculoId));

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task PostgreSQL_limita_um_principal_ativo_mas_aceita_dois_financeiros()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var responsavelUmId = Guid.NewGuid();
        var responsavelDoisId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, alunoId: alunoId);
        await InserirResponsavelAsync(
            connection,
            organizacaoId,
            responsavelId: responsavelUmId);
        await InserirResponsavelAsync(
            connection,
            organizacaoId,
            responsavelId: responsavelDoisId);
        await InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelUmId,
            principalContato: true,
            responsavelFinanceiro: true);
        await InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelDoisId,
            principalContato: false,
            responsavelFinanceiro: true);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            """
            UPDATE teste_alunos_responsaveis
            SET principal_contato = true
            WHERE responsavel_id = @responsavel_id;
            """,
            ("responsavel_id", responsavelDoisId)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("uq_teste_alunos_responsaveis_principal_ativo", exception.ConstraintName);
    }

    [Theory]
    [InlineData("Outro", null)]
    [InlineData("Outro", "")]
    [InlineData("Mae", "Genitora")]
    public async Task PostgreSQL_alinha_tipo_e_descricao_da_relacao(
        string tipoRelacao,
        string? descricao)
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId,
            tipoRelacao: tipoRelacao,
            descricaoRelacao: descricao));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_teste_alunos_responsaveis_descricao", exception.ConstraintName);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_vinculo_ativo_com_aluno_inativo()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, alunoId: alunoId, ativo: false);
        await InserirResponsavelAsync(
            connection,
            organizacaoId,
            responsavelId: responsavelId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_vinculo_ativo_com_responsavel_inativo()
    {
        await using var connection = await AbrirCenarioAsync();
        var organizacaoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, alunoId: alunoId);
        await InserirResponsavelAsync(
            connection,
            organizacaoId,
            responsavelId: responsavelId,
            ativo: false);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_nao_inativa_aluno_com_vinculo_ativo()
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);
        await InserirVinculoAsync(connection, organizacaoId, alunoId, responsavelId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "UPDATE teste_alunos SET ativo = false WHERE id = @id;",
            ("id", alunoId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_nao_inativa_responsavel_com_vinculo_ativo()
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);
        await InserirVinculoAsync(connection, organizacaoId, alunoId, responsavelId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "UPDATE teste_responsaveis SET ativo = false WHERE id = @id;",
            ("id", responsavelId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Theory]
    [InlineData("aluno")]
    [InlineData("responsavel")]
    [InlineData("vinculo")]
    public async Task PostgreSQL_preserva_identidade_tenant_e_criacao(string alvo)
    {
        await using var connection = await AbrirCenarioAsync();
        var (organizacaoId, alunoId, responsavelId) = await CriarPessoasAsync(connection);
        var vinculoId = Guid.NewGuid();
        await InserirVinculoAsync(
            connection,
            organizacaoId,
            alunoId,
            responsavelId,
            vinculoId: vinculoId);

        var (sql, id) = alvo switch
        {
            "aluno" => ("UPDATE teste_alunos SET organizacao_id = @novo WHERE id = @id;", alunoId),
            "responsavel" => (
                "UPDATE teste_responsaveis SET criado_em_utc = now() + interval '1 minute' "
                + "WHERE id = @id;",
                responsavelId),
            _ => (
                "UPDATE teste_alunos_responsaveis SET aluno_id = @novo WHERE id = @id;",
                vinculoId)
        };

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            sql,
            ("novo", Guid.NewGuid()),
            ("id", id)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    private static async Task<NpgsqlConnection> AbrirCenarioAsync()
    {
        var connection = new NpgsqlConnection(ObterConnectionString());
        await connection.OpenAsync();
        await CriarEstruturaTemporariaAsync(connection);
        return connection;
    }

    private static async Task CriarEstruturaTemporariaAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TEMP TABLE teste_alunos (
                id uuid NOT NULL,
                organizacao_id uuid NOT NULL,
                usuario_id uuid NULL,
                nome_completo varchar(150) NOT NULL,
                data_nascimento date NOT NULL,
                cpf varchar(11) NULL,
                ativo boolean NOT NULL,
                criado_em_utc timestamptz NOT NULL,
                atualizado_em_utc timestamptz NOT NULL,
                CONSTRAINT pk_teste_alunos PRIMARY KEY (id),
                CONSTRAINT uq_teste_alunos_tenant_id UNIQUE (organizacao_id, id),
                CONSTRAINT ck_teste_alunos_cpf_valido
                    CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
                CONSTRAINT ck_teste_alunos_data_nascimento_nao_futura
                    CHECK (data_nascimento <= CURRENT_DATE)
            );

            CREATE UNIQUE INDEX uq_teste_alunos_organizacao_cpf
                ON teste_alunos (organizacao_id, cpf)
                WHERE cpf IS NOT NULL;

            CREATE UNIQUE INDEX uq_teste_alunos_organizacao_usuario
                ON teste_alunos (organizacao_id, usuario_id)
                WHERE usuario_id IS NOT NULL;

            CREATE TEMP TABLE teste_responsaveis (
                id uuid NOT NULL,
                organizacao_id uuid NOT NULL,
                usuario_id uuid NULL,
                nome_completo varchar(150) NOT NULL,
                cpf varchar(11) NULL,
                telefone varchar(30) NULL,
                email varchar(256) NULL,
                ativo boolean NOT NULL,
                criado_em_utc timestamptz NOT NULL,
                atualizado_em_utc timestamptz NOT NULL,
                CONSTRAINT pk_teste_responsaveis PRIMARY KEY (id),
                CONSTRAINT uq_teste_responsaveis_tenant_id UNIQUE (organizacao_id, id),
                CONSTRAINT ck_teste_responsaveis_cpf_valido
                    CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
                CONSTRAINT ck_teste_responsaveis_contato_obrigatorio
                    CHECK (telefone IS NOT NULL OR email IS NOT NULL)
            );

            CREATE UNIQUE INDEX uq_teste_responsaveis_organizacao_cpf
                ON teste_responsaveis (organizacao_id, cpf)
                WHERE cpf IS NOT NULL;

            CREATE UNIQUE INDEX uq_teste_responsaveis_organizacao_usuario
                ON teste_responsaveis (organizacao_id, usuario_id)
                WHERE usuario_id IS NOT NULL;

            CREATE TEMP TABLE teste_alunos_responsaveis (
                id uuid NOT NULL,
                organizacao_id uuid NOT NULL,
                aluno_id uuid NOT NULL,
                responsavel_id uuid NOT NULL,
                tipo_relacao varchar(30) NOT NULL,
                descricao_relacao varchar(100) NULL,
                principal_contato boolean NOT NULL,
                responsavel_financeiro boolean NOT NULL,
                ativo boolean NOT NULL,
                criado_em_utc timestamptz NOT NULL,
                atualizado_em_utc timestamptz NOT NULL,
                CONSTRAINT pk_teste_alunos_responsaveis PRIMARY KEY (id),
                CONSTRAINT fk_teste_alunos_responsaveis_aluno
                    FOREIGN KEY (organizacao_id, aluno_id)
                    REFERENCES teste_alunos (organizacao_id, id)
                    ON DELETE RESTRICT,
                CONSTRAINT fk_teste_alunos_responsaveis_responsavel
                    FOREIGN KEY (organizacao_id, responsavel_id)
                    REFERENCES teste_responsaveis (organizacao_id, id)
                    ON DELETE RESTRICT,
                CONSTRAINT ck_teste_alunos_responsaveis_tipo
                    CHECK (tipo_relacao IN
                        ('Pai', 'Mae', 'ResponsavelLegal', 'Tutor', 'Avo', 'Outro')),
                CONSTRAINT ck_teste_alunos_responsaveis_descricao
                    CHECK (
                        (
                            tipo_relacao = 'Outro'
                            AND descricao_relacao IS NOT NULL
                            AND btrim(descricao_relacao) <> ''
                        )
                        OR
                        (tipo_relacao <> 'Outro' AND descricao_relacao IS NULL)
                    )
            );

            CREATE UNIQUE INDEX uq_teste_alunos_responsaveis_pessoas
                ON teste_alunos_responsaveis
                   (organizacao_id, aluno_id, responsavel_id);

            CREATE UNIQUE INDEX uq_teste_alunos_responsaveis_principal_ativo
                ON teste_alunos_responsaveis (organizacao_id, aluno_id)
                WHERE principal_contato = true AND ativo = true;

            CREATE FUNCTION pg_temp.proteger_teste_aluno()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF NEW.id IS DISTINCT FROM OLD.id
                    OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
                    OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
                    RAISE EXCEPTION 'identidade do aluno imutavel'
                        USING ERRCODE = '23514';
                END IF;

                IF OLD.ativo = true AND NEW.ativo = false AND EXISTS (
                    SELECT 1 FROM teste_alunos_responsaveis
                    WHERE organizacao_id = OLD.organizacao_id
                      AND aluno_id = OLD.id
                      AND ativo = true
                ) THEN
                    RAISE EXCEPTION 'aluno possui vinculo ativo'
                        USING ERRCODE = '23514';
                END IF;

                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER trg_proteger_teste_aluno
            BEFORE UPDATE ON teste_alunos
            FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_teste_aluno();

            CREATE FUNCTION pg_temp.proteger_teste_responsavel()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF NEW.id IS DISTINCT FROM OLD.id
                    OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
                    OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
                    RAISE EXCEPTION 'identidade do responsavel imutavel'
                        USING ERRCODE = '23514';
                END IF;

                IF OLD.ativo = true AND NEW.ativo = false AND EXISTS (
                    SELECT 1 FROM teste_alunos_responsaveis
                    WHERE organizacao_id = OLD.organizacao_id
                      AND responsavel_id = OLD.id
                      AND ativo = true
                ) THEN
                    RAISE EXCEPTION 'responsavel possui vinculo ativo'
                        USING ERRCODE = '23514';
                END IF;

                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER trg_proteger_teste_responsavel
            BEFORE UPDATE ON teste_responsaveis
            FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_teste_responsavel();

            CREATE FUNCTION pg_temp.proteger_teste_aluno_responsavel()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            DECLARE
                aluno_ativo boolean;
                responsavel_ativo boolean;
            BEGIN
                IF TG_OP = 'UPDATE' AND (
                    NEW.id IS DISTINCT FROM OLD.id
                    OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
                    OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
                    OR NEW.responsavel_id IS DISTINCT FROM OLD.responsavel_id
                    OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc
                ) THEN
                    RAISE EXCEPTION 'identidade do vinculo imutavel'
                        USING ERRCODE = '23514';
                END IF;

                SELECT ativo INTO aluno_ativo
                FROM teste_alunos
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.aluno_id
                FOR UPDATE;

                SELECT ativo INTO responsavel_ativo
                FROM teste_responsaveis
                WHERE organizacao_id = NEW.organizacao_id AND id = NEW.responsavel_id
                FOR UPDATE;

                IF NEW.ativo = true AND aluno_ativo IS DISTINCT FROM true THEN
                    RAISE EXCEPTION 'aluno inativo ou de outro tenant'
                        USING ERRCODE = '23514';
                END IF;

                IF NEW.ativo = true AND responsavel_ativo IS DISTINCT FROM true THEN
                    RAISE EXCEPTION 'responsavel inativo ou de outro tenant'
                        USING ERRCODE = '23514';
                END IF;

                RETURN NEW;
            END;
            $function$;

            CREATE TRIGGER trg_proteger_teste_aluno_responsavel
            BEFORE INSERT OR UPDATE ON teste_alunos_responsaveis
            FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_teste_aluno_responsavel();
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InserirAlunoAsync(
        NpgsqlConnection connection,
        Guid organizacaoId,
        Guid? alunoId = null,
        Guid? usuarioId = null,
        string? cpf = null,
        DateOnly? dataNascimento = null,
        bool ativo = true)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teste_alunos
                (id, organizacao_id, usuario_id, nome_completo, data_nascimento, cpf,
                 ativo, criado_em_utc, atualizado_em_utc)
            VALUES
                (@id, @organizacao_id, @usuario_id, 'Aluno BFA', @data_nascimento, @cpf,
                 @ativo, now(), now());
            """;
        command.Parameters.AddWithValue("id", alunoId ?? Guid.NewGuid());
        command.Parameters.AddWithValue("organizacao_id", organizacaoId);
        command.Parameters.AddWithValue(
            "usuario_id",
            NpgsqlDbType.Uuid,
            usuarioId.HasValue ? usuarioId.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "data_nascimento",
            dataNascimento ?? new DateOnly(2010, 1, 1));
        command.Parameters.AddWithValue(
            "cpf",
            NpgsqlDbType.Varchar,
            cpf is null ? DBNull.Value : cpf);
        command.Parameters.AddWithValue("ativo", ativo);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InserirResponsavelAsync(
        NpgsqlConnection connection,
        Guid organizacaoId,
        Guid? responsavelId = null,
        Guid? usuarioId = null,
        string? cpf = null,
        string? telefone = "15999999999",
        string? email = null,
        bool ativo = true)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teste_responsaveis
                (id, organizacao_id, usuario_id, nome_completo, cpf, telefone, email,
                 ativo, criado_em_utc, atualizado_em_utc)
            VALUES
                (@id, @organizacao_id, @usuario_id, 'Responsavel BFA', @cpf, @telefone, @email,
                 @ativo, now(), now());
            """;
        command.Parameters.AddWithValue("id", responsavelId ?? Guid.NewGuid());
        command.Parameters.AddWithValue("organizacao_id", organizacaoId);
        command.Parameters.AddWithValue(
            "usuario_id",
            NpgsqlDbType.Uuid,
            usuarioId.HasValue ? usuarioId.Value : DBNull.Value);
        command.Parameters.AddWithValue(
            "cpf",
            NpgsqlDbType.Varchar,
            cpf is null ? DBNull.Value : cpf);
        command.Parameters.AddWithValue(
            "telefone",
            NpgsqlDbType.Varchar,
            telefone is null ? DBNull.Value : telefone);
        command.Parameters.AddWithValue(
            "email",
            NpgsqlDbType.Varchar,
            email is null ? DBNull.Value : email);
        command.Parameters.AddWithValue("ativo", ativo);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InserirVinculoAsync(
        NpgsqlConnection connection,
        Guid organizacaoId,
        Guid alunoId,
        Guid responsavelId,
        Guid? vinculoId = null,
        string tipoRelacao = "Mae",
        string? descricaoRelacao = null,
        bool principalContato = false,
        bool responsavelFinanceiro = false,
        bool ativo = true)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO teste_alunos_responsaveis
                (id, organizacao_id, aluno_id, responsavel_id, tipo_relacao,
                 descricao_relacao, principal_contato, responsavel_financeiro,
                 ativo, criado_em_utc, atualizado_em_utc)
            VALUES
                (@id, @organizacao_id, @aluno_id, @responsavel_id, @tipo_relacao,
                 @descricao_relacao, @principal_contato, @responsavel_financeiro,
                 @ativo, now(), now());
            """;
        command.Parameters.AddWithValue("id", vinculoId ?? Guid.NewGuid());
        command.Parameters.AddWithValue("organizacao_id", organizacaoId);
        command.Parameters.AddWithValue("aluno_id", alunoId);
        command.Parameters.AddWithValue("responsavel_id", responsavelId);
        command.Parameters.AddWithValue("tipo_relacao", tipoRelacao);
        command.Parameters.AddWithValue(
            "descricao_relacao",
            NpgsqlDbType.Varchar,
            descricaoRelacao is null ? DBNull.Value : descricaoRelacao);
        command.Parameters.AddWithValue("principal_contato", principalContato);
        command.Parameters.AddWithValue("responsavel_financeiro", responsavelFinanceiro);
        command.Parameters.AddWithValue("ativo", ativo);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<(Guid OrganizacaoId, Guid AlunoId, Guid ResponsavelId)>
        CriarPessoasAsync(NpgsqlConnection connection)
    {
        var organizacaoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var responsavelId = Guid.NewGuid();
        await InserirAlunoAsync(connection, organizacaoId, alunoId: alunoId);
        await InserirResponsavelAsync(
            connection,
            organizacaoId,
            responsavelId: responsavelId);
        return (organizacaoId, alunoId, responsavelId);
    }

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    private static string ObterConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("BfaDatabase");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "Configure ConnectionStrings:BfaDatabase para executar os testes PostgreSQL.");
        return connectionString;
    }
}
