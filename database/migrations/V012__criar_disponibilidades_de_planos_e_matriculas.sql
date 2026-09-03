BEGIN;

CREATE TABLE planos_disponibilidades_unidades (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    plano_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    ativo boolean NOT NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_planos_disponibilidades_unidades PRIMARY KEY (id),
    CONSTRAINT uq_planos_disponibilidades_unidades_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT uq_planos_disponibilidades_unidades_organizacao_plano_unidade
        UNIQUE (organizacao_id, plano_id, unidade_id),
    CONSTRAINT fk_planos_disponibilidades_unidades_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_disponibilidades_unidades_plano
        FOREIGN KEY (organizacao_id, plano_id)
        REFERENCES planos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_disponibilidades_unidades_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_disponibilidades_unidades_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_planos_disponibilidades_unidades_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT
);

CREATE INDEX ix_planos_disponibilidades_unidades_organizacao_unidade_ativo
    ON planos_disponibilidades_unidades
       (organizacao_id, unidade_id, ativo);

CREATE INDEX ix_planos_disponibilidades_unidades_organizacao_plano_ativo
    ON planos_disponibilidades_unidades
       (organizacao_id, plano_id, ativo);

CREATE INDEX ix_planos_disponibilidades_unidades_criado_por_usuario_id
    ON planos_disponibilidades_unidades (criado_por_usuario_id);

CREATE INDEX ix_planos_disponibilidades_unidades_atualizado_por_usuario_id
    ON planos_disponibilidades_unidades (atualizado_por_usuario_id);

CREATE FUNCTION proteger_plano_disponibilidade_unidade()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    plano_ativo boolean;
    plano_unidade_id uuid;
    unidade_ativa boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.plano_id IS DISTINCT FROM OLD.plano_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o escopo e a auditoria de criacao da disponibilidade nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    SELECT ativo, unidade_id
    INTO plano_ativo, plano_unidade_id
    FROM planos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.plano_id;

    IF plano_ativo IS NULL THEN
        RAISE EXCEPTION
            'A disponibilidade exige um plano do mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF plano_unidade_id IS NOT NULL THEN
        RAISE EXCEPTION
            'Somente um plano da rede pode possuir disponibilidade por unidade.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.ativo = true THEN
        SELECT ativo
        INTO unidade_ativa
        FROM unidades
        WHERE organizacao_id = NEW.organizacao_id
          AND id = NEW.unidade_id;

        IF plano_ativo IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma disponibilidade ativa exige um plano da rede ativo.'
                USING ERRCODE = '23514';
        END IF;

        IF unidade_ativa IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma disponibilidade ativa exige uma unidade ativa no mesmo tenant.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_plano_disponibilidade_unidade
BEFORE INSERT OR UPDATE
ON planos_disponibilidades_unidades
FOR EACH ROW
EXECUTE FUNCTION proteger_plano_disponibilidade_unidade();

CREATE TABLE matriculas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aluno_id uuid NOT NULL,
    plano_versao_id uuid NOT NULL,
    data_inicio date NOT NULL,
    data_fim_prevista date NOT NULL,
    data_fim_real date NULL,
    status varchar(20) NOT NULL,
    valor_mensal_contratado numeric(12,2) NOT NULL,
    cobra_taxa_matricula boolean NOT NULL,
    valor_taxa_matricula numeric(12,2) NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_matriculas PRIMARY KEY (id),
    CONSTRAINT uq_matriculas_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_matriculas_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_aluno
        FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_plano_versao
        FOREIGN KEY (organizacao_id, plano_versao_id)
        REFERENCES planos_versoes (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_matriculas_status_valido
        CHECK (status IN ('Ativa', 'Encerrada', 'Cancelada')),
    CONSTRAINT ck_matriculas_data_fim_prevista_valida
        CHECK (data_fim_prevista >= data_inicio),
    CONSTRAINT ck_matriculas_valor_mensal_positivo
        CHECK (valor_mensal_contratado > 0),
    CONSTRAINT ck_matriculas_taxa_valida
        CHECK (
            (
                cobra_taxa_matricula = true
                AND valor_taxa_matricula IS NOT NULL
                AND valor_taxa_matricula > 0
            )
            OR
            (
                cobra_taxa_matricula = false
                AND valor_taxa_matricula IS NULL
            )
        ),
    CONSTRAINT ck_matriculas_status_data_fim_real
        CHECK (
            (status = 'Ativa' AND data_fim_real IS NULL)
            OR
            (
                status IN ('Encerrada', 'Cancelada')
                AND data_fim_real IS NOT NULL
                AND data_fim_real >= data_inicio
            )
        )
);

CREATE UNIQUE INDEX uq_matriculas_ativa_organizacao_unidade_aluno
    ON matriculas (organizacao_id, unidade_id, aluno_id)
    WHERE status = 'Ativa';

CREATE INDEX ix_matriculas_organizacao_unidade_status
    ON matriculas (organizacao_id, unidade_id, status);

CREATE INDEX ix_matriculas_organizacao_aluno_status
    ON matriculas (organizacao_id, aluno_id, status);

CREATE INDEX ix_matriculas_organizacao_unidade_aluno
    ON matriculas (organizacao_id, unidade_id, aluno_id);

CREATE INDEX ix_matriculas_organizacao_plano_versao
    ON matriculas (organizacao_id, plano_versao_id);

CREATE INDEX ix_matriculas_criado_por_usuario_id
    ON matriculas (criado_por_usuario_id);

