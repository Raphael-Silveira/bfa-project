BEGIN;

GRANT CREATE ON SCHEMA hangfire TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V018', 'conceder CREATE no schema hangfire ao bfa_app_role');

COMMIT;
