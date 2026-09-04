BEGIN;

CREATE TABLE aulas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    turma_id uuid NOT NULL,
    turma_horario_id uuid NOT NULL,
    data date NOT NULL,
    hora_inicio time without time zone NOT NULL,
    hora_fim time without time zone NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Programada',
    capacidade integer NOT NULL,
    observacoes text NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_aulas PRIMARY KEY (id),
    CONSTRAINT uq_aulas_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_aulas_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_turma
        FOREIGN KEY (organizacao_id, unidade_id, turma_id)
        REFERENCES turmas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_turma_horario
        FOREIGN KEY (organizacao_id, unidade_id, turma_horario_id)
        REFERENCES turmas_horarios (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_aulas_status_valido
        CHECK (status IN ('Programada', 'Concluida', 'Cancelada')),
    CONSTRAINT ck_aulas_intervalo_valido
        CHECK (hora_inicio < hora_fim),
    CONSTRAINT ck_aulas_capacidade_valida
        CHECK (capacidade > 0)
);

CREATE UNIQUE INDEX uq_aulas_organizacao_turma_data_hora
    ON aulas
       (organizacao_id, turma_id, data, hora_inicio);

CREATE INDEX ix_aulas_organizacao_unidade_data
    ON aulas (organizacao_id, unidade_id, data);

CREATE INDEX ix_aulas_organizacao_turma_data
    ON aulas (organizacao_id, turma_id, data);

CREATE INDEX ix_aulas_organizacao_turma_horario
    ON aulas (organizacao_id, turma_horario_id);

CREATE INDEX ix_aulas_criado_por_usuario_id
    ON aulas (criado_por_usuario_id);

CREATE INDEX ix_aulas_atualizado_por_usuario_id
    ON aulas (atualizado_por_usuario_id);

CREATE FUNCTION proteger_aula()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.turma_id IS DISTINCT FROM OLD.turma_id
            OR NEW.turma_horario_id IS DISTINCT FROM OLD.turma_horario_id
            OR NEW.data IS DISTINCT FROM OLD.data
            OR NEW.hora_inicio IS DISTINCT FROM OLD.hora_inicio
            OR NEW.hora_fim IS DISTINCT FROM OLD.hora_fim
            OR NEW.capacidade IS DISTINCT FROM OLD.capacidade
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o slot, a capacidade e a auditoria de criacao da Aula nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Concluida'
            AND NEW.status IS DISTINCT FROM OLD.status THEN
            RAISE EXCEPTION
                'Uma Aula concluida nao pode ter seu status alterado.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Cancelada'
            AND NEW.status IS DISTINCT FROM OLD.status THEN
            RAISE EXCEPTION
                'Uma Aula cancelada nao pode ter seu status alterado.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_aula
BEFORE INSERT OR UPDATE
ON aulas
FOR EACH ROW
EXECUTE FUNCTION proteger_aula();

CREATE TABLE presencas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aula_id uuid NOT NULL,
    aluno_id uuid NOT NULL,
    matricula_id uuid NOT NULL,
    status varchar(20) NOT NULL,
    chegou_as time without time zone NULL,
    saiu_as time without time zone NULL,
    observacoes text NULL,
    registrado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_presencas PRIMARY KEY (id),
    CONSTRAINT uq_presencas_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_presencas_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_aula
        FOREIGN KEY (organizacao_id, unidade_id, aula_id)
        REFERENCES aulas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_aluno
        FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_matricula
        FOREIGN KEY (organizacao_id, unidade_id, matricula_id)
        REFERENCES matriculas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_registrado_por_usuario_id
        FOREIGN KEY (registrado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_presencas_status_valido
        CHECK (status IN ('Presente', 'Ausente', 'Justificado', 'Isento'))
);

CREATE UNIQUE INDEX uq_presencas_aula_aluno
    ON presencas
       (organizacao_id, aula_id, aluno_id);

CREATE INDEX ix_presencas_organizacao_aula
    ON presencas (organizacao_id, aula_id);

CREATE INDEX ix_presencas_organizacao_aluno
    ON presencas (organizacao_id, aluno_id);

CREATE INDEX ix_presencas_registrado_por_usuario_id
    ON presencas (registrado_por_usuario_id);

CREATE FUNCTION proteger_presenca()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    status_aula varchar(20);
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.aula_id IS DISTINCT FROM OLD.aula_id
            OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
            OR NEW.matricula_id IS DISTINCT FROM OLD.matricula_id
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o vinculo e a auditoria de criacao da Presenca nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    IF TG_OP = 'INSERT' THEN
        SELECT status
        INTO status_aula
        FROM aulas
        WHERE organizacao_id = NEW.organizacao_id
          AND unidade_id = NEW.unidade_id
          AND id = NEW.aula_id;

        IF status_aula IS NULL THEN
            RAISE EXCEPTION
                'A Presenca exige uma Aula valida.'
                USING ERRCODE = '23514';
        END IF;

        IF status_aula = 'Cancelada' THEN
            RAISE EXCEPTION
                'Nao e possivel registrar Presenca em Aula cancelada.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_presenca
BEFORE INSERT OR UPDATE
ON presencas
FOR EACH ROW
EXECUTE FUNCTION proteger_presenca();

GRANT SELECT, INSERT, UPDATE
    ON TABLE aulas
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE presencas
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V015', 'criar aulas e presencas');

COMMIT;
