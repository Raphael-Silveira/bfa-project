BEGIN;

CREATE TABLE professores (
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
    CONSTRAINT pk_professores PRIMARY KEY (id),
    CONSTRAINT uq_professores_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_professores_organizacoes_organizacao_id
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_professores_usuarios_usuario_id
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_professores_nome_completo_nao_vazio
        CHECK (btrim(nome_completo) <> ''),
    CONSTRAINT ck_professores_cpf_valido
        CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
    CONSTRAINT ck_professores_telefone_nao_vazio
        CHECK (telefone IS NULL OR btrim(telefone) <> ''),
    CONSTRAINT ck_professores_email_nao_vazio
        CHECK (email IS NULL OR btrim(email) <> '')
);

CREATE UNIQUE INDEX uq_professores_organizacao_cpf
    ON professores (organizacao_id, cpf)
    WHERE cpf IS NOT NULL;

CREATE UNIQUE INDEX uq_professores_organizacao_usuario
    ON professores (organizacao_id, usuario_id)
    WHERE usuario_id IS NOT NULL;

CREATE INDEX ix_professores_organizacao_ativo
    ON professores (organizacao_id, ativo);

CREATE INDEX ix_professores_usuario_id
    ON professores (usuario_id);

CREATE TABLE professores_unidades (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    professor_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_professores_unidades PRIMARY KEY (id),
    CONSTRAINT uq_professores_unidades_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_professores_unidades_professor
        FOREIGN KEY (organizacao_id, professor_id)
        REFERENCES professores (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_professores_unidades_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_professores_unidades_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX uq_professores_unidades_professor_unidade
    ON professores_unidades (organizacao_id, professor_id, unidade_id);

CREATE INDEX ix_professores_unidades_organizacao_unidade_ativo
    ON professores_unidades (organizacao_id, unidade_id, ativo);

CREATE INDEX ix_professores_unidades_organizacao_professor_ativo
    ON professores_unidades (organizacao_id, professor_id, ativo);

CREATE TABLE professores_remuneracoes (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    professor_unidade_id uuid NOT NULL,
    modalidade varchar(30) NOT NULL,
    valor numeric(12,2) NOT NULL,
    vigencia_inicio date NOT NULL,
    vigencia_fim date NULL,
    observacao varchar(1000) NULL,
    criado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_professores_remuneracoes PRIMARY KEY (id),
    CONSTRAINT fk_professores_remuneracoes_professor_unidade
        FOREIGN KEY (organizacao_id, professor_unidade_id)
        REFERENCES professores_unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_professores_remuneracoes_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_professores_remuneracoes_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_professores_remuneracoes_modalidade_valida
        CHECK (modalidade IN ('Mensal', 'PorAula', 'PorHora')),
    CONSTRAINT ck_professores_remuneracoes_valor_valido
        CHECK (valor >= 0),
    CONSTRAINT ck_professores_remuneracoes_vigencia_valida
        CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio),
    CONSTRAINT ck_professores_remuneracoes_observacao_nao_vazia
        CHECK (observacao IS NULL OR btrim(observacao) <> '')
);

CREATE UNIQUE INDEX uq_professores_remuneracoes_aberta
    ON professores_remuneracoes (professor_unidade_id)
    WHERE vigencia_fim IS NULL;

CREATE UNIQUE INDEX uq_professores_remuneracoes_vigencia_inicio
    ON professores_remuneracoes
       (organizacao_id, professor_unidade_id, vigencia_inicio);

CREATE INDEX ix_professores_remuneracoes_criado_por_usuario_id
    ON professores_remuneracoes (criado_por_usuario_id);

CREATE FUNCTION proteger_inativacao_professor()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
        RAISE EXCEPTION
            'Identidade, tenant e auditoria de criacao do professor nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM professores_unidades
            WHERE organizacao_id = OLD.organizacao_id
              AND professor_id = OLD.id
              AND ativo = true
        ) THEN
        RAISE EXCEPTION
            'O professor nao pode ser inativado enquanto possuir vinculo profissional ativo.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_inativacao_professor
BEFORE UPDATE
ON professores
FOR EACH ROW
EXECUTE FUNCTION proteger_inativacao_professor();

CREATE FUNCTION proteger_estado_professor_unidade()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    professor_ativo boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF
            NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.professor_id IS DISTINCT FROM OLD.professor_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc
        THEN
            RAISE EXCEPTION
                'A identidade historica do vinculo profissional nao pode ser alterada.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    SELECT ativo
    INTO professor_ativo
    FROM professores
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.professor_id
    FOR UPDATE;

    IF NEW.ativo = true AND professor_ativo = false THEN
        RAISE EXCEPTION
            'Um vinculo profissional ativo exige um professor ativo.'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF OLD.ativo = true
            AND NEW.ativo = false
            AND EXISTS (
                SELECT 1
                FROM professores_remuneracoes
                WHERE organizacao_id = OLD.organizacao_id
                  AND professor_unidade_id = OLD.id
                  AND vigencia_fim IS NULL
            ) THEN
            RAISE EXCEPTION
                'O vinculo profissional nao pode ser inativado enquanto possuir remuneracao aberta.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_estado_professor_unidade
BEFORE INSERT OR UPDATE
ON professores_unidades
FOR EACH ROW
EXECUTE FUNCTION proteger_estado_professor_unidade();

CREATE FUNCTION proteger_remuneracao_professor()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM 1
    FROM professores_unidades
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.professor_unidade_id
    FOR UPDATE;

    IF NEW.vigencia_fim IS NULL
        AND EXISTS (
            SELECT 1
            FROM professores_unidades
            WHERE organizacao_id = NEW.organizacao_id
              AND id = NEW.professor_unidade_id
              AND ativo = false
        ) THEN
        RAISE EXCEPTION
            'Uma remuneracao aberta exige um vinculo profissional ativo.'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.professor_unidade_id IS DISTINCT FROM OLD.professor_unidade_id
            OR NEW.modalidade IS DISTINCT FROM OLD.modalidade
            OR NEW.valor IS DISTINCT FROM OLD.valor
            OR NEW.vigencia_inicio IS DISTINCT FROM OLD.vigencia_inicio
            OR NEW.observacao IS DISTINCT FROM OLD.observacao
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'Os dados historicos da remuneracao do professor nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.vigencia_fim IS NOT NULL
            AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim THEN
            RAISE EXCEPTION
                'A vigencia final da remuneracao do professor nao pode ser alterada novamente.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM professores_remuneracoes AS existente
        WHERE existente.organizacao_id = NEW.organizacao_id
          AND existente.professor_unidade_id = NEW.professor_unidade_id
          AND existente.id <> NEW.id
          AND daterange(
                  existente.vigencia_inicio,
                  existente.vigencia_fim,
                  '[]')
              && daterange(NEW.vigencia_inicio, NEW.vigencia_fim, '[]')
    ) THEN
        RAISE EXCEPTION
            'O periodo da remuneracao do professor sobrepoe uma vigencia existente.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_remuneracao_professor
BEFORE INSERT OR UPDATE
ON professores_remuneracoes
FOR EACH ROW
EXECUTE FUNCTION proteger_remuneracao_professor();

GRANT SELECT, INSERT, UPDATE
    ON TABLE professores
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE professores_unidades
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE professores_remuneracoes
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V008', 'criar professores e historico de remuneracoes');

COMMIT;
