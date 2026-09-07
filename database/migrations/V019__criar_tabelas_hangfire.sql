-- Consolidated Hangfire PostgreSQL schema (versions 3-19 from Hangfire.PostgreSql 1.20.9)
-- Created as a single migration because V017 only created the schema, not the tables.

SET search_path = 'hangfire';

-- schema version tracking
CREATE TABLE IF NOT EXISTS "schema"
(
    "version" INT NOT NULL,
    PRIMARY KEY ("version")
);
INSERT INTO "schema"("version") VALUES ('19');

-- aggregatedcounter (v18)
CREATE TABLE IF NOT EXISTS "aggregatedcounter"
(
    "id"       BIGSERIAL  NOT NULL,
    "key"      TEXT       NOT NULL UNIQUE,
    "value"    BIGINT     NOT NULL,
    "expireat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id")
);

-- counter (v3, v8: bigint value, v12: text key)
CREATE TABLE IF NOT EXISTS "counter"
(
    "id"       BIGSERIAL NOT NULL,
    "key"      TEXT      NOT NULL,
    "value"    BIGINT    NOT NULL,
    "expireat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_counter_key" ON "counter" ("key");
CREATE INDEX IF NOT EXISTS "ix_hangfire_counter_expireat" ON "counter" ("expireat");

-- hash (v3, v12: text columns)
CREATE TABLE IF NOT EXISTS "hash"
(
    "id"       BIGSERIAL NOT NULL,
    "key"      TEXT      NOT NULL,
    "field"    TEXT      NOT NULL,
    "value"    TEXT,
    "expireat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id"),
    UNIQUE ("key", "field")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_hash_expireat" ON "hash" ("expireat");

-- job (v3, v11: bigint, v12: text columns, v19: timestamptz)
CREATE TABLE IF NOT EXISTS "job"
(
    "id"             BIGSERIAL  NOT NULL,
    "stateid"        BIGINT,
    "statename"      TEXT,
    "invocationdata" TEXT       NOT NULL,
    "arguments"      TEXT       NOT NULL,
    "createdat"      TIMESTAMP WITH TIME ZONE NOT NULL,
    "expireat"       TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_job_statename" ON "job" ("statename");
CREATE INDEX IF NOT EXISTS "ix_hangfire_job_expireat" ON "job" ("expireat");

-- state (v3, v11: bigint, v12: text columns)
CREATE TABLE IF NOT EXISTS "state"
(
    "id"        BIGSERIAL  NOT NULL,
    "jobid"     BIGINT     NOT NULL,
    "name"      TEXT       NOT NULL,
    "reason"    TEXT,
    "createdat" TIMESTAMP WITH TIME ZONE NOT NULL,
    "data"      TEXT,
    PRIMARY KEY ("id"),
    FOREIGN KEY ("jobid") REFERENCES "job" ("id") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_state_jobid" ON "state" ("jobid");

-- jobqueue (v3, v10: text queue, v11: bigint, v19: timestamptz)
CREATE TABLE IF NOT EXISTS "jobqueue"
(
    "id"        BIGSERIAL  NOT NULL,
    "jobid"     BIGINT     NOT NULL,
    "queue"     TEXT       NOT NULL,
    "fetchedat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_jobqueue_queueandfetchedat" ON "jobqueue" ("queue", "fetchedat");
CREATE INDEX IF NOT EXISTS "ix_hangfire_jobqueue_jobidandqueue" ON "jobqueue" ("jobid", "queue");
CREATE INDEX IF NOT EXISTS "jobqueue_queue_fetchat_jobId" ON jobqueue USING btree (queue ASC, fetchedat ASC NULLS LAST, jobid ASC);

-- list (v3, v11: bigint, v12: text key)
CREATE TABLE IF NOT EXISTS "list"
(
    "id"       BIGSERIAL  NOT NULL,
    "key"      TEXT       NOT NULL,
    "value"    TEXT,
    "expireat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_list_expireat" ON "list" ("expireat");

-- server (v3, v5: varchar(100), v11: bigint→v12: text)
CREATE TABLE IF NOT EXISTS "server"
(
    "id"            TEXT        NOT NULL,
    "data"          TEXT,
    "lastheartbeat" TIMESTAMP WITH TIME ZONE NOT NULL,
    PRIMARY KEY ("id")
);

-- set (v3, v11: bigint, v12: text key)
CREATE TABLE IF NOT EXISTS "set"
(
    "id"       BIGSERIAL  NOT NULL,
    "key"      TEXT       NOT NULL,
    "score"    FLOAT8     NOT NULL,
    "value"    TEXT       NOT NULL,
    "expireat" TIMESTAMP WITH TIME ZONE,
    PRIMARY KEY ("id"),
    UNIQUE ("key", "value")
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_set_expireat" ON "set" ("expireat");
CREATE INDEX IF NOT EXISTS "ix_hangfire_set_key_score" ON "set" (key, score);

-- jobparameter (v3, v11: bigint, v12: text name)
CREATE TABLE IF NOT EXISTS "jobparameter"
(
    "id"    BIGSERIAL  NOT NULL,
    "jobid" BIGINT     NOT NULL,
    "name"  TEXT       NOT NULL,
    "value" TEXT,
    PRIMARY KEY ("id"),
    FOREIGN KEY ("jobid") REFERENCES "job" ("id") ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_hangfire_jobparameter_jobidandname" ON "jobparameter" ("jobid", "name");

-- lock (v3, v7: acquired column, v9: text resource)
CREATE TABLE IF NOT EXISTS "lock"
(
    "resource" TEXT       NOT NULL,
    "acquired" TIMESTAMP WITH TIME ZONE,
    UNIQUE ("resource")
);

RESET search_path;

-- migrations history
INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V019', 'criar tabelas hangfire (schema consolidado v3-v19)');
