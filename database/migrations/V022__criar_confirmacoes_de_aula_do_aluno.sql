BEGIN;

CREATE TABLE confirmacoes_aula_aluno (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aula_id uuid NOT NULL,
    aluno_id uuid NOT NULL,
    ativa boolean NOT NULL,
    confirmada_em_utc timestamptz NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_confirmacoes_aula_aluno PRIMARY KEY (id),
    CONSTRAINT uq_confirmacoes_aula_aluno_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_confirmacoes_aula_aluno_organizacao
        FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_confirmacoes_aula_aluno_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_confirmacoes_aula_aluno_aula
        FOREIGN KEY (organizacao_id, unidade_id, aula_id)
        REFERENCES aulas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_confirmacoes_aula_aluno_aluno
        FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_confirmacoes_aula_aluno_confirmacao_coerente
        CHECK (ativa = false OR confirmada_em_utc IS NOT NULL)
);

CREATE UNIQUE INDEX uq_confirmacoes_aula_aluno_identidade
    ON confirmacoes_aula_aluno (organizacao_id, unidade_id, aula_id, aluno_id);

CREATE INDEX ix_confirmacoes_aula_aluno_aluno
    ON confirmacoes_aula_aluno (organizacao_id, unidade_id, aluno_id);

GRANT SELECT, INSERT, UPDATE
    ON TABLE confirmacoes_aula_aluno
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V022', 'criar confirmacoes de aula do aluno');

COMMIT;
