BEGIN;

ALTER TABLE alunos
    ADD COLUMN apelido varchar(80) NULL,
    ADD COLUMN cep varchar(8) NULL,
    ADD COLUMN estado_codigo_ibge integer NULL,
    ADD COLUMN municipio_codigo_ibge integer NULL,
    ADD COLUMN bairro varchar(120) NULL,
    ADD COLUMN logradouro varchar(180) NULL,
    ADD COLUMN numero varchar(20) NULL,
    ADD COLUMN complemento varchar(120) NULL,
    ADD COLUMN foto_perfil_chave varchar(300) NULL,
    ADD COLUMN foto_perfil_content_type varchar(50) NULL,
    ADD COLUMN foto_perfil_atualizada_em_utc timestamptz NULL;

ALTER TABLE alunos
    ADD CONSTRAINT ck_alunos_cep_formato
        CHECK (cep IS NULL OR cep ~ '^[0-9]{8}$'),
    ADD CONSTRAINT ck_alunos_municipio_estado_consistente
        CHECK (municipio_codigo_ibge IS NULL OR estado_codigo_ibge IS NOT NULL),
    ADD CONSTRAINT fk_alunos_estado
        FOREIGN KEY (estado_codigo_ibge)
        REFERENCES estados (codigo_ibge)
        ON DELETE RESTRICT,
    ADD CONSTRAINT fk_alunos_municipio
        FOREIGN KEY (municipio_codigo_ibge)
        REFERENCES municipios (codigo_ibge)
        ON DELETE RESTRICT;

GRANT SELECT, INSERT, UPDATE
    ON TABLE alunos
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V023', 'ampliar perfil do aluno');

COMMIT;
