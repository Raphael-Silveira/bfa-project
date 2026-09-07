BEGIN;

-- Tornar criado_por_usuario_id e atualizado_por_usuario_id nullable na tabela cobrancas
-- para suportar cobranças geradas automaticamente por jobs (sem usuário)

ALTER TABLE cobrancas
    DROP CONSTRAINT fk_cobrancas_criado_por_usuario_id;

ALTER TABLE cobrancas
    DROP CONSTRAINT fk_cobrancas_atualizado_por_usuario_id;

ALTER TABLE cobrancas
    ALTER COLUMN criado_por_usuario_id DROP NOT NULL;

ALTER TABLE cobrancas
    ALTER COLUMN atualizado_por_usuario_id DROP NOT NULL;

ALTER TABLE cobrancas
    ADD CONSTRAINT fk_cobrancas_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE cobrancas
    ADD CONSTRAINT fk_cobrancas_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON UPDATE CASCADE ON DELETE RESTRICT;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V020', 'tornar criado/atualizado_por_usuario_id nullable em cobrancas');

COMMIT;
