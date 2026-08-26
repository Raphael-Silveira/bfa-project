BEGIN;

ALTER TABLE professores_unidades
    ADD CONSTRAINT uq_professores_unidades_organizacao_unidade_id
    UNIQUE (organizacao_id, unidade_id, id);

CREATE TABLE turmas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    professor_unidade_id uuid NOT NULL,
    nome varchar(150) NOT NULL,
    capacidade integer NOT NULL,
    ativo boolean NOT NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_turmas PRIMARY KEY (id),
    CONSTRAINT uq_turmas_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_turmas_organizacoes_organizacao_id
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_professor_unidade
        FOREIGN KEY (organizacao_id, unidade_id, professor_unidade_id)
        REFERENCES professores_unidades (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_turmas_nome_nao_vazio
        CHECK (btrim(nome) <> ''),
    CONSTRAINT ck_turmas_capacidade_valida
        CHECK (capacidade > 0)
);

CREATE INDEX ix_turmas_organizacao_unidade_ativo
    ON turmas (organizacao_id, unidade_id, ativo);

CREATE INDEX ix_turmas_organizacao_professor_unidade_ativo
    ON turmas (organizacao_id, professor_unidade_id, ativo);

CREATE INDEX ix_turmas_criado_por_usuario_id
    ON turmas (criado_por_usuario_id);

CREATE INDEX ix_turmas_atualizado_por_usuario_id
    ON turmas (atualizado_por_usuario_id);

CREATE TABLE turmas_horarios (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    turma_id uuid NOT NULL,
    professor_unidade_id uuid NOT NULL,
    dia_semana smallint NOT NULL,
    hora_inicio time without time zone NOT NULL,
    hora_fim time without time zone NOT NULL,
    vigencia_inicio date NOT NULL,
    vigencia_fim date NULL,
    ativo boolean NOT NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_turmas_horarios PRIMARY KEY (id),
    CONSTRAINT fk_turmas_horarios_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_horarios_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_horarios_turma
        FOREIGN KEY (organizacao_id, unidade_id, turma_id)
        REFERENCES turmas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_horarios_professor_unidade
        FOREIGN KEY (organizacao_id, unidade_id, professor_unidade_id)
        REFERENCES professores_unidades (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_horarios_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_turmas_horarios_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_turmas_horarios_dia_semana_valido
        CHECK (dia_semana BETWEEN 1 AND 7),
    CONSTRAINT ck_turmas_horarios_intervalo_valido
        CHECK (hora_inicio < hora_fim),
    CONSTRAINT ck_turmas_horarios_vigencia_valida
        CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)
);

CREATE UNIQUE INDEX uq_turmas_horarios_regra
    ON turmas_horarios
       (organizacao_id, turma_id, dia_semana, hora_inicio, hora_fim, vigencia_inicio);

CREATE INDEX ix_turmas_horarios_organizacao_unidade_dia_ativo
    ON turmas_horarios (organizacao_id, unidade_id, dia_semana, ativo);

CREATE INDEX ix_turmas_horarios_organizacao_turma_ativo
    ON turmas_horarios (organizacao_id, turma_id, ativo);

CREATE INDEX ix_turmas_horarios_conflito_professor
    ON turmas_horarios
       (organizacao_id, professor_unidade_id, dia_semana, ativo, hora_inicio, hora_fim);

CREATE INDEX ix_turmas_horarios_criado_por_usuario_id
    ON turmas_horarios (criado_por_usuario_id);

CREATE INDEX ix_turmas_horarios_atualizado_por_usuario_id
    ON turmas_horarios (atualizado_por_usuario_id);

CREATE FUNCTION validar_conflito_horario_professor(
    p_organizacao_id uuid,
    p_unidade_id uuid,
    p_horario_id uuid,
    p_turma_id uuid,
    p_professor_unidade_id uuid,
    p_dia_semana smallint,
    p_hora_inicio time without time zone,
    p_hora_fim time without time zone,
    p_vigencia_inicio date,
    p_vigencia_fim date)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    turma_ativa boolean;
    professor_unidade_id_turma uuid;
    vinculo_ativo boolean;
    professor_id_atual uuid;
