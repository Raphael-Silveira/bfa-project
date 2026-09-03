using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;

namespace BFA.IntegrationTests;

public sealed class MatriculasPostgreSqlConstraintTests
{
    [Fact]
    public async Task PostgreSQL_disponibiliza_plano_de_rede_e_permite_reativacao()
    {
        await using var cenario = await Cenario.CreateAsync();
        var id = await cenario.InserirDisponibilidadeAsync();
        await cenario.ExecuteAsync("UPDATE disponibilidades SET ativo = false WHERE id = @id", ("id", id));

        Assert.Equal(1, await cenario.ExecuteAsync(
            "UPDATE disponibilidades SET ativo = true WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task PostgreSQL_rejeita_disponibilidade_de_plano_local()
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirDisponibilidadeAsync(planoId: cenario.PlanoLocalId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_disponibilidade_cross_tenant()
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirDisponibilidadeAsync(organizacaoId: Guid.NewGuid()));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_disponibilidade_duplicada()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirDisponibilidadeAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirDisponibilidadeAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("plano")]
    [InlineData("unidade")]
    public async Task PostgreSQL_disponibilidade_ativa_exige_referencias_ativas(string alvo)
    {
        await using var cenario = await Cenario.CreateAsync();
        if (alvo == "plano")
        {
            await cenario.ExecuteAsync("UPDATE planos SET ativo = false WHERE id = @id", ("id", cenario.PlanoRedeId));
        }
        else
        {
            await cenario.ExecuteAsync("UPDATE unidades SET ativo = false WHERE id = @id", ("id", cenario.UnidadeId));
        }
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirDisponibilidadeAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_preserva_identidade_da_disponibilidade()
    {
        await using var cenario = await Cenario.CreateAsync();
        var id = await cenario.InserirDisponibilidadeAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(
            "UPDATE disponibilidades SET unidade_id = @novo WHERE id = @id",
            ("novo", cenario.OutraUnidadeId), ("id", id)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_aceita_matricula_com_plano_local_da_unidade()
    {
        await using var cenario = await Cenario.CreateAsync();
        Assert.Equal(1, await cenario.InserirMatriculaAsync(cenario.VersaoLocalId));
    }

    [Fact]
    public async Task PostgreSQL_rejeita_plano_local_de_outra_unidade()
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId, unidadeId: cenario.OutraUnidadeId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_aceita_plano_de_rede_disponibilizado()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirDisponibilidadeAsync();
        Assert.Equal(1, await cenario.InserirMatriculaAsync(cenario.VersaoRedeId));
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("inativa")]
    public async Task PostgreSQL_rejeita_plano_de_rede_sem_disponibilidade_ativa(string estado)
    {
        await using var cenario = await Cenario.CreateAsync();
        if (estado == "inativa")
        {
            await cenario.InserirDisponibilidadeAsync(ativo: false);
        }
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirMatriculaAsync(cenario.VersaoRedeId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("plano")]
    [InlineData("aluno")]
    [InlineData("unidade")]
    public async Task PostgreSQL_rejeita_nova_matricula_com_participante_inativo(string alvo)
    {
        await using var cenario = await Cenario.CreateAsync();
        var (tabela, id) = alvo switch
        {
            "plano" => ("planos", cenario.PlanoLocalId),
            "aluno" => ("alunos", cenario.AlunoId),
            _ => ("unidades", cenario.UnidadeId)
        };
        await cenario.ExecuteAsync($"UPDATE {tabela} SET ativo = false WHERE id = @id", ("id", id));
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirMatriculaAsync(cenario.VersaoLocalId));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("antes", false)]
    [InlineData("inicio", true)]
    [InlineData("fim", true)]
    [InlineData("depois", false)]
    public async Task PostgreSQL_aplica_vigencia_comercial_inclusiva(string posicao, bool aceita)
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.ExecuteAsync(
            "UPDATE versoes SET vigencia_fim = DATE '2026-12-31' WHERE id = @id",
            ("id", cenario.VersaoLocalId));
        var data = posicao switch
        {
            "antes" => new DateOnly(2025, 12, 31),
            "inicio" => new DateOnly(2026, 1, 1),
            "fim" => new DateOnly(2026, 12, 31),
            _ => new DateOnly(2027, 1, 1)
        };
        if (aceita)
        {
            Assert.Equal(1, await cenario.InserirMatriculaAsync(cenario.VersaoLocalId, dataInicio: data));
        }
        else
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                cenario.InserirMatriculaAsync(cenario.VersaoLocalId, dataInicio: data));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [Fact]
    public async Task PostgreSQL_preserva_versao_e_aceita_preco_negociado_diferente()
    {
        await using var cenario = await Cenario.CreateAsync();
        var id = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(cenario.VersaoLocalId, matriculaId: id, valor: 123.45m);
        Assert.Equal(123.45m, await cenario.ScalarAsync<decimal>(
            "SELECT valor_mensal_contratado FROM matriculas WHERE id = @id", ("id", id)));
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(
            "UPDATE matriculas SET plano_versao_id = @novo WHERE id = @id",
            ("novo", cenario.VersaoRedeId), ("id", id)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    public async Task PostgreSQL_rejeita_preco_contratado_nao_positivo(double valor)
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirMatriculaAsync(cenario.VersaoLocalId, valor: (decimal)valor));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, 0d)]
    [InlineData(false, 10d)]
    public async Task PostgreSQL_rejeita_taxa_efetiva_inconsistente(bool cobra, double? taxa)
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId, cobraTaxa: cobra,
            valorTaxa: taxa.HasValue ? (decimal)taxa.Value : null));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_rejeita_data_fim_prevista_anterior_ao_inicio()
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId, dataFimPrevista: new DateOnly(2026, 8, 31)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_limita_uma_ativa_por_aluno_unidade_e_permite_outra_unidade()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirMatriculaAsync(cenario.VersaoLocalId);
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            cenario.InserirMatriculaAsync(cenario.VersaoLocalId));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);

        Assert.Equal(1, await cenario.InserirMatriculaAsync(
            cenario.VersaoOutraUnidadeId, unidadeId: cenario.OutraUnidadeId));
    }

    [Theory]
    [InlineData("Ativa", "2026-09-01")]
    [InlineData("Encerrada", null)]
    [InlineData("Cancelada", null)]
    public async Task PostgreSQL_alinha_status_com_data_fim_real(string status, string? fim)
    {
        await using var cenario = await Cenario.CreateAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId, status: status,
            dataFimReal: fim is null ? null : DateOnly.Parse(fim)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("Encerrada")]
    [InlineData("Cancelada")]
    public async Task PostgreSQL_permite_transicao_da_ativa_para_terminal(string status)
    {
        await using var cenario = await Cenario.CreateAsync();
        var id = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(cenario.VersaoLocalId, matriculaId: id);
        Assert.Equal(1, await cenario.ExecuteAsync(
            "UPDATE matriculas SET status = @status, data_fim_real = DATE '2026-09-02' WHERE id = @id",
            ("status", status), ("id", id)));
    }

    [Fact]
    public async Task PostgreSQL_estado_terminal_nao_reabre_nem_altera_data_final()
    {
        await using var cenario = await Cenario.CreateAsync();
        var id = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(cenario.VersaoLocalId, matriculaId: id);
        await cenario.ExecuteAsync(
            "UPDATE matriculas SET status = 'Encerrada', data_fim_real = DATE '2026-09-02' WHERE id = @id", ("id", id));
        foreach (var sql in new[]
        {
            "UPDATE matriculas SET status = 'Ativa', data_fim_real = NULL WHERE id = @id",
            "UPDATE matriculas SET data_fim_real = DATE '2026-09-03' WHERE id = @id"
        })
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(sql, ("id", id)));
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [Fact]
    public async Task PostgreSQL_bloqueia_inativacao_do_aluno_com_matricula_ativa()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirMatriculaAsync(cenario.VersaoLocalId);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(
            "UPDATE alunos SET ativo = false WHERE id = @id", ("id", cenario.AlunoId)));
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_matricula_existente_permanece_apos_disponibilidade_e_plano_inativos()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirDisponibilidadeAsync();
        var id = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(cenario.VersaoRedeId, matriculaId: id);
        await cenario.ExecuteAsync("UPDATE disponibilidades SET ativo = false");
        await cenario.ExecuteAsync("UPDATE planos SET ativo = false WHERE id = @id", ("id", cenario.PlanoRedeId));
        Assert.Equal(id, await cenario.ScalarAsync<Guid>("SELECT id FROM matriculas WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task PostgreSQL_matricula_existente_impede_encerrar_versao_antes_de_seu_inicio()
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId,
            dataInicio: new DateOnly(2026, 10, 15));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(
            "UPDATE versoes SET vigencia_fim = DATE '2026-09-30' WHERE id = @id",
            ("id", cenario.VersaoLocalId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("Encerrada")]
    [InlineData("Cancelada")]
    public async Task PostgreSQL_matricula_historica_tambem_protege_vigencia_da_versao(
        string status)
    {
        await using var cenario = await Cenario.CreateAsync();
        var matriculaId = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId,
            matriculaId: matriculaId,
            dataInicio: new DateOnly(2026, 10, 15));
        await cenario.ExecuteAsync(
            "UPDATE matriculas SET status = @status, data_fim_real = DATE '2026-10-16' WHERE id = @id",
            ("status", status),
            ("id", matriculaId));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cenario.ExecuteAsync(
            "UPDATE versoes SET vigencia_fim = DATE '2026-09-30' WHERE id = @id",
            ("id", cenario.VersaoLocalId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData("2026-09-01")]
    [InlineData("2026-09-30")]
    public async Task PostgreSQL_versao_pode_terminar_em_data_compativel_com_inicio(
        string vigenciaFim)
    {
        await using var cenario = await Cenario.CreateAsync();
        await cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId,
            dataInicio: new DateOnly(2026, 9, 1));

        Assert.Equal(1, await cenario.ExecuteAsync(
            "UPDATE versoes SET vigencia_fim = @fim WHERE id = @id",
            ("fim", DateOnly.Parse(vigenciaFim)),
            ("id", cenario.VersaoLocalId)));
    }

    [Fact]
    public async Task PostgreSQL_matricula_pode_continuar_apos_fim_comercial_da_versao()
    {
        await using var cenario = await Cenario.CreateAsync();
        var matriculaId = Guid.NewGuid();
        await cenario.InserirMatriculaAsync(
            cenario.VersaoLocalId,
            matriculaId: matriculaId,
            dataInicio: new DateOnly(2026, 9, 1),
            dataFimPrevista: new DateOnly(2027, 2, 28));

        Assert.Equal(1, await cenario.ExecuteAsync(
            "UPDATE versoes SET vigencia_fim = DATE '2026-09-30' WHERE id = @id",
            ("id", cenario.VersaoLocalId)));
        Assert.Equal(
            new DateOnly(2027, 2, 28),
            await cenario.ScalarAsync<DateOnly>(
                "SELECT data_fim_prevista FROM matriculas WHERE id = @id",
                ("id", matriculaId)));
    }

    private sealed class Cenario : IAsyncDisposable
    {
        private Cenario(NpgsqlConnection connection) => Connection = connection;

        public NpgsqlConnection Connection { get; }
        public Guid OrganizacaoId { get; } = Guid.NewGuid();
        public Guid UnidadeId { get; } = Guid.NewGuid();
        public Guid OutraUnidadeId { get; } = Guid.NewGuid();
        public Guid AlunoId { get; } = Guid.NewGuid();
        public Guid PlanoRedeId { get; } = Guid.NewGuid();
        public Guid PlanoLocalId { get; } = Guid.NewGuid();
        public Guid PlanoOutraUnidadeId { get; } = Guid.NewGuid();
        public Guid VersaoRedeId { get; } = Guid.NewGuid();
        public Guid VersaoLocalId { get; } = Guid.NewGuid();
        public Guid VersaoOutraUnidadeId { get; } = Guid.NewGuid();

        public static async Task<Cenario> CreateAsync()
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().Build();
            var connectionString = configuration.GetConnectionString("BfaDatabase");
            Assert.False(string.IsNullOrWhiteSpace(connectionString),
                "Configure ConnectionStrings:BfaDatabase para executar os testes PostgreSQL.");
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var cenario = new Cenario(connection);
            await cenario.SetupAsync();
            return cenario;
        }

        public async Task<Guid> InserirDisponibilidadeAsync(
            Guid? organizacaoId = null, Guid? planoId = null, bool ativo = true)
        {
            var id = Guid.NewGuid();
            await ExecuteAsync(
                "INSERT INTO disponibilidades (id, organizacao_id, plano_id, unidade_id, ativo, criado_em_utc) "
                + "VALUES (@id, @org, @plano, @unidade, @ativo, now())",
                ("id", id), ("org", organizacaoId ?? OrganizacaoId),
                ("plano", planoId ?? PlanoRedeId), ("unidade", UnidadeId), ("ativo", ativo));
            return id;
        }

        public async Task<int> InserirMatriculaAsync(
            Guid versaoId, Guid? unidadeId = null, Guid? matriculaId = null,
            DateOnly? dataInicio = null, DateOnly? dataFimPrevista = null,
            decimal valor = 150m, bool cobraTaxa = false, decimal? valorTaxa = null,
            string status = "Ativa", DateOnly? dataFimReal = null,
            NpgsqlConnection? connection = null)
        {
            var target = connection ?? Connection;
            await using var command = target.CreateCommand();
            command.CommandTimeout = 10;
            command.CommandText = """
                INSERT INTO matriculas
                    (id, organizacao_id, unidade_id, aluno_id, plano_versao_id,
                     data_inicio, data_fim_prevista, data_fim_real, status,
                     valor_mensal_contratado, cobra_taxa_matricula, valor_taxa_matricula,
                     criado_em_utc)
                VALUES
                    (@id, @org, @unidade, @aluno, @versao,
                     @inicio, @fim_prevista, @fim_real, @status,
                     @valor, @cobra_taxa, @valor_taxa, now())
                """;
            var inicio = dataInicio ?? new DateOnly(2026, 9, 1);
            command.Parameters.AddWithValue("id", matriculaId ?? Guid.NewGuid());
            command.Parameters.AddWithValue("org", OrganizacaoId);
            command.Parameters.AddWithValue("unidade", unidadeId ?? UnidadeId);
            command.Parameters.AddWithValue("aluno", AlunoId);
            command.Parameters.AddWithValue("versao", versaoId);
            command.Parameters.AddWithValue("inicio", inicio);
            command.Parameters.AddWithValue("fim_prevista", dataFimPrevista ?? inicio.AddMonths(6).AddDays(-1));
            command.Parameters.AddWithValue("fim_real", NpgsqlDbType.Date, dataFimReal.HasValue ? dataFimReal.Value : DBNull.Value);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("valor", valor);
            command.Parameters.AddWithValue("cobra_taxa", cobraTaxa);
            command.Parameters.AddWithValue("valor_taxa", NpgsqlDbType.Numeric, valorTaxa.HasValue ? valorTaxa.Value : DBNull.Value);
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            return (T)(await command.ExecuteScalarAsync())!;
        }

        private async Task SetupAsync()
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = """
                CREATE TEMP TABLE unidades (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, ativo boolean NOT NULL,
                    UNIQUE (organizacao_id, id));
                CREATE TEMP TABLE alunos (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, ativo boolean NOT NULL,
                    UNIQUE (organizacao_id, id));
                CREATE TEMP TABLE planos (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, unidade_id uuid NULL,
                    ativo boolean NOT NULL, UNIQUE (organizacao_id, id));
                CREATE TEMP TABLE versoes (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, plano_id uuid NOT NULL,
                    vigencia_inicio date NOT NULL, vigencia_fim date NULL,
                    UNIQUE (organizacao_id, id),
                    FOREIGN KEY (organizacao_id, plano_id) REFERENCES planos (organizacao_id, id));
                CREATE TEMP TABLE disponibilidades (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, plano_id uuid NOT NULL,
                    unidade_id uuid NOT NULL, ativo boolean NOT NULL, criado_em_utc timestamptz NOT NULL,
                    UNIQUE (organizacao_id, plano_id, unidade_id),
                    FOREIGN KEY (organizacao_id, plano_id) REFERENCES planos (organizacao_id, id),
                    FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id));

                CREATE FUNCTION pg_temp.proteger_disponibilidade() RETURNS trigger LANGUAGE plpgsql AS $f$
                DECLARE plano_ativo boolean; plano_unidade uuid; unidade_ativa boolean;
                BEGIN
                    IF TG_OP = 'UPDATE' AND (
                        NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
                        OR NEW.plano_id IS DISTINCT FROM OLD.plano_id OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
                        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc) THEN
                        RAISE EXCEPTION 'identidade imutavel' USING ERRCODE = '23514';
                    END IF;
                    SELECT ativo, unidade_id INTO plano_ativo, plano_unidade FROM planos
                    WHERE organizacao_id = NEW.organizacao_id AND id = NEW.plano_id;
                    IF plano_unidade IS NOT NULL THEN RAISE EXCEPTION 'plano local' USING ERRCODE = '23514'; END IF;
                    IF NEW.ativo THEN
                        SELECT ativo INTO unidade_ativa FROM unidades
                        WHERE organizacao_id = NEW.organizacao_id AND id = NEW.unidade_id;
                        IF plano_ativo IS DISTINCT FROM true OR unidade_ativa IS DISTINCT FROM true THEN
                            RAISE EXCEPTION 'referencia inativa' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER trg_disponibilidade BEFORE INSERT OR UPDATE ON disponibilidades
                    FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_disponibilidade();

                CREATE TEMP TABLE matriculas (
                    id uuid PRIMARY KEY, organizacao_id uuid NOT NULL, unidade_id uuid NOT NULL,
                    aluno_id uuid NOT NULL, plano_versao_id uuid NOT NULL,
                    data_inicio date NOT NULL, data_fim_prevista date NOT NULL, data_fim_real date NULL,
                    status varchar(20) NOT NULL, valor_mensal_contratado numeric(12,2) NOT NULL,
                    cobra_taxa_matricula boolean NOT NULL, valor_taxa_matricula numeric(12,2) NULL,
                    criado_em_utc timestamptz NOT NULL,
                    FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id),
                    FOREIGN KEY (organizacao_id, aluno_id) REFERENCES alunos (organizacao_id, id),
                    FOREIGN KEY (organizacao_id, plano_versao_id) REFERENCES versoes (organizacao_id, id),
                    CHECK (data_fim_prevista >= data_inicio), CHECK (valor_mensal_contratado > 0),
                    CHECK ((cobra_taxa_matricula AND valor_taxa_matricula IS NOT NULL AND valor_taxa_matricula > 0)
                        OR (NOT cobra_taxa_matricula AND valor_taxa_matricula IS NULL)),
                    CHECK ((status = 'Ativa' AND data_fim_real IS NULL)
                        OR (status IN ('Encerrada', 'Cancelada') AND data_fim_real IS NOT NULL AND data_fim_real >= data_inicio)));
                CREATE UNIQUE INDEX uq_matricula_ativa ON matriculas (organizacao_id, unidade_id, aluno_id)
                    WHERE status = 'Ativa';

                CREATE FUNCTION pg_temp.proteger_matricula() RETURNS trigger LANGUAGE plpgsql AS $f$
                DECLARE plano_id_atual uuid; inicio date; fim date; plano_ativo boolean;
                    plano_unidade uuid; disp_ativa boolean; unidade_ativa boolean; aluno_ativo boolean;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
                            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
                            OR NEW.plano_versao_id IS DISTINCT FROM OLD.plano_versao_id
                            OR NEW.data_inicio IS DISTINCT FROM OLD.data_inicio
                            OR NEW.data_fim_prevista IS DISTINCT FROM OLD.data_fim_prevista
                            OR NEW.valor_mensal_contratado IS DISTINCT FROM OLD.valor_mensal_contratado
                            OR NEW.cobra_taxa_matricula IS DISTINCT FROM OLD.cobra_taxa_matricula
                            OR NEW.valor_taxa_matricula IS DISTINCT FROM OLD.valor_taxa_matricula
                            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
                            RAISE EXCEPTION 'snapshot imutavel' USING ERRCODE = '23514';
                        END IF;
                        IF OLD.status IN ('Encerrada', 'Cancelada') AND
                            (NEW.status IS DISTINCT FROM OLD.status OR NEW.data_fim_real IS DISTINCT FROM OLD.data_fim_real) THEN
                            RAISE EXCEPTION 'terminal imutavel' USING ERRCODE = '23514';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF NEW.status <> 'Ativa' OR NEW.data_fim_real IS NOT NULL THEN
                        RAISE EXCEPTION 'nova matricula invalida' USING ERRCODE = '23514';
                    END IF;
                    SELECT plano_id, vigencia_inicio, vigencia_fim INTO plano_id_atual, inicio, fim FROM versoes
                    WHERE organizacao_id = NEW.organizacao_id AND id = NEW.plano_versao_id FOR UPDATE;
                    IF NEW.data_inicio < inicio OR (fim IS NOT NULL AND NEW.data_inicio > fim) THEN
                        RAISE EXCEPTION 'fora da vigencia' USING ERRCODE = '23514';
                    END IF;
                    SELECT ativo, unidade_id INTO plano_ativo, plano_unidade FROM planos
                    WHERE organizacao_id = NEW.organizacao_id AND id = plano_id_atual FOR UPDATE;
                    IF plano_ativo IS DISTINCT FROM true THEN RAISE EXCEPTION 'plano inativo' USING ERRCODE = '23514'; END IF;
                    IF plano_unidade IS NULL THEN
                        SELECT ativo INTO disp_ativa FROM disponibilidades
                        WHERE organizacao_id = NEW.organizacao_id AND plano_id = plano_id_atual
                            AND unidade_id = NEW.unidade_id FOR UPDATE;
                        IF disp_ativa IS DISTINCT FROM true THEN RAISE EXCEPTION 'sem disponibilidade' USING ERRCODE = '23514'; END IF;
                    ELSIF plano_unidade <> NEW.unidade_id THEN
                        RAISE EXCEPTION 'unidade local divergente' USING ERRCODE = '23514';
                    END IF;
                    SELECT ativo INTO unidade_ativa FROM unidades
                    WHERE organizacao_id = NEW.organizacao_id AND id = NEW.unidade_id FOR UPDATE;
                    SELECT ativo INTO aluno_ativo FROM alunos
                    WHERE organizacao_id = NEW.organizacao_id AND id = NEW.aluno_id FOR UPDATE;
                    IF unidade_ativa IS DISTINCT FROM true OR aluno_ativo IS DISTINCT FROM true THEN
                        RAISE EXCEPTION 'participante inativo' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER trg_matricula BEFORE INSERT OR UPDATE ON matriculas
                    FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_matricula();

                CREATE FUNCTION pg_temp.proteger_versao_matriculas() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                    IF OLD.vigencia_fim IS NULL AND NEW.vigencia_fim IS NOT NULL AND EXISTS (
                        SELECT 1 FROM matriculas WHERE organizacao_id = NEW.organizacao_id
                            AND plano_versao_id = NEW.id AND data_inicio > NEW.vigencia_fim) THEN
                        RAISE EXCEPTION 'vigencia invalida para matricula existente' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER trg_versao_matriculas BEFORE UPDATE ON versoes
                    FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_versao_matriculas();

                CREATE FUNCTION pg_temp.proteger_aluno() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                    IF OLD.ativo AND NOT NEW.ativo AND EXISTS (
                        SELECT 1 FROM matriculas WHERE organizacao_id = OLD.organizacao_id
                            AND aluno_id = OLD.id AND status = 'Ativa') THEN
                        RAISE EXCEPTION 'matricula ativa' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $f$;
                CREATE TRIGGER trg_aluno BEFORE UPDATE ON alunos FOR EACH ROW EXECUTE FUNCTION pg_temp.proteger_aluno();
                """;
            await command.ExecuteNonQueryAsync();

            await ExecuteAsync("INSERT INTO unidades VALUES (@id, @org, true), (@outra, @org, true)",
                ("id", UnidadeId), ("outra", OutraUnidadeId), ("org", OrganizacaoId));
            await ExecuteAsync("INSERT INTO alunos VALUES (@id, @org, true)", ("id", AlunoId), ("org", OrganizacaoId));
            await ExecuteAsync(
                "INSERT INTO planos VALUES (@rede, @org, NULL, true), (@local, @org, @unidade, true), (@outro, @org, @outra, true)",
                ("rede", PlanoRedeId), ("local", PlanoLocalId), ("outro", PlanoOutraUnidadeId),
                ("org", OrganizacaoId), ("unidade", UnidadeId), ("outra", OutraUnidadeId));
            await ExecuteAsync(
                "INSERT INTO versoes VALUES (@vr, @org, @rede, DATE '2026-01-01', NULL), "
                + "(@vl, @org, @local, DATE '2026-01-01', NULL), "
                + "(@vo, @org, @outro, DATE '2026-01-01', NULL)",
                ("vr", VersaoRedeId), ("vl", VersaoLocalId), ("vo", VersaoOutraUnidadeId),
                ("org", OrganizacaoId), ("rede", PlanoRedeId), ("local", PlanoLocalId), ("outro", PlanoOutraUnidadeId));
        }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync();
        }
    }
}
