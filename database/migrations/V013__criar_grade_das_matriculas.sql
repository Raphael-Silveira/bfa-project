BEGIN;

ALTER TABLE turmas_horarios
    ADD CONSTRAINT uq_turmas_horarios_organizacao_unidade_id
    UNIQUE (organizacao_id, unidade_id, id);

CREATE TABLE matriculas_horarios (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    matricula_id uuid NOT NULL,
    turma_horario_id uuid NOT NULL,
    vigencia_inicio date NOT NULL,
    vigencia_fim date NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_matriculas_horarios PRIMARY KEY (id),
    CONSTRAINT uq_matriculas_horarios_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_matriculas_horarios_matricula
        FOREIGN KEY (organizacao_id, unidade_id, matricula_id)
        REFERENCES matriculas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_horarios_turma_horario
        FOREIGN KEY (organizacao_id, unidade_id, turma_horario_id)
        REFERENCES turmas_horarios (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_horarios_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_matriculas_horarios_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_matriculas_horarios_vigencia_valida
        CHECK (vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio)
);

CREATE UNIQUE INDEX uq_matriculas_horarios_aberto
    ON matriculas_horarios
       (organizacao_id, unidade_id, matricula_id, turma_horario_id)
    WHERE vigencia_fim IS NULL;

CREATE UNIQUE INDEX uq_matriculas_horarios_historico
    ON matriculas_horarios
       (organizacao_id, matricula_id, turma_horario_id, vigencia_inicio);

CREATE INDEX ix_matriculas_horarios_organizacao_unidade_matricula
    ON matriculas_horarios (organizacao_id, unidade_id, matricula_id);

CREATE INDEX ix_matriculas_horarios_organizacao_unidade_turma_horario
    ON matriculas_horarios (organizacao_id, unidade_id, turma_horario_id);

CREATE INDEX ix_matriculas_horarios_abertos_matricula
    ON matriculas_horarios (organizacao_id, unidade_id, matricula_id)
    WHERE vigencia_fim IS NULL;

CREATE INDEX ix_matriculas_horarios_abertos_turma_horario
    ON matriculas_horarios (organizacao_id, unidade_id, turma_horario_id, vigencia_inicio)
    WHERE vigencia_fim IS NULL;

CREATE INDEX ix_matriculas_horarios_criado_por_usuario_id
    ON matriculas_horarios (criado_por_usuario_id);

CREATE INDEX ix_matriculas_horarios_atualizado_por_usuario_id
    ON matriculas_horarios (atualizado_por_usuario_id);

CREATE FUNCTION proteger_matricula_horario()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    aluno_id_matricula uuid;
    plano_versao_id_matricula uuid;
    data_inicio_matricula date;
    data_fim_prevista_matricula date;
    data_fim_real_matricula date;
    status_matricula varchar(20);
    aluno_ativo boolean;
    frequencia_semanal_plano smallint;
    turma_id_horario uuid;
    dia_semana_horario smallint;
    hora_inicio_horario time without time zone;
    hora_fim_horario time without time zone;
    vigencia_inicio_horario date;
    vigencia_fim_horario date;
    horario_ativo boolean;
    turma_ativa boolean;
    capacidade_turma integer;
    maximo_simultaneo integer;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION
            'O historico da Grade nao pode ser excluido.'
            USING ERRCODE = '23514';
    END IF;

    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.matricula_id IS DISTINCT FROM OLD.matricula_id
            OR NEW.turma_horario_id IS DISTINCT FROM OLD.turma_horario_id
            OR NEW.vigencia_inicio IS DISTINCT FROM OLD.vigencia_inicio
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o slot, a vigencia inicial e a auditoria de criacao da Grade nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.vigencia_fim IS NOT NULL
            AND NEW.vigencia_fim IS DISTINCT FROM OLD.vigencia_fim THEN
            RAISE EXCEPTION
                'A vigencia final da Grade nao pode ser alterada novamente nem voltar a nulo.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    IF TG_OP = 'INSERT' AND NEW.vigencia_fim IS NOT NULL THEN
        RAISE EXCEPTION
            'Um horario da matricula deve ser criado aberto; o historico surge somente pelo fechamento posterior.'
            USING ERRCODE = '23514';
    END IF;

    -- Ordem definitiva: Matricula -> Aluno -> TurmaHorario(s em ordem de id no lote).
    SELECT aluno_id, plano_versao_id, data_inicio, data_fim_prevista,
           data_fim_real, status
    INTO aluno_id_matricula, plano_versao_id_matricula, data_inicio_matricula,
         data_fim_prevista_matricula, data_fim_real_matricula, status_matricula
    FROM matriculas
    WHERE organizacao_id = NEW.organizacao_id
      AND unidade_id = NEW.unidade_id
      AND id = NEW.matricula_id
    FOR UPDATE;

    IF aluno_id_matricula IS NULL THEN
        RAISE EXCEPTION
            'A Grade exige uma matricula da mesma Organizacao e Unidade.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo
    INTO aluno_ativo
    FROM alunos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = aluno_id_matricula
    FOR UPDATE;

    SELECT turma_id, dia_semana, hora_inicio, hora_fim,
           vigencia_inicio, vigencia_fim, ativo
    INTO turma_id_horario, dia_semana_horario, hora_inicio_horario,
         hora_fim_horario, vigencia_inicio_horario, vigencia_fim_horario,
         horario_ativo
    FROM turmas_horarios
    WHERE organizacao_id = NEW.organizacao_id
      AND unidade_id = NEW.unidade_id
      AND id = NEW.turma_horario_id
    FOR UPDATE;

    IF turma_id_horario IS NULL THEN
        RAISE EXCEPTION
            'A Grade exige um horario recorrente da mesma Organizacao e Unidade.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo, capacidade
    INTO turma_ativa, capacidade_turma
    FROM turmas
    WHERE organizacao_id = NEW.organizacao_id
      AND unidade_id = NEW.unidade_id
      AND id = turma_id_horario;

    SELECT frequencia_semanal
    INTO frequencia_semanal_plano
    FROM planos_versoes
    WHERE organizacao_id = NEW.organizacao_id
      AND id = plano_versao_id_matricula;

    IF NEW.vigencia_inicio < data_inicio_matricula
        OR NEW.vigencia_inicio > data_fim_prevista_matricula THEN
        RAISE EXCEPTION
            'A Grade deve iniciar dentro da vigencia prevista da matricula.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.vigencia_fim IS NOT NULL
        AND NEW.vigencia_fim > data_fim_prevista_matricula THEN
        RAISE EXCEPTION
            'A Grade nao pode terminar depois da vigencia prevista da matricula.'
            USING ERRCODE = '23514';
    END IF;

    IF status_matricula IN ('Encerrada', 'Cancelada')
        AND (
            NEW.vigencia_fim IS NULL
            OR NEW.vigencia_fim > data_fim_real_matricula
        ) THEN
        RAISE EXCEPTION
            'A Grade historica deve terminar ate a data final real da matricula.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.vigencia_inicio < vigencia_inicio_horario
        OR (
            vigencia_fim_horario IS NOT NULL
            AND (
                NEW.vigencia_fim IS NULL
                OR NEW.vigencia_fim > vigencia_fim_horario
            )
        ) THEN
        RAISE EXCEPTION
            'A vigencia da Grade deve estar contida na vigencia do horario recorrente.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.vigencia_fim IS NULL THEN
        IF status_matricula <> 'Ativa' THEN
            RAISE EXCEPTION
                'Uma nova Grade aberta exige matricula ativa.'
                USING ERRCODE = '23514';
        END IF;

        IF aluno_ativo IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma nova Grade aberta exige aluno ativo.'
                USING ERRCODE = '23514';
        END IF;

        IF turma_ativa IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma nova Grade aberta exige turma ativa.'
                USING ERRCODE = '23514';
        END IF;

        IF horario_ativo IS DISTINCT FROM true OR vigencia_fim_horario IS NOT NULL THEN
            RAISE EXCEPTION
                'Uma nova Grade aberta exige horario recorrente ativo e aberto.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    WITH intervalos AS (
        SELECT existente.id, existente.vigencia_inicio, existente.vigencia_fim
        FROM matriculas_horarios AS existente
        WHERE existente.organizacao_id = NEW.organizacao_id
          AND existente.unidade_id = NEW.unidade_id
          AND existente.matricula_id = NEW.matricula_id
          AND existente.id <> NEW.id
        UNION ALL
        SELECT NEW.id, NEW.vigencia_inicio, NEW.vigencia_fim
    ),
    pontos AS (
        SELECT DISTINCT vigencia_inicio AS data_referencia
        FROM intervalos
    )
    SELECT COALESCE(MAX((
        SELECT COUNT(*)
        FROM intervalos AS intervalo
        WHERE intervalo.vigencia_inicio <= ponto.data_referencia
          AND (
              intervalo.vigencia_fim IS NULL
              OR intervalo.vigencia_fim >= ponto.data_referencia
          )
    )), 0)
    INTO maximo_simultaneo
    FROM pontos AS ponto;

    IF maximo_simultaneo > frequencia_semanal_plano THEN
        RAISE EXCEPTION
            'A Grade ultrapassa a frequencia semanal contratada.'
            USING ERRCODE = '23514';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM matriculas_horarios AS existente
        INNER JOIN matriculas AS matricula_existente
            ON matricula_existente.organizacao_id = existente.organizacao_id
           AND matricula_existente.unidade_id = existente.unidade_id
           AND matricula_existente.id = existente.matricula_id
        INNER JOIN turmas_horarios AS horario_existente
            ON horario_existente.organizacao_id = existente.organizacao_id
           AND horario_existente.unidade_id = existente.unidade_id
           AND horario_existente.id = existente.turma_horario_id
        WHERE matricula_existente.organizacao_id = NEW.organizacao_id
          AND matricula_existente.aluno_id = aluno_id_matricula
          AND existente.id <> NEW.id
          AND horario_existente.dia_semana = dia_semana_horario
          AND hora_inicio_horario < horario_existente.hora_fim
          AND horario_existente.hora_inicio < hora_fim_horario
          AND existente.vigencia_inicio <= COALESCE(NEW.vigencia_fim, 'infinity'::date)
          AND NEW.vigencia_inicio <= COALESCE(existente.vigencia_fim, 'infinity'::date)
    ) THEN
        RAISE EXCEPTION
            'O aluno ja ocupa outro horario recorrente conflitante nesse periodo.'
            USING ERRCODE = '23514';
    END IF;

    WITH intervalos AS (
        SELECT existente.id, existente.vigencia_inicio, existente.vigencia_fim
        FROM matriculas_horarios AS existente
        WHERE existente.organizacao_id = NEW.organizacao_id
          AND existente.unidade_id = NEW.unidade_id
          AND existente.turma_horario_id = NEW.turma_horario_id
          AND existente.id <> NEW.id
        UNION ALL
        SELECT NEW.id, NEW.vigencia_inicio, NEW.vigencia_fim
    ),
    pontos AS (
        SELECT DISTINCT vigencia_inicio AS data_referencia
        FROM intervalos
    )
    SELECT COALESCE(MAX((
        SELECT COUNT(*)
        FROM intervalos AS intervalo
        WHERE intervalo.vigencia_inicio <= ponto.data_referencia
          AND (
              intervalo.vigencia_fim IS NULL
              OR intervalo.vigencia_fim >= ponto.data_referencia
          )
    )), 0)
    INTO maximo_simultaneo
    FROM pontos AS ponto;

    IF maximo_simultaneo > capacidade_turma THEN
        RAISE EXCEPTION
            'O horario recorrente nao possui capacidade para a Grade nesse periodo.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_matricula_horario