CREATE INDEX ix_matriculas_atualizado_por_usuario_id
    ON matriculas (atualizado_por_usuario_id);

CREATE FUNCTION proteger_matricula()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    plano_id_da_versao uuid;
    vigencia_inicio_da_versao date;
    vigencia_fim_da_versao date;
    plano_ativo boolean;
    plano_unidade_id uuid;
    disponibilidade_ativa boolean;
    unidade_ativa boolean;
    aluno_ativo boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
            OR NEW.plano_versao_id IS DISTINCT FROM OLD.plano_versao_id
            OR NEW.data_inicio IS DISTINCT FROM OLD.data_inicio
            OR NEW.data_fim_prevista IS DISTINCT FROM OLD.data_fim_prevista
            OR NEW.valor_mensal_contratado IS DISTINCT FROM OLD.valor_mensal_contratado
            OR NEW.cobra_taxa_matricula IS DISTINCT FROM OLD.cobra_taxa_matricula
            OR NEW.valor_taxa_matricula IS DISTINCT FROM OLD.valor_taxa_matricula
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'O escopo e o snapshot contratual da matricula nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status IN ('Encerrada', 'Cancelada')
            AND (
                NEW.status IS DISTINCT FROM OLD.status
                OR NEW.data_fim_real IS DISTINCT FROM OLD.data_fim_real
            ) THEN
            RAISE EXCEPTION
                'Uma matricula em estado terminal nao pode ser alterada.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Ativa'
            AND NEW.status IS DISTINCT FROM OLD.status
            AND NEW.status NOT IN ('Encerrada', 'Cancelada') THEN
            RAISE EXCEPTION
                'Uma matricula ativa somente pode ser encerrada ou cancelada.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.data_fim_real IS NOT NULL
            AND NEW.data_fim_real IS DISTINCT FROM OLD.data_fim_real THEN
            RAISE EXCEPTION
                'A data final real da matricula so pode ser preenchida uma vez.'
                USING ERRCODE = '23514';
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.status <> 'Ativa' OR NEW.data_fim_real IS NOT NULL THEN
        RAISE EXCEPTION
            'Uma nova matricula deve ser criada ativa e sem data final real.'
            USING ERRCODE = '23514';
    END IF;

    SELECT plano_id, vigencia_inicio, vigencia_fim
    INTO plano_id_da_versao, vigencia_inicio_da_versao, vigencia_fim_da_versao
    FROM planos_versoes
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.plano_versao_id
    FOR UPDATE;

    IF plano_id_da_versao IS NULL THEN
        RAISE EXCEPTION
            'A matricula exige uma versao de plano do mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.data_inicio < vigencia_inicio_da_versao
        OR (
            vigencia_fim_da_versao IS NOT NULL
            AND NEW.data_inicio > vigencia_fim_da_versao
        ) THEN
        RAISE EXCEPTION
            'A data de inicio deve pertencer a vigencia inclusiva da versao do plano.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo, unidade_id
    INTO plano_ativo, plano_unidade_id
    FROM planos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = plano_id_da_versao
    FOR UPDATE;

    IF plano_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige um plano ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF plano_unidade_id IS NULL THEN
        SELECT ativo
        INTO disponibilidade_ativa
        FROM planos_disponibilidades_unidades
        WHERE organizacao_id = NEW.organizacao_id
          AND plano_id = plano_id_da_versao
          AND unidade_id = NEW.unidade_id
        FOR UPDATE;

        IF disponibilidade_ativa IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'O plano da rede deve estar disponivel e ativo para a unidade da matricula.'
                USING ERRCODE = '23514';
        END IF;
    ELSIF plano_unidade_id <> NEW.unidade_id THEN
        RAISE EXCEPTION
            'Um plano local somente pode ser contratado em sua propria unidade.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo
    INTO unidade_ativa
    FROM unidades
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.unidade_id
    FOR UPDATE;

    IF unidade_ativa IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige uma unidade ativa no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo
    INTO aluno_ativo
    FROM alunos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.aluno_id
    FOR UPDATE;

    IF aluno_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige um aluno ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_matricula
BEFORE INSERT OR UPDATE
ON matriculas
FOR EACH ROW
EXECUTE FUNCTION proteger_matricula();

CREATE FUNCTION proteger_plano_versao_matriculas()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.vigencia_fim IS NULL
        AND NEW.vigencia_fim IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM matriculas
            WHERE organizacao_id = NEW.organizacao_id
              AND plano_versao_id = NEW.id
              AND data_inicio > NEW.vigencia_fim
        ) THEN
        RAISE EXCEPTION
            'A vigencia da versao nao pode ser encerrada antes do inicio de uma matricula que ja utiliza esta versao.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_plano_versao_matriculas
BEFORE UPDATE
ON planos_versoes
FOR EACH ROW
EXECUTE FUNCTION proteger_plano_versao_matriculas();

CREATE FUNCTION proteger_aluno_matriculas()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM matriculas
            WHERE organizacao_id = OLD.organizacao_id
              AND aluno_id = OLD.id
              AND status = 'Ativa'
        ) THEN
        RAISE EXCEPTION
            'O aluno nao pode ser inativado enquanto possuir matricula ativa.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_aluno_matriculas
BEFORE UPDATE
ON alunos
FOR EACH ROW
EXECUTE FUNCTION proteger_aluno_matriculas();

GRANT SELECT, INSERT, UPDATE
    ON TABLE planos_disponibilidades_unidades
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE matriculas
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V012', 'criar disponibilidades de planos e matriculas');

COMMIT;
