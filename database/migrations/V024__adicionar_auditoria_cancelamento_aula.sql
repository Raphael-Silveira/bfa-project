BEGIN;

ALTER TABLE aulas
    ADD COLUMN motivo_cancelamento varchar(500) NULL,
    ADD COLUMN cancelada_em_utc timestamptz NULL,
    ADD COLUMN cancelada_por_usuario_id uuid NULL;

ALTER TABLE aulas
    ADD CONSTRAINT ck_aulas_motivo_cancelamento_valido
        CHECK (motivo_cancelamento IS NULL OR btrim(motivo_cancelamento) <> ''),
    ADD CONSTRAINT fk_aulas_cancelada_por_usuario_id
        FOREIGN KEY (cancelada_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT;

CREATE INDEX ix_aulas_cancelada_por_usuario_id
    ON aulas (cancelada_por_usuario_id);

GRANT SELECT, INSERT, UPDATE
    ON TABLE aulas
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V024', 'adicionar auditoria de cancelamento de aula');

COMMIT;