BEGIN
    SELECT ativo, professor_unidade_id
    INTO turma_ativa, professor_unidade_id_turma
    FROM turmas
    WHERE organizacao_id = p_organizacao_id
      AND unidade_id = p_unidade_id
      AND id = p_turma_id
    FOR UPDATE;

    IF turma_ativa IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Um horario recorrente ativo exige uma turma ativa.'
            USING ERRCODE = '23514';
    END IF;

    IF professor_unidade_id_turma IS DISTINCT FROM p_professor_unidade_id THEN
        RAISE EXCEPTION
            'Um horario recorrente ativo deve registrar o professor responsavel atual da turma.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo, professor_id
    INTO vinculo_ativo, professor_id_atual
    FROM professores_unidades
    WHERE organizacao_id = p_organizacao_id
      AND unidade_id = p_unidade_id
      AND id = p_professor_unidade_id
    FOR UPDATE;

    IF vinculo_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Um horario recorrente ativo exige um vinculo profissional ativo.'
            USING ERRCODE = '23514';
    END IF;

    PERFORM 1
    FROM professores
    WHERE organizacao_id = p_organizacao_id
      AND id = professor_id_atual
    FOR UPDATE;

    IF EXISTS (
        SELECT 1
        FROM turmas_horarios AS existente
        INNER JOIN turmas AS turma_existente
            ON turma_existente.organizacao_id = existente.organizacao_id
           AND turma_existente.unidade_id = existente.unidade_id
           AND turma_existente.id = existente.turma_id
        INNER JOIN professores_unidades AS vinculo_existente
            ON vinculo_existente.organizacao_id = existente.organizacao_id
           AND vinculo_existente.unidade_id = existente.unidade_id
           AND vinculo_existente.id = existente.professor_unidade_id
        WHERE existente.organizacao_id = p_organizacao_id
          AND existente.id <> p_horario_id
          AND existente.ativo = true
          AND turma_existente.ativo = true
          AND vinculo_existente.professor_id = professor_id_atual
          AND existente.dia_semana = p_dia_semana
          AND p_hora_inicio < existente.hora_fim
          AND existente.hora_inicio < p_hora_fim
          AND daterange(
                  existente.vigencia_inicio,
                  existente.vigencia_fim,
                  '[]')
              && daterange(p_vigencia_inicio, p_vigencia_fim, '[]')
    ) THEN
        RAISE EXCEPTION
            'O professor responsavel possui horario recorrente conflitante.'
            USING ERRCODE = '23514';
    END IF;
END;
$$;

CREATE FUNCTION proteger_estado_turma()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    vinculo_ativo boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o tenant e a auditoria de criacao da turma nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.ativo = true
            AND NEW.ativo = false
            AND EXISTS (
                SELECT 1
                FROM turmas_horarios
                WHERE organizacao_id = OLD.organizacao_id
                  AND turma_id = OLD.id
                  AND ativo = true
            ) THEN
            RAISE EXCEPTION
                'A turma nao pode ser inativada enquanto possuir horario recorrente ativo.'
                USING ERRCODE = '23514';
        END IF;

        IF NEW.professor_unidade_id IS DISTINCT FROM OLD.professor_unidade_id
            AND EXISTS (
                SELECT 1
                FROM turmas_horarios
                WHERE organizacao_id = OLD.organizacao_id
                  AND unidade_id = OLD.unidade_id
                  AND turma_id = OLD.id
                  AND professor_unidade_id = OLD.professor_unidade_id
                  AND ativo = true
                  AND vigencia_fim IS NULL
            ) THEN
            RAISE EXCEPTION
                'O professor responsavel nao pode ser trocado enquanto possuir horario recorrente ativo e aberto.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    SELECT ativo
    INTO vinculo_ativo
    FROM professores_unidades
    WHERE organizacao_id = NEW.organizacao_id
      AND unidade_id = NEW.unidade_id
      AND id = NEW.professor_unidade_id
    FOR UPDATE;

    IF NEW.ativo = true AND vinculo_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma turma ativa exige um vinculo profissional ativo na mesma unidade.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_estado_turma
BEFORE INSERT OR UPDATE
ON turmas
FOR EACH ROW
EXECUTE FUNCTION proteger_estado_turma();

CREATE FUNCTION proteger_turma_horario()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.turma_id IS DISTINCT FROM OLD.turma_id
            OR NEW.professor_unidade_id IS DISTINCT FROM OLD.professor_unidade_id
            OR NEW.dia_semana IS DISTINCT FROM OLD.dia_semana
            OR NEW.hora_inicio IS DISTINCT FROM OLD.hora_inicio
            OR NEW.hora_fim IS DISTINCT FROM OLD.hora_fim
            OR NEW.vigencia_inicio IS DISTINCT FROM OLD.vigencia_inicio
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade e os dados historicos do horario recorrente nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.vigencia_fim IS NOT NULL
            AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim THEN
            RAISE EXCEPTION
                'A vigencia final do horario recorrente nao pode ser alterada novamente.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    IF NEW.ativo = true THEN
        PERFORM validar_conflito_horario_professor(
            NEW.organizacao_id,
            NEW.unidade_id,
            NEW.id,
            NEW.turma_id,
            NEW.professor_unidade_id,
            NEW.dia_semana,
            NEW.hora_inicio,
            NEW.hora_fim,
            NEW.vigencia_inicio,
            NEW.vigencia_fim);
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_turma_horario
BEFORE INSERT OR UPDATE
ON turmas_horarios
FOR EACH ROW
EXECUTE FUNCTION proteger_turma_horario();

CREATE FUNCTION proteger_professor_unidade_turmas()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM turmas
            WHERE organizacao_id = OLD.organizacao_id
              AND unidade_id = OLD.unidade_id
              AND professor_unidade_id = OLD.id
              AND ativo = true
        ) THEN
        RAISE EXCEPTION
            'O vinculo profissional nao pode ser inativado enquanto for responsavel por turma ativa.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_professor_unidade_turmas
BEFORE UPDATE
ON professores_unidades
FOR EACH ROW
EXECUTE FUNCTION proteger_professor_unidade_turmas();

GRANT SELECT, INSERT, UPDATE
    ON TABLE turmas
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE turmas_horarios
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V009', 'criar turmas e horarios recorrentes');

COMMIT;