BEFORE INSERT OR UPDATE OR DELETE
ON matriculas_horarios
FOR EACH ROW
EXECUTE FUNCTION proteger_matricula_horario();

CREATE FUNCTION proteger_matricula_grade_aberta()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'Ativa'
        AND NEW.status IN ('Encerrada', 'Cancelada')
        AND EXISTS (
            SELECT 1
            FROM matriculas_horarios
            WHERE organizacao_id = OLD.organizacao_id
              AND unidade_id = OLD.unidade_id
              AND matricula_id = OLD.id
              AND (
                  vigencia_fim IS NULL
                  OR vigencia_fim > NEW.data_fim_real
              )
        ) THEN
        RAISE EXCEPTION
            'A matricula nao pode ser finalizada antes do encerramento de toda a sua Grade.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_matricula_grade_aberta
BEFORE UPDATE
ON matriculas
FOR EACH ROW
EXECUTE FUNCTION proteger_matricula_grade_aberta();

CREATE FUNCTION proteger_turma_horario_grade_aberta()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.vigencia_fim IS NULL
        AND NEW.vigencia_fim IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM matriculas_horarios
            WHERE organizacao_id = OLD.organizacao_id
              AND unidade_id = OLD.unidade_id
              AND turma_horario_id = OLD.id
              AND (
                  vigencia_fim IS NULL
                  OR vigencia_fim > NEW.vigencia_fim
              )
        ) THEN
        RAISE EXCEPTION
            'O horario recorrente nao pode terminar antes do encerramento de toda a Grade vinculada.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.ativo = true
        AND NEW.ativo = false
        AND EXISTS (
            SELECT 1
            FROM matriculas_horarios
            WHERE organizacao_id = OLD.organizacao_id
              AND unidade_id = OLD.unidade_id
              AND turma_horario_id = OLD.id
              AND (
                  vigencia_fim IS NULL
                  OR vigencia_fim >= CURRENT_DATE
              )
        ) THEN
        RAISE EXCEPTION
            'O horario recorrente nao pode ser inativado enquanto possuir compromisso atual ou futuro de Grade.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_turma_horario_grade_aberta
