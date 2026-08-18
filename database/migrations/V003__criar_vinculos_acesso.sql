BEGIN;

ALTER TABLE unidades
    ADD CONSTRAINT uq_unidades_organizacao_id_id
    UNIQUE (organizacao_id, id);

CREATE TABLE vinculos_acesso (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NULL,
    perfil varchar(50) NOT NULL,
    ativo boolean NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_vinculos_acesso PRIMARY KEY (id),
    CONSTRAINT fk_vinculos_acesso_usuarios_usuario_id
        FOREIGN KEY (usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_vinculos_acesso_organizacoes_organizacao_id
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_vinculos_acesso_unidades_organizacao_id_unidade_id
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_vinculos_acesso_perfil_valido CHECK (
        perfil IN (
            'AdministradorRede',
            'AdministradorUnidade',
            'Professor',
            'Aluno',
            'Responsavel'
        )
    ),
    CONSTRAINT ck_vinculos_acesso_escopo_perfil CHECK (
        (perfil = 'AdministradorRede' AND unidade_id IS NULL)
        OR
        (perfil <> 'AdministradorRede' AND unidade_id IS NOT NULL)
    )
);

CREATE INDEX ix_vinculos_acesso_usuario_id_ativo
    ON vinculos_acesso (usuario_id, ativo);

CREATE INDEX ix_vinculos_acesso_organizacao_id_unidade_id
    ON vinculos_acesso (organizacao_id, unidade_id);

CREATE INDEX ix_vinculos_acesso_unidade_id
    ON vinculos_acesso (unidade_id);

CREATE UNIQUE INDEX uq_vinculos_acesso_usuario_organizacao_unidade_perfil
    ON vinculos_acesso (usuario_id, organizacao_id, unidade_id, perfil)
    NULLS NOT DISTINCT;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON TABLE vinculos_acesso
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V003', 'criar vinculos de acesso');

COMMIT;
