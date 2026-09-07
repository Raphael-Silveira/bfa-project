BEGIN;

CREATE SCHEMA IF NOT EXISTS hangfire;

GRANT USAGE ON SCHEMA hangfire TO bfa_app_role;
GRANT ALL ON ALL TABLES IN SCHEMA hangfire TO bfa_app_role;
GRANT ALL ON ALL SEQUENCES IN SCHEMA hangfire TO bfa_app_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire
    GRANT ALL ON TABLES TO bfa_app_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire
    GRANT ALL ON SEQUENCES TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V017', 'criar schema hangfire');

COMMIT;
