BEGIN;

GRANT DELETE ON TABLE day_uses TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V026', 'permitir exclusao de day use');

COMMIT;
