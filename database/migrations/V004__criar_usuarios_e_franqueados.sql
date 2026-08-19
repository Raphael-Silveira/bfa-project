BEGIN;

CREATE TABLE perfis_usuario (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    nome_completo varchar(150) NOT NULL,
    telefone varchar(30) NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_perfis_usuario PRIMARY KEY (id),
    CONSTRAINT fk_perfis_usuario_usuarios_usuario_id
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_perfis_usuario_nome_completo_nao_vazio
        CHECK (btrim(nome_completo) <> '')
);

CREATE UNIQUE INDEX uq_perfis_usuario_usuario_id
    ON perfis_usuario (usuario_id);

CREATE TABLE franqueados (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    tipo_pessoa varchar(30) NOT NULL,
    nome_razao_social varchar(200) NOT NULL,
    nome_fantasia varchar(200) NULL,
    documento varchar(14) NOT NULL,
    telefone varchar(30) NULL,
    email varchar(256) NOT NULL,
    email_financeiro varchar(256) NULL,
    responsavel_legal varchar(150) NULL,
    logradouro varchar(200) NULL,
    numero varchar(30) NULL,
    complemento varchar(100) NULL,
    bairro varchar(100) NULL,
    cidade varchar(100) NULL,
    estado varchar(2) NULL,
    cep varchar(8) NULL,
    observacoes varchar(2000) NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_franqueados PRIMARY KEY (id),
    CONSTRAINT uq_franqueados_organizacao_id_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_franqueados_organizacoes_organizacao_id
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_franqueados_tipo_pessoa_valido
        CHECK (tipo_pessoa IN ('PessoaFisica', 'PessoaJuridica')),
    CONSTRAINT ck_franqueados_nome_razao_social_nao_vazio
        CHECK (btrim(nome_razao_social) <> ''),
    CONSTRAINT ck_franqueados_documento_tipo_pessoa CHECK (
        (tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$')
        OR
        (tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[0-9]{14}$')
    ),
    CONSTRAINT ck_franqueados_email_nao_vazio
        CHECK (btrim(email) <> '')
);

CREATE UNIQUE INDEX uq_franqueados_organizacao_id_documento
    ON franqueados (organizacao_id, documento);

CREATE TABLE franqueados_usuarios (
    id uuid NOT NULL,
    franqueado_id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    principal boolean NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_franqueados_usuarios PRIMARY KEY (id),
    CONSTRAINT fk_franqueados_usuarios_franqueado_id
        FOREIGN KEY (franqueado_id)
        REFERENCES franqueados (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_franqueados_usuarios_usuario_id
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT
);

CREATE UNIQUE INDEX uq_franqueados_usuarios_franqueado_id_usuario_id
    ON franqueados_usuarios (franqueado_id, usuario_id);

CREATE UNIQUE INDEX uq_franqueados_usuarios_principal_ativo
    ON franqueados_usuarios (franqueado_id)
    WHERE principal = true AND ativo = true;

CREATE INDEX ix_franqueados_usuarios_usuario_id
    ON franqueados_usuarios (usuario_id);

CREATE TABLE franqueados_unidades (
    id uuid NOT NULL,
    franqueado_id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_franqueados_unidades PRIMARY KEY (id),
    CONSTRAINT fk_franqueados_unidades_franqueado
        FOREIGN KEY (organizacao_id, franqueado_id)
        REFERENCES franqueados (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_franqueados_unidades_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_franqueados_unidades_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT
);

CREATE INDEX ix_franqueados_unidades_franqueado_id
    ON franqueados_unidades (franqueado_id);

CREATE UNIQUE INDEX uq_franqueados_unidades_franqueado_unidade
    ON franqueados_unidades (organizacao_id, franqueado_id, unidade_id);

CREATE INDEX ix_franqueados_unidades_organizacao_unidade_ativo
    ON franqueados_unidades (organizacao_id, unidade_id, ativo);

CREATE UNIQUE INDEX uq_franqueados_unidades_unidade_ativa
    ON franqueados_unidades (organizacao_id, unidade_id)
    WHERE ativo = true;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON TABLE perfis_usuario, franqueados, franqueados_usuarios, franqueados_unidades
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V004', 'criar usuarios e franqueados');

COMMIT;