BEFORE UPDATE
ON turmas_horarios
FOR EACH ROW
EXECUTE FUNCTION proteger_turma_horario_grade_aberta();

CREATE FUNCTION proteger_capacidade_turma_grade()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    maximo_simultaneo integer;
BEGIN
    IF NEW.capacidade >= OLD.capacidade THEN
        RETURN NEW;
    END IF;

    -- Serializa com inclusoes de Grade; o gatilho da Grade nao bloqueia Turma,
    -- preservando compatibilidade com a ordem de locks da V009.
    PERFORM 1
    FROM turmas_horarios
    WHERE organizacao_id = OLD.organizacao_id
      AND unidade_id = OLD.unidade_id
      AND turma_id = OLD.id
    ORDER BY id
    FOR UPDATE;

    WITH intervalos AS (
        SELECT horario.id AS turma_horario_id,
               grade.vigencia_inicio,
               grade.vigencia_fim
        FROM turmas_horarios AS horario
        INNER JOIN matriculas_horarios AS grade
            ON grade.organizacao_id = horario.organizacao_id
           AND grade.unidade_id = horario.unidade_id
           AND grade.turma_horario_id = horario.id
        WHERE horario.organizacao_id = OLD.organizacao_id
          AND horario.unidade_id = OLD.unidade_id
          AND horario.turma_id = OLD.id
          AND (
              grade.vigencia_fim IS NULL
              OR grade.vigencia_fim >= CURRENT_DATE
          )
    ),
    pontos AS (
        SELECT DISTINCT turma_horario_id, CURRENT_DATE AS data_referencia
        FROM intervalos
        UNION
        SELECT DISTINCT turma_horario_id, vigencia_inicio
        FROM intervalos
        WHERE vigencia_inicio > CURRENT_DATE
    )
    SELECT COALESCE(MAX((
        SELECT COUNT(*)
        FROM intervalos AS intervalo
        WHERE intervalo.turma_horario_id = ponto.turma_horario_id
          AND intervalo.vigencia_inicio <= ponto.data_referencia
          AND (
              intervalo.vigencia_fim IS NULL
              OR intervalo.vigencia_fim >= ponto.data_referencia
          )
    )), 0)
    INTO maximo_simultaneo
    FROM pontos AS ponto;

    IF NEW.capacidade < maximo_simultaneo THEN
        RAISE EXCEPTION
            'A capacidade da turma nao pode ficar abaixo da ocupacao atual ou futura da Grade.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_capacidade_turma_grade
BEFORE UPDATE
ON turmas
FOR EACH ROW
EXECUTE FUNCTION proteger_capacidade_turma_grade();

GRANT SELECT, INSERT, UPDATE
    ON TABLE matriculas_horarios
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V013', 'criar grade das matriculas');

COMMIT;
