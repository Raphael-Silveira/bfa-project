BEGIN;

CREATE TABLE cobrancas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aluno_id uuid NOT NULL,
    matricula_id uuid NOT NULL,
    tipo varchar(20) NOT NULL,
    descricao varchar(200) NOT NULL,
    valor numeric(12,2) NOT NULL,
    valor_pago numeric(12,2) NOT NULL DEFAULT 0,
    data_emissao date NOT NULL,
    data_vencimento date NOT NULL,
    data_pagamento date NULL,
    status varchar(20) NOT NULL DEFAULT 'Pendente',
    observacoes text NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_cobrancas PRIMARY KEY (id),
    CONSTRAINT uq_cobrancas_organizacao_unidade_id
        UNIQUE (organizacao_id, unidade_id, id),
    CONSTRAINT fk_cobrancas_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_aluno
        FOREIGN KEY (organizacao_id, aluno_id)
        REFERENCES alunos (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_matricula
        FOREIGN KEY (organizacao_id, unidade_id, matricula_id)
        REFERENCES matriculas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_criado_por_usuario_id
        FOREIGN KEY (criado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_atualizado_por_usuario_id
        FOREIGN KEY (atualizado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_cobrancas_tipo_valido
        CHECK (tipo IN ('Matricula', 'Mensalidade', 'Avulso')),
    CONSTRAINT ck_cobrancas_status_valido
        CHECK (status IN ('Pendente', 'Paga', 'Atrasada', 'Cancelada')),
    CONSTRAINT ck_cobrancas_valor_positivo
        CHECK (valor > 0),
    CONSTRAINT ck_cobrancas_valor_pago_nao_negativo
        CHECK (valor_pago >= 0)
);

CREATE INDEX ix_cobrancas_organizacao_aluno
    ON cobrancas (organizacao_id, aluno_id);

CREATE INDEX ix_cobrancas_organizacao_matricula
    ON cobrancas (organizacao_id, matricula_id);

CREATE INDEX ix_cobrancas_organizacao_vencimento_status
    ON cobrancas (organizacao_id, data_vencimento, status);

CREATE INDEX ix_cobrancas_criado_por_usuario_id
    ON cobrancas (criado_por_usuario_id);

CREATE INDEX ix_cobrancas_atualizado_por_usuario_id
    ON cobrancas (atualizado_por_usuario_id);

CREATE FUNCTION proteger_cobranca()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
            OR NEW.matricula_id IS DISTINCT FROM OLD.matricula_id
            OR NEW.tipo IS DISTINCT FROM OLD.tipo
            OR NEW.valor IS DISTINCT FROM OLD.valor
            OR NEW.data_emissao IS DISTINCT FROM OLD.data_emissao
            OR NEW.data_vencimento IS DISTINCT FROM OLD.data_vencimento
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o tipo, o valor, as datas de emissao/vencimento e a auditoria de criacao da Cobranca nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Paga'
            AND NEW.status IS DISTINCT FROM OLD.status THEN
            RAISE EXCEPTION
                'Uma Cobranca paga nao pode ter seu status alterado.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Cancelada'
            AND NEW.status IS DISTINCT FROM OLD.status THEN
            RAISE EXCEPTION
                'Uma Cobranca cancelada nao pode ter seu status alterado.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_cobranca
BEFORE INSERT OR UPDATE
ON cobrancas
FOR EACH ROW
EXECUTE FUNCTION proteger_cobranca();

CREATE TABLE pagamentos (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    cobranca_id uuid NOT NULL,
    valor numeric(12,2) NOT NULL,
    data_pagamento date NOT NULL,
    data_registro timestamptz NOT NULL,
    forma_pagamento varchar(20) NOT NULL,
    observacoes text NULL,
    registrado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_pagamentos PRIMARY KEY (id),
    CONSTRAINT uq_pagamentos_organizacao_id
        UNIQUE (organizacao_id, id),
    CONSTRAINT fk_pagamentos_organizacao
        FOREIGN KEY (organizacao_id)
        REFERENCES organizacoes (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_unidade
        FOREIGN KEY (organizacao_id, unidade_id)
        REFERENCES unidades (organizacao_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_cobranca
        FOREIGN KEY (organizacao_id, unidade_id, cobranca_id)
        REFERENCES cobrancas (organizacao_id, unidade_id, id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_registrado_por_usuario_id
        FOREIGN KEY (registrado_por_usuario_id)
        REFERENCES usuarios (id)
        ON DELETE RESTRICT,
    CONSTRAINT ck_pagamentos_forma_valida
        CHECK (forma_pagamento IN ('Dinheiro','Pix','CartaoCredito','CartaoDebito','Boleto','Transferencia','Outros')),
    CONSTRAINT ck_pagamentos_valor_positivo
        CHECK (valor > 0)
);

CREATE INDEX ix_pagamentos_organizacao_cobranca
    ON pagamentos (organizacao_id, cobranca_id);

CREATE INDEX ix_pagamentos_registrado_por_usuario_id
    ON pagamentos (registrado_por_usuario_id);

CREATE FUNCTION proteger_pagamento()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.cobranca_id IS DISTINCT FROM OLD.cobranca_id
            OR NEW.valor IS DISTINCT FROM OLD.valor
            OR NEW.data_pagamento IS DISTINCT FROM OLD.data_pagamento
            OR NEW.forma_pagamento IS DISTINCT FROM OLD.forma_pagamento
            OR NEW.registrado_por_usuario_id IS DISTINCT FROM OLD.registrado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'Os dados do Pagamento sao imutaveis apos o registro.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_proteger_pagamento
BEFORE INSERT OR UPDATE
ON pagamentos
FOR EACH ROW
EXECUTE FUNCTION proteger_pagamento();

CREATE FUNCTION atualizar_cobranca_apos_pagamento()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    total_pago numeric(12,2);
    valor_cobranca numeric(12,2);
    data_venc_cobranca date;
    nova_data_pagamento date;
BEGIN
    SELECT valor, data_vencimento
    INTO valor_cobranca, data_venc_cobranca
    FROM cobrancas
    WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
      AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);

    SELECT COALESCE(SUM(valor), 0)
    INTO total_pago
    FROM pagamentos
    WHERE cobranca_id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
      AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);

    IF total_pago >= valor_cobranca THEN
        SELECT MAX(data_pagamento)
        INTO nova_data_pagamento
        FROM pagamentos
        WHERE cobranca_id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
          AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);

        UPDATE cobrancas
        SET valor_pago = total_pago,
            status = 'Paga',
            data_pagamento = nova_data_pagamento,
            atualizado_em_utc = NOW()
        WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
          AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
    ELSIF data_venc_cobranca < CURRENT_DATE THEN
        UPDATE cobrancas
        SET valor_pago = total_pago,
            status = 'Atrasada',
            data_pagamento = NULL,
            atualizado_em_utc = NOW()
        WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
          AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
    ELSE
        UPDATE cobrancas
        SET valor_pago = total_pago,
            status = 'Pendente',
            data_pagamento = NULL,
            atualizado_em_utc = NOW()
        WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id)
          AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
    END IF;

    RETURN COALESCE(NEW, OLD);
END;
$$;

CREATE TRIGGER trg_atualizar_cobranca_apos_pagamento
AFTER INSERT OR DELETE
ON pagamentos
FOR EACH ROW
EXECUTE FUNCTION atualizar_cobranca_apos_pagamento();

GRANT SELECT, INSERT, UPDATE
    ON TABLE cobrancas
    TO bfa_app_role;

GRANT SELECT, INSERT
    ON TABLE pagamentos
    TO bfa_app_role;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V016', 'criar cobrancas e pagamentos');

COMMIT;
