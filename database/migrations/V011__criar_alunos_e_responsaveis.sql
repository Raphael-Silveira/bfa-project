BEGIN;

CREATE TABLE alunos (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    usuario_id uuid NULL,
    nome_completo varchar(150) NOT NULL,
    data_nascimento date NOT NULL,
    cpf varchar(11) NULL,
    telefone varchar(30) NULL,
    email varchar(256) NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_alunos PRIMARY KEY (id),
    CONSTRAINT uq_alunos_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_alunos_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_alunos_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_alunos_nome_completo_nao_vazio
        CHECK (btrim(nome_completo) <> ''),
    CONSTRAINT ck_alunos_data_nascimento_nao_futura
        CHECK (data_nascimento <= CURRENT_DATE),
    CONSTRAINT ck_alunos_cpf_valido
        CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
    CONSTRAINT ck_alunos_telefone_nao_vazio
        CHECK (telefone IS NULL OR btrim(telefone) <> ''),
    CONSTRAINT ck_alunos_email_nao_vazio
        CHECK (email IS NULL OR btrim(email) <> '')
);

COMMENT ON CONSTRAINT ck_alunos_data_nascimento_nao_futura ON alunos IS
    'Protecao de integridade na data civil do banco; calculos operacionais de idade usam a data civil do contexto BFA no Domain/Application.';

CREATE UNIQUE INDEX uq_alunos_organizacao_cpf
    ON alunos (organizacao_id, cpf)
    WHERE cpf IS NOT NULL;

CREATE UNIQUE INDEX uq_alunos_organizacao_usuario
    ON alunos (organizacao_id, usuario_id)
    WHERE usuario_id IS NOT NULL;

CREATE INDEX ix_alunos_organizacao_ativo
    ON alunos (organizacao_id, ativo);

CREATE INDEX ix_alunos_usuario_id
    ON alunos (usuario_id);

CREATE TABLE responsaveis (
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
    CONSTRAINT pk_responsaveis PRIMARY KEY (id),
    CONSTRAINT uq_responsaveis_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_responsaveis_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_responsaveis_usuario
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_responsaveis_nome_completo_nao_vazio
        CHECK (btrim(nome_completo) <> ''),
    CONSTRAINT ck_responsaveis_cpf_valido
        CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$'),
    CONSTRAINT ck_responsaveis_telefone_nao_vazio
        CHECK (telefone IS NULL OR btrim(telefone) <> ''),
    CONSTRAINT ck_responsaveis_email_nao_vazio
        CHECK (email IS NULL OR btrim(email) <> ''),
    CONSTRAINT ck_responsaveis_contato_obrigatorio
        CHECK (telefone IS NOT NULL OR email IS NOT NULL)
);

CREATE UNIQUE INDEX uq_responsaveis_organizacao_cpf
    ON responsaveis (organizacao_id, cpf)
    WHERE cpf IS NOT NULL;

CREATE UNIQUE INDEX uq_responsaveis_organizacao_usuario
    ON responsaveis (organizacao_id, usuario_id)
    WHERE usuario_id IS NOT NULL;

CREATE INDEX ix_responsaveis_organizacao_ativo
    ON responsaveis (organizacao_id, ativo);

CREATE INDEX ix_responsaveis_usuario_id
    ON responsaveis (usuario_id);

CREATE TABLE alunos_responsaveis (
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
    CONSTRAINT pk_alunos_responsaveis PRIMARY KEY (id),
    CONSTRAINT uq_alunos_responsaveis_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_alunos_responsaveis_aluno
        FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_alunos_responsaveis_responsavel
        FOREIGN KEY (organizacao_id, responsavel_id)
        REFERENCES responsaveis (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_alunos_responsaveis_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_alunos_responsaveis_tipo_relacao_valido
        CHECK (
            tipo_relacao IN (
                'Pai',
                'Mae',
                'ResponsavelLegal',
                'Tutor',
                'Avo',
                'Outro'
            )
        ),
    CONSTRAINT ck_alunos_responsaveis_descricao_relacao_valida
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

CREATE UNIQUE INDEX uq_alunos_responsaveis_aluno_responsavel
    ON alunos_responsaveis (organizacao_id, aluno_id, responsavel_id);

CREATE UNIQUE INDEX uq_alunos_responsaveis_principal_ativo
    ON alunos_responsaveis (organizacao_id, aluno_id)
    WHERE principal_contato = true AND ativo = true;

CREATE INDEX ix_alunos_responsaveis_organizacao_aluno_ativo
    ON alunos_responsaveis (organizacao_id, aluno_id, ativo);

CREATE INDEX ix_alunos_responsaveis_organizacao_responsavel_ativo
    ON alunos_responsaveis (organizacao_id, responsavel_id, ativo);

CREATE FUNCTION proteger_aluno()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
        RAISE EXCEPTION
            'A identidade, o tenant e a auditoria de criacao do aluno nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM alunos_responsaveis
            WHERE organizacao_id = OLD.organizacao_id
              AND aluno_id = OLD.id
              AND ativo = true
        ) THEN
        RAISE EXCEPTION
            'O aluno nao pode ser inativado enquanto possuir vinculo de responsavel ativo.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_aluno
BEFORE UPDATE
ON alunos
FOR EACH ROW
EXECUTE FUNCTION proteger_aluno();

CREATE FUNCTION proteger_responsavel()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
        RAISE EXCEPTION
            'A identidade, o tenant e a auditoria de criacao do responsavel nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM alunos_responsaveis
            WHERE organizacao_id = OLD.organizacao_id
              AND responsavel_id = OLD.id
              AND ativo = true
        ) THEN
        RAISE EXCEPTION
            'O responsavel nao pode ser inativado enquanto possuir vinculo com aluno ativo.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_responsavel
BEFORE UPDATE
ON responsaveis
FOR EACH ROW
EXECUTE FUNCTION proteger_responsavel();

CREATE FUNCTION proteger_aluno_responsavel()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    aluno_ativo boolean;
    responsavel_ativo boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
            OR NEW.responsavel_id IS DISTINCT FROM OLD.responsavel_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade historica do vinculo entre aluno e responsavel nao pode ser alterada.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    SELECT ativo
    INTO aluno_ativo
    FROM alunos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.aluno_id
    FOR UPDATE;

    SELECT ativo
    INTO responsavel_ativo
    FROM responsaveis
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.responsavel_id
    FOR UPDATE;

    IF NEW.ativo = true AND aluno_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Um vinculo ativo exige um aluno ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.ativo = true AND responsavel_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Um vinculo ativo exige um responsavel ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_aluno_responsavel
BEFORE INSERT OR UPDATE
ON alunos_responsaveis
FOR EACH ROW
EXECUTE FUNCTION proteger_aluno_responsavel();

GRANT SELECT, INSERT, UPDATE
    ON TABLE alunos
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE responsaveis
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE alunos_responsaveis
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V011', 'criar alunos, responsaveis e seus vinculos');

COMMIT;
