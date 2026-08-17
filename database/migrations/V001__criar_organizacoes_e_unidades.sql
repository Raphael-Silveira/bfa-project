BEGIN;

CREATE TABLE bfa_schema_history (
    version varchar(20) PRIMARY KEY,
    descricao varchar(200) NOT NULL,
    aplicado_em_utc timestamptz NOT NULL DEFAULT now(),
    executado_por varchar(100) NOT NULL DEFAULT current_user
);

REVOKE ALL ON TABLE bfa_schema_history FROM bfa_app_role;

CREATE TABLE organizacoes (
    id uuid NOT NULL,
    nome varchar(150) NOT NULL,
    slug varchar(100) NOT NULL,
    ativa boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_organizacoes PRIMARY KEY (id),
    CONSTRAINT uq_organizacoes_slug UNIQUE (slug),
    CONSTRAINT ck_organizacoes_nome_nao_vazio CHECK (btrim(nome) <> ''),
    CONSTRAINT ck_organizacoes_slug_nao_vazio CHECK (btrim(slug) <> ''),
    CONSTRAINT ck_organizacoes_slug_normalizado CHECK (slug = lower(btrim(slug)))
);

CREATE TABLE unidades (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    nome varchar(150) NOT NULL,
    slug varchar(100) NOT NULL,
    ativa boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_unidades PRIMARY KEY (id),
    CONSTRAINT uq_unidades_organizacao_id_slug UNIQUE (organizacao_id, slug),
    CONSTRAINT ck_unidades_nome_nao_vazio CHECK (btrim(nome) <> ''),
    CONSTRAINT ck_unidades_slug_nao_vazio CHECK (btrim(slug) <> ''),
    CONSTRAINT ck_unidades_slug_normalizado CHECK (slug = lower(btrim(slug))),
    CONSTRAINT fk_unidades_organizacoes_organizacao_id
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT
);

CREATE INDEX ix_unidades_organizacao_id
    ON unidades (organizacao_id);

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V001', 'criar organizacoes e unidades');

COMMIT;
