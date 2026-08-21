BEGIN;

CREATE TABLE estados (
    codigo_ibge integer NOT NULL,
    sigla varchar(2) NOT NULL,
    nome varchar(100) NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_estados PRIMARY KEY (codigo_ibge),
    CONSTRAINT uq_estados_sigla UNIQUE (sigla),
    CONSTRAINT ck_estados_codigo_ibge_positivo CHECK (codigo_ibge > 0),
    CONSTRAINT ck_estados_sigla_formato CHECK (sigla ~ '^[A-Z]{2}$'),
    CONSTRAINT ck_estados_nome_nao_vazio CHECK (btrim(nome) <> '')
);

CREATE TABLE municipios (
    codigo_ibge integer NOT NULL,
    estado_codigo_ibge integer NOT NULL,
    nome varchar(150) NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_municipios PRIMARY KEY (codigo_ibge),
    CONSTRAINT ck_municipios_codigo_ibge_positivo CHECK (codigo_ibge > 0),
    CONSTRAINT ck_municipios_nome_nao_vazio CHECK (btrim(nome) <> ''),
    CONSTRAINT fk_municipios_estados_estado_codigo_ibge
        FOREIGN KEY (estado_codigo_ibge)
        REFERENCES estados (codigo_ibge)
        ON DELETE RESTRICT
);

CREATE INDEX ix_municipios_estado_ativo_nome
    ON municipios (estado_codigo_ibge, ativo, nome);

GRANT SELECT, INSERT, UPDATE
    ON TABLE estados, municipios
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V006', 'criar catalogo de localidades');

COMMIT;
