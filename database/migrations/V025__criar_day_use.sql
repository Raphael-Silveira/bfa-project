BEGIN;

ALTER TABLE unidades
    ADD COLUMN valor_day_use_sugerido numeric(12,2) NULL;

ALTER TABLE unidades
    ADD CONSTRAINT ck_unidades_valor_day_use_nao_negativo
        CHECK (valor_day_use_sugerido IS NULL OR valor_day_use_sugerido >= 0);

CREATE TABLE day_uses (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aluno_id uuid NULL,
    nome_avulso varchar(150) NULL,
    telefone_avulso varchar(30) NULL,
    email_avulso varchar(256) NULL,
    data_uso date NOT NULL,
    valor_sugerido numeric(12,2) NOT NULL,
    valor_cobrado numeric(12,2) NOT NULL,
    pago boolean NOT NULL DEFAULT false,
    criado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_day_uses PRIMARY KEY (id),
    CONSTRAINT uq_day_uses_organizacao_id_id UNIQUE (organizacao_id, id),
    CONSTRAINT fk_day_uses_organizacao FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_day_uses_unidade FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_day_uses_aluno FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_day_uses_criado_por_usuario FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_day_uses_participante_valido CHECK (
        (aluno_id IS NOT NULL AND nome_avulso IS NULL AND telefone_avulso IS NULL AND email_avulso IS NULL)
        OR (aluno_id IS NULL AND nome_avulso IS NOT NULL AND btrim(nome_avulso) <> '')
    ),
    CONSTRAINT ck_day_uses_valor_sugerido_nao_negativo CHECK (valor_sugerido >= 0),
    CONSTRAINT ck_day_uses_valor_cobrado_nao_negativo CHECK (valor_cobrado >= 0)
);

CREATE UNIQUE INDEX uq_day_uses_aluno_data
    ON day_uses (organizacao_id, unidade_id, aluno_id, data_uso)
    WHERE aluno_id IS NOT NULL;

CREATE INDEX ix_day_uses_unidade_data
    ON day_uses (organizacao_id, unidade_id, data_uso);

GRANT SELECT, INSERT, UPDATE
    ON TABLE unidades, day_uses
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V025', 'criar day use');

COMMIT;
