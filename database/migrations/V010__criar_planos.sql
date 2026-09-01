BEGIN;

CREATE TABLE planos (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NULL,
    nome varchar(150) NOT NULL,
    ativo boolean NOT NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_planos PRIMARY KEY (id),
    CONSTRAINT uq_planos_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_planos_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_planos_nome_nao_vazio
        CHECK (btrim(nome) <> '')
);

CREATE INDEX ix_planos_organizacao_unidade_ativo
    ON planos (organizacao_id, unidade_id, ativo);

CREATE INDEX ix_planos_criado_por_usuario_id
    ON planos (criado_por_usuario_id);

CREATE INDEX ix_planos_atualizado_por_usuario_id
    ON planos (atualizado_por_usuario_id);

CREATE FUNCTION proteger_plano()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
        OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
        OR NEW.nome IS DISTINCT FROM OLD.nome
        OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
        RAISE EXCEPTION
            'A identidade, o escopo e a auditoria de criacao do plano nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_plano
BEFORE UPDATE
ON planos
FOR EACH ROW
EXECUTE FUNCTION proteger_plano();

CREATE TABLE planos_versoes (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    plano_id uuid NOT NULL,
    numero_versao integer NOT NULL,
    duracao_meses smallint NOT NULL,
    frequencia_semanal smallint NOT NULL,
    valor_mensal numeric(12,2) NOT NULL,
    cobra_matricula boolean NOT NULL,
    valor_matricula numeric(12,2) NULL,
    vigencia_inicio date NOT NULL,
    vigencia_fim date NULL,
    criado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_planos_versoes PRIMARY KEY (id),
    CONSTRAINT uq_planos_versoes_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_planos_versoes_plano
        FOREIGN KEY (organizacao_id, plano_id)
        REFERENCES planos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_versoes_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_versoes_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_planos_versoes_numero_positivo
        CHECK (numero_versao > 0),
    CONSTRAINT ck_planos_versoes_duracao_positiva
        CHECK (duracao_meses > 0),
    CONSTRAINT ck_planos_versoes_frequencia_valida
        CHECK (frequencia_semanal BETWEEN 1 AND 7),
    CONSTRAINT ck_planos_versoes_valor_mensal_positivo
        CHECK (valor_mensal > 0),
    CONSTRAINT ck_planos_versoes_matricula_valida
        CHECK (
            (
                cobra_matricula = true
                AND valor_matricula IS NOT NULL
                AND valor_matricula > 0
            )
            OR
            (cobra_matricula = false AND valor_matricula IS NULL)
        ),
    CONSTRAINT ck_planos_versoes_vigencia_valida
        CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)
);

CREATE UNIQUE INDEX uq_planos_versoes_plano_numero
    ON planos_versoes (plano_id, numero_versao);

CREATE UNIQUE INDEX uq_planos_versoes_aberta
    ON planos_versoes (plano_id)
    WHERE vigencia_fim IS NULL;

CREATE INDEX ix_planos_versoes_organizacao_plano_vigencia
    ON planos_versoes
       (organizacao_id, plano_id, vigencia_inicio, vigencia_fim);

CREATE INDEX ix_planos_versoes_criado_por_usuario_id
    ON planos_versoes (criado_por_usuario_id);

CREATE FUNCTION proteger_plano_versao()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.plano_id IS DISTINCT FROM OLD.plano_id
            OR NEW.numero_versao IS DISTINCT FROM OLD.numero_versao
            OR NEW.duracao_meses IS DISTINCT FROM OLD.duracao_meses
            OR NEW.frequencia_semanal IS DISTINCT FROM OLD.frequencia_semanal
            OR NEW.valor_mensal IS DISTINCT FROM OLD.valor_mensal
            OR NEW.cobra_matricula IS DISTINCT FROM OLD.cobra_matricula
            OR NEW.valor_matricula IS DISTINCT FROM OLD.valor_matricula
            OR NEW.vigencia_inicio IS DISTINCT FROM OLD.vigencia_inicio
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade e os termos comerciais da versao do plano nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.vigencia_fim IS NOT NULL
            AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim THEN
            RAISE EXCEPTION
                'A vigencia final da versao do plano nao pode ser alterada novamente.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    PERFORM 1
    FROM planos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.plano_id
    FOR UPDATE;

    IF EXISTS (
        SELECT 1
        FROM planos_versoes AS existente
        WHERE existente.organizacao_id = NEW.organizacao_id
          AND existente.plano_id = NEW.plano_id
          AND existente.id <> NEW.id
          AND daterange(
                  existente.vigencia_inicio,
                  existente.vigencia_fim,
                  '[]')
              && daterange(NEW.vigencia_inicio, NEW.vigencia_fim, '[]')
    ) THEN
        RAISE EXCEPTION
            'As vigencias das versoes comerciais do plano nao podem se sobrepor.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_plano_versao
BEFORE INSERT OR UPDATE
ON planos_versoes
FOR EACH ROW
EXECUTE FUNCTION proteger_plano_versao();

GRANT SELECT, INSERT, UPDATE
    ON TABLE planos
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE planos_versoes
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V010', 'criar planos e versoes comerciais');

COMMIT;
