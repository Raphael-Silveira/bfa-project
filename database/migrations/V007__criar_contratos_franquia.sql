BEGIN;

CREATE TABLE contratos_franquia (
    id uuid NOT NULL,
    franqueado_unidade_id uuid NOT NULL,
    numero varchar(100) NULL,
    status varchar(30) NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_contratos_franquia PRIMARY KEY (id),
    CONSTRAINT fk_contratos_franquia_franqueado_unidade_id
        FOREIGN KEY (franqueado_unidade_id)
        REFERENCES franqueados_unidades (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_contratos_franquia_status_valido
        CHECK (status IN ('Rascunho', 'Ativo', 'Encerrado', 'Cancelado')),
    CONSTRAINT ck_contratos_franquia_numero_nao_vazio
        CHECK (numero IS NULL OR btrim(numero) <> '')
);

CREATE INDEX ix_contratos_franquia_franqueado_unidade_id
    ON contratos_franquia (franqueado_unidade_id);

CREATE UNIQUE INDEX uq_contratos_franquia_franqueado_unidade_ativo
    ON contratos_franquia (franqueado_unidade_id)
    WHERE status = 'Ativo';

CREATE FUNCTION proteger_contrato_franquia()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.franqueado_unidade_id IS DISTINCT FROM OLD.franqueado_unidade_id
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
        RAISE EXCEPTION
            'Identidade e vinculo do contrato de franquia nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.status <> 'Rascunho'
        AND NEW.numero IS DISTINCT FROM OLD.numero THEN
        RAISE EXCEPTION
            'O numero de um contrato formalizado nao pode ser alterado.'
            USING ERRCODE = '23514';
    END IF;

    IF NOT COALESCE(
        (OLD.status = 'Rascunho'
            AND NEW.status IN ('Rascunho', 'Ativo', 'Cancelado'))
        OR (OLD.status = 'Ativo'
            AND NEW.status IN ('Ativo', 'Encerrado', 'Cancelado'))
        OR (OLD.status = 'Encerrado' AND NEW.status = 'Encerrado')
        OR (OLD.status = 'Cancelado' AND NEW.status = 'Cancelado'),
        false) THEN
        RAISE EXCEPTION
            'Transicao de status do contrato de franquia nao permitida: % -> %.',
            OLD.status,
            NEW.status
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_contrato_franquia
BEFORE UPDATE
ON contratos_franquia
FOR EACH ROW
EXECUTE FUNCTION proteger_contrato_franquia();

CREATE TABLE contratos_franquia_versoes (
    id uuid NOT NULL,
    contrato_franquia_id uuid NOT NULL,
    numero_versao integer NOT NULL,
    data_inicio date NOT NULL,
    data_fim date NULL,
    percentual_royalties numeric(5,2) NOT NULL,
    mensalidade_fixa numeric(12,2) NOT NULL,
    taxa_adesao numeric(12,2) NULL,
    dia_vencimento smallint NULL,
    status varchar(30) NOT NULL,
    motivo_alteracao varchar(1000) NULL,
    observacoes varchar(4000) NULL,
    criado_em_utc timestamptz NOT NULL,
    criado_por_usuario_id uuid NOT NULL,
    CONSTRAINT pk_contratos_franquia_versoes PRIMARY KEY (id),
    CONSTRAINT fk_contratos_franquia_versoes_contrato_id
        FOREIGN KEY (contrato_franquia_id)
        REFERENCES contratos_franquia (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_contratos_franquia_versoes_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_contratos_franquia_versoes_numero_positivo
        CHECK (numero_versao >= 1),
    CONSTRAINT ck_contratos_franquia_versoes_vigencia_valida
        CHECK (data_fim IS NULL OR data_fim >= data_inicio),
    CONSTRAINT ck_contratos_franquia_versoes_royalties_valido
        CHECK (percentual_royalties >= 0 AND percentual_royalties <= 100),
    CONSTRAINT ck_contratos_franquia_versoes_mensalidade_valida
        CHECK (mensalidade_fixa >= 0),
    CONSTRAINT ck_contratos_franquia_versoes_taxa_adesao_valida
        CHECK (taxa_adesao IS NULL OR taxa_adesao >= 0),
    CONSTRAINT ck_contratos_franquia_versoes_dia_vencimento_valido
        CHECK (dia_vencimento IS NULL OR dia_vencimento BETWEEN 1 AND 31),
    CONSTRAINT ck_contratos_franquia_versoes_status_valido
        CHECK (status IN ('Rascunho', 'Vigente', 'Substituida', 'Cancelada')),
    CONSTRAINT ck_contratos_franquia_versoes_motivo_nao_vazio
        CHECK (motivo_alteracao IS NULL OR btrim(motivo_alteracao) <> ''),
    CONSTRAINT ck_contratos_franquia_versoes_observacoes_nao_vazias
        CHECK (observacoes IS NULL OR btrim(observacoes) <> '')
);

CREATE UNIQUE INDEX uq_contratos_franquia_versoes_contrato_numero
    ON contratos_franquia_versoes (contrato_franquia_id, numero_versao);

CREATE UNIQUE INDEX uq_contratos_franquia_versoes_vigente
    ON contratos_franquia_versoes (contrato_franquia_id)
    WHERE status = 'Vigente';

CREATE INDEX ix_contratos_franquia_versoes_criado_por_usuario_id
    ON contratos_franquia_versoes (criado_por_usuario_id);

CREATE FUNCTION proteger_versao_contrato_formalizada()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.id IS DISTINCT FROM OLD.id
        OR NEW.contrato_franquia_id IS DISTINCT FROM OLD.contrato_franquia_id
        OR NEW.numero_versao IS DISTINCT FROM OLD.numero_versao
        OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc
        OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id THEN
        RAISE EXCEPTION
            'Identidade e auditoria da versao contratual nao podem ser alteradas.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.status <> 'Rascunho'
        AND (
            NEW.data_inicio IS DISTINCT FROM OLD.data_inicio
            OR NEW.data_fim IS DISTINCT FROM OLD.data_fim
            OR NEW.percentual_royalties IS DISTINCT FROM OLD.percentual_royalties
            OR NEW.mensalidade_fixa IS DISTINCT FROM OLD.mensalidade_fixa
            OR NEW.taxa_adesao IS DISTINCT FROM OLD.taxa_adesao
            OR NEW.dia_vencimento IS DISTINCT FROM OLD.dia_vencimento
            OR NEW.motivo_alteracao IS DISTINCT FROM OLD.motivo_alteracao
            OR NEW.observacoes IS DISTINCT FROM OLD.observacoes
        ) THEN
        RAISE EXCEPTION
            'Termos de uma versao contratual formalizada nao podem ser alterados.'
            USING ERRCODE = '23514';
    END IF;

    IF NOT COALESCE(
        (OLD.status = 'Rascunho'
            AND NEW.status IN ('Rascunho', 'Vigente', 'Cancelada'))
        OR (OLD.status = 'Vigente'
            AND NEW.status IN ('Vigente', 'Substituida', 'Cancelada'))
        OR (OLD.status = 'Substituida' AND NEW.status = 'Substituida')
        OR (OLD.status = 'Cancelada' AND NEW.status = 'Cancelada'),
        false) THEN
        RAISE EXCEPTION
            'Transicao de status da versao contratual nao permitida: % -> %.',
            OLD.status,
            NEW.status
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_versao_contrato_formalizada
BEFORE UPDATE
ON contratos_franquia_versoes
FOR EACH ROW
EXECUTE FUNCTION proteger_versao_contrato_formalizada();

CREATE TABLE documentos_contrato_franquia (
    id uuid NOT NULL,
    contrato_franquia_versao_id uuid NOT NULL,
    tipo_documento varchar(30) NOT NULL,
    nome_original varchar(255) NOT NULL,
    chave_armazenamento varchar(500) NOT NULL,
    content_type varchar(100) NOT NULL,
    tamanho_bytes bigint NOT NULL,
    hash_sha256 varchar(64) NULL,
    criado_em_utc timestamptz NOT NULL,
    enviado_por_usuario_id uuid NOT NULL,
    CONSTRAINT pk_documentos_contrato_franquia PRIMARY KEY (id),
    CONSTRAINT fk_documentos_contrato_franquia_versao_id
        FOREIGN KEY (contrato_franquia_versao_id)
        REFERENCES contratos_franquia_versoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_documentos_contrato_franquia_enviado_por_usuario_id
        FOREIGN KEY (enviado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_documentos_contrato_franquia_tipo_valido
        CHECK (tipo_documento IN ('Contrato', 'Aditivo', 'Anexo', 'Outro')),
    CONSTRAINT ck_documentos_contrato_franquia_nome_nao_vazio
        CHECK (btrim(nome_original) <> ''),
    CONSTRAINT ck_documentos_contrato_franquia_chave_nao_vazia
        CHECK (btrim(chave_armazenamento) <> ''),
    CONSTRAINT ck_documentos_contrato_franquia_content_type_nao_vazio
        CHECK (btrim(content_type) <> ''),
    CONSTRAINT ck_documentos_contrato_franquia_tamanho_positivo
        CHECK (tamanho_bytes > 0),
    CONSTRAINT ck_documentos_contrato_franquia_hash_valido
        CHECK (hash_sha256 IS NULL OR hash_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE INDEX ix_documentos_contrato_franquia_versao_id
    ON documentos_contrato_franquia (contrato_franquia_versao_id);

CREATE UNIQUE INDEX uq_documentos_contrato_franquia_chave_armazenamento
    ON documentos_contrato_franquia (chave_armazenamento);

CREATE INDEX ix_documentos_contrato_franquia_enviado_por_usuario_id
    ON documentos_contrato_franquia (enviado_por_usuario_id);

GRANT SELECT, INSERT, UPDATE
    ON TABLE contratos_franquia
    TO bfa_app_role;

GRANT SELECT, INSERT, UPDATE
    ON TABLE contratos_franquia_versoes
    TO bfa_app_role;

GRANT SELECT, INSERT
    ON TABLE documentos_contrato_franquia
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V007', 'criar contratos de franquia versionados');

COMMIT;
