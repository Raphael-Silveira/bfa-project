-- =============================================================
-- SEED: Dados de teste para Franqueadora (Dashboard + Alunos)
-- =============================================================
-- Organizacao: BFA (aa390a31-292b-477c-9ba6-4e549bad19b8)
-- Unidades: BFA-Cerquilho (90 alunas, 2 quadras)
--           BFA-Tiete (50 alunas, 1 quadra)
-- =============================================================
-- IMPORTANTE: Execute com o user bfa_dev_deploy (DDL permissions)
-- =============================================================

BEGIN;

-- =============================================================
-- CRIAR TABELAS V015/V016 SE NAO EXISTIREM
-- =============================================================

CREATE TABLE IF NOT EXISTS aulas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    turma_id uuid NOT NULL,
    turma_horario_id uuid NOT NULL,
    data date NOT NULL,
    hora_inicio time without time zone NOT NULL,
    hora_fim time without time zone NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Programada',
    capacidade integer NOT NULL,
    observacoes text NULL,
    criado_por_usuario_id uuid NOT NULL,
    atualizado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_aulas PRIMARY KEY (id),
    CONSTRAINT fk_aulas_organizacao FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_unidade FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_turma FOREIGN KEY (organizacao_id, unidade_id, turma_id) REFERENCES turmas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_turma_horario FOREIGN KEY (organizacao_id, unidade_id, turma_horario_id) REFERENCES turmas_horarios (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_criado_por_usuario_id FOREIGN KEY (criado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_aulas_atualizado_por_usuario_id FOREIGN KEY (atualizado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_aulas_status_valido CHECK (status IN ('Programada', 'Concluida', 'Cancelada')),
    CONSTRAINT ck_aulas_intervalo_valido CHECK (hora_inicio < hora_fim),
    CONSTRAINT ck_aulas_capacidade_valida CHECK (capacidade > 0),
    CONSTRAINT uq_aulas_organizacao_unidade_id UNIQUE (organizacao_id, unidade_id, id)
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_aulas_organizacao_turma_data_hora ON aulas (organizacao_id, turma_id, data, hora_inicio);
CREATE INDEX IF NOT EXISTS ix_aulas_organizacao_unidade_data ON aulas (organizacao_id, unidade_id, data);
CREATE INDEX IF NOT EXISTS ix_aulas_organizacao_turma_data ON aulas (organizacao_id, turma_id, data);
CREATE INDEX IF NOT EXISTS ix_aulas_organizacao_turma_horario ON aulas (organizacao_id, turma_horario_id);
CREATE INDEX IF NOT EXISTS ix_aulas_criado_por_usuario_id ON aulas (criado_por_usuario_id);
CREATE INDEX IF NOT EXISTS ix_aulas_atualizado_por_usuario_id ON aulas (atualizado_por_usuario_id);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'proteger_aula') THEN
        CREATE FUNCTION proteger_aula() RETURNS trigger LANGUAGE plpgsql AS $func$
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id OR NEW.turma_id IS DISTINCT FROM OLD.turma_id OR NEW.turma_horario_id IS DISTINCT FROM OLD.turma_horario_id OR NEW.data IS DISTINCT FROM OLD.data OR NEW.hora_inicio IS DISTINCT FROM OLD.hora_inicio OR NEW.hora_fim IS DISTINCT FROM OLD.hora_fim OR NEW.capacidade IS DISTINCT FROM OLD.capacidade OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN RAISE EXCEPTION 'A identidade, o slot, a capacidade e a auditoria de criacao da Aula nao podem ser alterados.' USING ERRCODE = '23514'; END IF;
                IF OLD.status = 'Concluida' AND NEW.status IS DISTINCT FROM OLD.status THEN RAISE EXCEPTION 'Uma Aula concluida nao pode ter seu status alterado.' USING ERRCODE = '23514'; END IF;
                IF OLD.status = 'Cancelada' AND NEW.status IS DISTINCT FROM OLD.status THEN RAISE EXCEPTION 'Uma Aula cancelada nao pode ter seu status alterado.' USING ERRCODE = '23514'; END IF;
            END IF;
            RETURN NEW;
        END;
        $func$;
        CREATE TRIGGER trg_proteger_aula BEFORE INSERT OR UPDATE ON aulas FOR EACH ROW EXECUTE FUNCTION proteger_aula();
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS presencas (
    id uuid NOT NULL,
    organizacao_id uuid NOT NULL,
    unidade_id uuid NOT NULL,
    aula_id uuid NOT NULL,
    aluno_id uuid NOT NULL,
    matricula_id uuid NOT NULL,
    status varchar(20) NOT NULL,
    chegou_as time without time zone NULL,
    saiu_as time without time zone NULL,
    observacoes text NULL,
    registrado_por_usuario_id uuid NOT NULL,
    criado_em_utc timestamptz NOT NULL,
    atualizado_em_utc timestamptz NOT NULL,
    CONSTRAINT pk_presencas PRIMARY KEY (id),
    CONSTRAINT fk_presencas_organizacao FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_unidade FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_aula FOREIGN KEY (organizacao_id, unidade_id, aula_id) REFERENCES aulas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_aluno FOREIGN KEY (organizacao_id, aluno_id) REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_matricula FOREIGN KEY (organizacao_id, unidade_id, matricula_id) REFERENCES matriculas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_presencas_registrado_por_usuario_id FOREIGN KEY (registrado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_presencas_status_valido CHECK (status IN ('Presente', 'Ausente', 'Justificado', 'Isento'))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_presencas_aula_aluno ON presencas (organizacao_id, aula_id, aluno_id);
CREATE INDEX IF NOT EXISTS ix_presencas_organizacao_aula ON presencas (organizacao_id, aula_id);
CREATE INDEX IF NOT EXISTS ix_presencas_organizacao_aluno ON presencas (organizacao_id, aluno_id);
CREATE INDEX IF NOT EXISTS ix_presencas_registrado_por_usuario_id ON presencas (registrado_por_usuario_id);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'proteger_presenca') THEN
        CREATE FUNCTION proteger_presenca() RETURNS trigger LANGUAGE plpgsql AS $func$
        DECLARE status_aula varchar(20);
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id OR NEW.aula_id IS DISTINCT FROM OLD.aula_id OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id OR NEW.matricula_id IS DISTINCT FROM OLD.matricula_id OR NEW.registrado_por_usuario_id IS DISTINCT FROM OLD.registrado_por_usuario_id OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN RAISE EXCEPTION 'A identidade, o vinculo e a auditoria de criacao da Presenca nao podem ser alterados.' USING ERRCODE = '23514'; END IF;
            END IF;
            IF TG_OP = 'INSERT' THEN
                SELECT status INTO status_aula FROM aulas WHERE organizacao_id = NEW.organizacao_id AND unidade_id = NEW.unidade_id AND id = NEW.aula_id;
                IF status_aula IS NULL THEN RAISE EXCEPTION 'A Presenca exige uma Aula valida.' USING ERRCODE = '23514'; END IF;
                IF status_aula = 'Cancelada' THEN RAISE EXCEPTION 'Nao e possivel registrar Presenca em Aula cancelada.' USING ERRCODE = '23514'; END IF;
            END IF;
            RETURN NEW;
        END;
        $func$;
        CREATE TRIGGER trg_proteger_presenca BEFORE INSERT OR UPDATE ON presencas FOR EACH ROW EXECUTE FUNCTION proteger_presenca();
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS cobrancas (
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
    CONSTRAINT fk_cobrancas_organizacao FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_unidade FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_aluno FOREIGN KEY (organizacao_id, aluno_id) REFERENCES alunos (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_matricula FOREIGN KEY (organizacao_id, unidade_id, matricula_id) REFERENCES matriculas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_criado_por_usuario_id FOREIGN KEY (criado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT fk_cobrancas_atualizado_por_usuario_id FOREIGN KEY (atualizado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_cobrancas_tipo_valido CHECK (tipo IN ('Matricula', 'Mensalidade', 'Avulso')),
    CONSTRAINT ck_cobrancas_status_valido CHECK (status IN ('Pendente', 'Paga', 'Atrasada', 'Cancelada')),
    CONSTRAINT ck_cobrancas_valor_positivo CHECK (valor > 0),
    CONSTRAINT ck_cobrancas_valor_pago_nao_negativo CHECK (valor_pago >= 0),
    CONSTRAINT uq_cobrancas_organizacao_unidade_id UNIQUE (organizacao_id, unidade_id, id)
);
CREATE INDEX IF NOT EXISTS ix_cobrancas_organizacao_aluno ON cobrancas (organizacao_id, aluno_id);
CREATE INDEX IF NOT EXISTS ix_cobrancas_organizacao_matricula ON cobrancas (organizacao_id, matricula_id);
CREATE INDEX IF NOT EXISTS ix_cobrancas_organizacao_vencimento_status ON cobrancas (organizacao_id, data_vencimento, status);
CREATE INDEX IF NOT EXISTS ix_cobrancas_criado_por_usuario_id ON cobrancas (criado_por_usuario_id);
CREATE INDEX IF NOT EXISTS ix_cobrancas_atualizado_por_usuario_id ON cobrancas (atualizado_por_usuario_id);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'proteger_cobranca') THEN
        CREATE FUNCTION proteger_cobranca() RETURNS trigger LANGUAGE plpgsql AS $func$
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id OR NEW.matricula_id IS DISTINCT FROM OLD.matricula_id OR NEW.tipo IS DISTINCT FROM OLD.tipo OR NEW.valor IS DISTINCT FROM OLD.valor OR NEW.data_emissao IS DISTINCT FROM OLD.data_emissao OR NEW.data_vencimento IS DISTINCT FROM OLD.data_vencimento OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN RAISE EXCEPTION 'A identidade, o tipo, o valor, as datas de emissao/vencimento e a auditoria de criacao da Cobranca nao podem ser alterados.' USING ERRCODE = '23514'; END IF;
                IF OLD.status = 'Paga' AND NEW.status IS DISTINCT FROM OLD.status THEN RAISE EXCEPTION 'Uma Cobranca paga nao pode ter seu status alterado.' USING ERRCODE = '23514'; END IF;
                IF OLD.status = 'Cancelada' AND NEW.status IS DISTINCT FROM OLD.status THEN RAISE EXCEPTION 'Uma Cobranca cancelada nao pode ter seu status alterado.' USING ERRCODE = '23514'; END IF;
            END IF;
            RETURN NEW;
        END;
        $func$;
        CREATE TRIGGER trg_proteger_cobranca BEFORE INSERT OR UPDATE ON cobrancas FOR EACH ROW EXECUTE FUNCTION proteger_cobranca();
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS pagamentos (
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
    CONSTRAINT fk_pagamentos_organizacao FOREIGN KEY (organizacao_id) REFERENCES organizacoes (id) ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_unidade FOREIGN KEY (organizacao_id, unidade_id) REFERENCES unidades (organizacao_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_cobranca FOREIGN KEY (organizacao_id, unidade_id, cobranca_id) REFERENCES cobrancas (organizacao_id, unidade_id, id) ON DELETE RESTRICT,
    CONSTRAINT fk_pagamentos_registrado_por_usuario_id FOREIGN KEY (registrado_por_usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT,
    CONSTRAINT ck_pagamentos_forma_valida CHECK (forma_pagamento IN ('Dinheiro','Pix','CartaoCredito','CartaoDebito','Boleto','Transferencia','Outros')),
    CONSTRAINT ck_pagamentos_valor_positivo CHECK (valor > 0)
);
CREATE INDEX IF NOT EXISTS ix_pagamentos_organizacao_cobranca ON pagamentos (organizacao_id, cobranca_id);
CREATE INDEX IF NOT EXISTS ix_pagamentos_registrado_por_usuario_id ON pagamentos (registrado_por_usuario_id);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'proteger_pagamento') THEN
        CREATE FUNCTION proteger_pagamento() RETURNS trigger LANGUAGE plpgsql AS $func$
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF NEW.id IS DISTINCT FROM OLD.id OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id OR NEW.cobranca_id IS DISTINCT FROM OLD.cobranca_id OR NEW.valor IS DISTINCT FROM OLD.valor OR NEW.data_pagamento IS DISTINCT FROM OLD.data_pagamento OR NEW.forma_pagamento IS DISTINCT FROM OLD.forma_pagamento OR NEW.registrado_por_usuario_id IS DISTINCT FROM OLD.registrado_por_usuario_id OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN RAISE EXCEPTION 'Os dados do Pagamento sao imutaveis apos o registro.' USING ERRCODE = '23514'; END IF;
            END IF;
            RETURN NEW;
        END;
        $func$;
        CREATE TRIGGER trg_proteger_pagamento BEFORE INSERT OR UPDATE ON pagamentos FOR EACH ROW EXECUTE FUNCTION proteger_pagamento();
    END IF;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'atualizar_cobranca_apos_pagamento') THEN
        CREATE FUNCTION atualizar_cobranca_apos_pagamento() RETURNS trigger LANGUAGE plpgsql AS $func$
        DECLARE total_pago numeric(12,2); valor_cobranca numeric(12,2); data_venc_cobranca date; nova_data_pagamento date;
        BEGIN
            SELECT valor, data_vencimento INTO valor_cobranca, data_venc_cobranca FROM cobrancas WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
            SELECT COALESCE(SUM(valor), 0) INTO total_pago FROM pagamentos WHERE cobranca_id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
            IF total_pago >= valor_cobranca THEN
                SELECT MAX(data_pagamento) INTO nova_data_pagamento FROM pagamentos WHERE cobranca_id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
                UPDATE cobrancas SET valor_pago = total_pago, status = 'Paga', data_pagamento = nova_data_pagamento, atualizado_em_utc = NOW() WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
            ELSIF data_venc_cobranca < CURRENT_DATE THEN
                UPDATE cobrancas SET valor_pago = total_pago, status = 'Atrasada', data_pagamento = NULL, atualizado_em_utc = NOW() WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
            ELSE
                UPDATE cobrancas SET valor_pago = total_pago, status = 'Pendente', data_pagamento = NULL, atualizado_em_utc = NOW() WHERE id = COALESCE(NEW.cobranca_id, OLD.cobranca_id) AND organizacao_id = COALESCE(NEW.organizacao_id, OLD.organizacao_id);
            END IF;
            RETURN COALESCE(NEW, OLD);
        END;
        $func$;
        CREATE TRIGGER trg_atualizar_cobranca_apos_pagamento AFTER INSERT OR DELETE ON pagamentos FOR EACH ROW EXECUTE FUNCTION atualizar_cobranca_apos_pagamento();
    END IF;
END $$;

DO $$ BEGIN
    GRANT SELECT, INSERT, UPDATE ON TABLE aulas TO bfa_app_role;
    GRANT SELECT, INSERT, UPDATE ON TABLE presencas TO bfa_app_role;
    GRANT SELECT, INSERT, UPDATE ON TABLE cobrancas TO bfa_app_role;
    GRANT SELECT, INSERT ON TABLE pagamentos TO bfa_app_role;
EXCEPTION WHEN undefined_object THEN NULL;
END $$;

-- =============================================================
-- CONSTANTES
-- =============================================================

-- Professor Thalisson
-- Thalisson/Cerquilho
-- Thalisson/Tiete

-- Plano mensal (rede) v2 - pegar o id da versao

-- =============================================================
-- LIMPAR DADOS EXISTENTES (re-execucao segura)
-- =============================================================
ALTER TABLE matriculas_horarios DISABLE TRIGGER trg_proteger_matricula_horario;
ALTER TABLE matriculas DISABLE TRIGGER trg_proteger_matricula;
ALTER TABLE matriculas DISABLE TRIGGER trg_proteger_matricula_grade_aberta;
ALTER TABLE turmas_horarios DISABLE TRIGGER trg_proteger_turma_horario;
ALTER TABLE turmas_horarios DISABLE TRIGGER trg_proteger_turma_horario_grade_aberta;
ALTER TABLE turmas DISABLE TRIGGER trg_proteger_estado_turma;
ALTER TABLE turmas DISABLE TRIGGER trg_proteger_capacidade_turma_grade;
ALTER TABLE alunos DISABLE TRIGGER trg_proteger_aluno;
ALTER TABLE alunos DISABLE TRIGGER trg_proteger_aluno_matriculas;
DELETE FROM matriculas_horarios WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM matriculas WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM presencas WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM aulas WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM cobrancas WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM pagamentos WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM turmas_horarios WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM turmas WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM alunos_responsaveis WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
DELETE FROM alunos WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8';
ALTER TABLE alunos ENABLE TRIGGER trg_proteger_aluno;
ALTER TABLE alunos ENABLE TRIGGER trg_proteger_aluno_matriculas;
ALTER TABLE turmas ENABLE TRIGGER trg_proteger_estado_turma;
ALTER TABLE turmas ENABLE TRIGGER trg_proteger_capacidade_turma_grade;
ALTER TABLE turmas_horarios ENABLE TRIGGER trg_proteger_turma_horario;
ALTER TABLE turmas_horarios ENABLE TRIGGER trg_proteger_turma_horario_grade_aberta;
ALTER TABLE matriculas ENABLE TRIGGER trg_proteger_matricula;
ALTER TABLE matriculas ENABLE TRIGGER trg_proteger_matricula_grade_aberta;
ALTER TABLE matriculas_horarios ENABLE TRIGGER trg_proteger_matricula_horario;

-- =============================================================
-- 1. TURMAS - BFA-Cerquilho (6 turmas, 2 quadras)
-- =============================================================

-- Quadra 1
INSERT INTO turmas (id, organizacao_id, unidade_id, professor_unidade_id, nome, capacidade, ativo, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc) VALUES
('10000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Manha 1', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('10000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Tarde 1', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('10000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Noite 1', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW());

-- Quadra 2
INSERT INTO turmas (id, organizacao_id, unidade_id, professor_unidade_id, nome, capacidade, ativo, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc) VALUES
('10000001-0000-0000-0000-000000000004', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Manha 2', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('10000001-0000-0000-0000-000000000005', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Tarde 2', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('10000001-0000-0000-0000-000000000006', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '4889d49f-59a7-478a-81c5-7b8535e09855', 'Turma Noite 2', 15, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW());

-- =============================================================
-- 2. TURMAS - BFA-Tiete (3 turmas, 1 quadra)
-- =============================================================

INSERT INTO turmas (id, organizacao_id, unidade_id, professor_unidade_id, nome, capacidade, ativo, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc) VALUES
('20000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 'Turma Manha', 20, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('20000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 'Turma Tarde', 20, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('20000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 'Turma Noite', 10, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW());

-- =============================================================
-- 3. TURMAS_HORARIOS - Cerquilho
-- =============================================================
-- Quadra 1: Manha (08-10), Tarde (14-16), Noite (19-21) - Seg a Sex
-- Quadra 2: Manha (09-11), Tarde (15-17), Noite (20-22) - Seg a Sex

INSERT INTO turmas_horarios (id, organizacao_id, unidade_id, turma_id, professor_unidade_id, dia_semana, hora_inicio, hora_fim, vigencia_inicio, vigencia_fim, ativo, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc) VALUES
-- Turma Manha 1 (Quadra 1) - Seg, Qua, Sex
('30000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000001', '4889d49f-59a7-478a-81c5-7b8535e09855', 1, '08:00', '10:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000001', '4889d49f-59a7-478a-81c5-7b8535e09855', 3, '08:00', '10:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000001', '4889d49f-59a7-478a-81c5-7b8535e09855', 5, '08:00', '10:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Tarde 1 (Quadra 1) - Seg, Qua, Sex
('30000001-0000-0000-0000-000000000006', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000002', '4889d49f-59a7-478a-81c5-7b8535e09855', 1, '14:00', '16:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000007', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000002', '4889d49f-59a7-478a-81c5-7b8535e09855', 3, '14:00', '16:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000008', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000002', '4889d49f-59a7-478a-81c5-7b8535e09855', 5, '14:00', '16:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Noite 1 (Quadra 1) - Seg, Qua, Sex
('30000001-0000-0000-0000-000000000009', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000003', '4889d49f-59a7-478a-81c5-7b8535e09855', 1, '19:00', '21:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000010', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000003', '4889d49f-59a7-478a-81c5-7b8535e09855', 3, '19:00', '21:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000011', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000003', '4889d49f-59a7-478a-81c5-7b8535e09855', 5, '19:00', '21:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Manha 2 (Quadra 2) - Ter, Qui
('30000001-0000-0000-0000-000000000012', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000004', '4889d49f-59a7-478a-81c5-7b8535e09855', 2, '09:00', '11:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000013', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000004', '4889d49f-59a7-478a-81c5-7b8535e09855', 4, '09:00', '11:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Tarde 2 (Quadra 2) - Ter, Qui
('30000001-0000-0000-0000-000000000015', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000005', '4889d49f-59a7-478a-81c5-7b8535e09855', 2, '15:00', '17:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000016', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000005', '4889d49f-59a7-478a-81c5-7b8535e09855', 4, '15:00', '17:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Noite 2 (Quadra 2) - Ter, Qui
('30000001-0000-0000-0000-000000000018', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000006', '4889d49f-59a7-478a-81c5-7b8535e09855', 2, '20:00', '22:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('30000001-0000-0000-0000-000000000019', 'aa390a31-292b-477c-9ba6-4e549bad19b8', '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a', '10000001-0000-0000-0000-000000000006', '4889d49f-59a7-478a-81c5-7b8535e09855', 4, '20:00', '22:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW());

-- =============================================================
-- 4. TURMAS_HORARIOS - Tiete (1 quadra)
-- =============================================================

INSERT INTO turmas_horarios (id, organizacao_id, unidade_id, turma_id, professor_unidade_id, dia_semana, hora_inicio, hora_fim, vigencia_inicio, vigencia_fim, ativo, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc) VALUES
-- Turma Manha - Seg a Sex (06:00-08:00, antes de Cerquilho)
('40000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000001', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 1, '06:00', '08:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000001', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 2, '06:00', '08:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000001', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 3, '06:00', '08:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000004', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000001', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 4, '06:00', '08:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000005', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000001', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 5, '06:00', '08:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Tarde - Seg a Sex (12:00-14:00, entre Cerquilho 11:00 e 14:00)
('40000001-0000-0000-0000-000000000006', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000002', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 1, '12:00', '14:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000007', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000002', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 3, '12:00', '14:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000008', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000002', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 5, '12:00', '14:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),

-- Turma Noite - Seg a Sex (17:00-19:00, entre Cerquilho 16:00 e 19:00)
('40000001-0000-0000-0000-000000000009', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000003', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 1, '17:00', '19:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000010', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000003', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 3, '17:00', '19:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW()),
('40000001-0000-0000-0000-000000000011', 'aa390a31-292b-477c-9ba6-4e549bad19b8', 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4', '20000001-0000-0000-0000-000000000003', 'f45080e4-fa4d-4fd2-a5da-80ce02777627', 5, '17:00', '19:00', '2026-01-01', NULL, true, '219571e1-b69c-4b4f-a439-d9613b41b7b4', '219571e1-b69c-4b4f-a439-d9613b41b7b4', NOW(), NOW());

-- =============================================================
-- 5. ALUNOS - BFA-Cerquilho (90 alunas)
-- Nomes femininos brasileiros realistas
-- =============================================================

INSERT INTO alunos (id, organizacao_id, usuario_id, nome_completo, data_nascimento, cpf, telefone, email, ativo, criado_em_utc, atualizado_em_utc) VALUES
('A1000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ana Beatriz Silva', '2005-03-15', '11111111111', '(11) 98765-0001', 'ana.beatriz@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ana Clara Souza', '2004-07-22', '22222222222', '(11) 98765-0002', 'ana.clara@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ana Julia Ferreira', '2006-01-10', '33333333333', '(11) 98765-0003', 'ana.julia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000004', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ana Luisa Oliveira', '2003-11-05', '44444444444', '(11) 98765-0004', 'ana.luisa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000005', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Beatriz Santos', '2005-06-18', '55555555555', '(11) 98765-0005', 'beatriz@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000006', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Camila Ribeiro', '2004-09-30', '66666666666', '(11) 98765-0006', 'camila@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000007', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Carolina Lima', '2006-04-12', '77777777777', '(11) 98765-0007', 'carolina@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000008', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Cecilia Almeida', '2003-08-25', '88888888888', '(11) 98765-0008', 'cecilia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000009', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Clara Rodrigues', '2005-12-03', '99999999999', '(11) 98765-0009', 'clara@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000010', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Daniela Costa', '2004-02-14', '10101010101', '(11) 98765-0010', 'daniela@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000011', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Eduarda Martins', '2006-05-20', '12121212121', '(11) 98765-0011', 'eduarda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000012', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Elisa Pereira', '2003-10-08', '13131313131', '(11) 98765-0012', 'elisa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000013', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Emily Araujo', '2005-01-27', '14141414141', '(11) 98765-0013', 'emily@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000014', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Fernanda Gomes', '2004-08-16', '15151515151', '(11) 98765-0014', 'fernanda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000015', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Flavia Barbosa', '2006-03-09', '16161616161', '(11) 98765-0015', 'flavia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000016', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Gabriela Dias', '2003-07-04', '17171717171', '(11) 98765-0016', 'gabriela@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000017', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Helena Nunes', '2005-11-19', '18181818181', '(11) 98765-0017', 'helena@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000018', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Isabela Moreira', '2004-04-28', '19191919191', '(11) 98765-0018', 'isabela@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000019', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Julia Carvalho', '2006-09-11', '20202020202', '(11) 98765-0019', 'julia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000020', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Larissa Mendes', '2003-06-02', '21212121212', '(11) 98765-0020', 'larissa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000021', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Laura Freitas', '2005-08-13', '22222222223', '(11) 98765-0021', 'laura@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000022', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Leticia Campos', '2004-01-21', '23232323232', '(11) 98765-0022', 'leticia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000023', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Livia Monteiro', '2006-07-07', '24242424242', '(11) 98765-0023', 'livia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000024', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Lorena Teixeira', '2003-12-15', '25252525252', '(11) 98765-0024', 'lorena@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000025', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Luana Cardoso', '2005-05-24', '26262626262', '(11) 98765-0025', 'luana@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000026', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Manuela Azevedo', '2004-10-01', '27272727272', '(11) 98765-0026', 'manuela@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000027', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Maria Eduarda Lopes', '2006-02-18', '28282828282', '(11) 98765-0027', 'maria.eduarda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000028', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Marina Rocha', '2003-09-06', '29292929292', '(11) 98765-0028', 'marina@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000029', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Melissa Correia', '2005-04-30', '30303030303', '(11) 98765-0029', 'melissa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000030', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Natalia Rezende', '2004-12-22', '31313131313', '(11) 98765-0030', 'natalia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000031', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Nicole das Neves', '2006-06-14', '32323232323', '(11) 98765-0031', 'nicole@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000032', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Patricia Melo', '2003-03-08', '33333333334', '(11) 98765-0032', 'patricia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000033', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Paula Nascimento', '2005-10-17', '34343434343', '(11) 98765-0033', 'paula@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000034', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Raquel Vieira', '2004-05-25', '35353535353', '(11) 98765-0034', 'raquel@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000035', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Renata Albuquerque', '2006-08-03', '36363636363', '(11) 98765-0035', 'renata@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000036', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Sarah Cavalcanti', '2003-01-19', '37373737373', '(11) 98765-0036', 'sarah@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000037', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Sophia Ribeiro', '2005-07-11', '38383838383', '(11) 98765-0037', 'sophia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000038', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Valentina Araujo', '2004-11-28', '39393939393', '(11) 98765-0038', 'valentina@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000039', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Vitoria Cardoso', '2006-04-05', '40404040404', '(11) 98765-0039', 'vitoria@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000040', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Yasmin Duarte', '2003-08-12', '41414141414', '(11) 98765-0040', 'yasmin@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000041', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Amanda Fernandes', '2005-02-09', '42424242424', '(11) 98765-0041', 'amanda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000042', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Bianca Teles', '2004-06-16', '43434343434', '(11) 98765-0042', 'bianca@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000043', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Bruna Machado', '2006-10-23', '44444444445', '(11) 98765-0043', 'bruna@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000044', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Brenda Lacerda', '2003-05-01', '45454545454', '(11) 98765-0044', 'brenda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000045', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Carol Pinheiro', '2005-09-14', '46464646464', '(11) 98765-0045', 'carol@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000046', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Cris Medina', '2004-03-27', '47474747474', '(11) 98765-0046', 'cris@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000047', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Daiane Tavares', '2006-07-19', '48484848484', '(11) 98765-0047', 'daiane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000048', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Debora Prado', '2003-11-06', '49494949494', '(11) 98765-0048', 'debora@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000049', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Diana Queiroz', '2005-04-13', '50505050505', '(11) 98765-0049', 'diana@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000050', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Eliane Salles', '2004-08-20', '51515151515', '(11) 98765-0050', 'eliane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000051', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Erika Brito', '2006-12-08', '52525252525', '(11) 98765-0051', 'erika@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000052', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Fabiana Reis', '2003-04-25', '53535353535', '(11) 98765-0052', 'fabiana@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000053', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Franciele Sales', '2005-09-02', '54545454545', '(11) 98765-0053', 'franciele@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000054', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Graziela Silveira', '2004-01-15', '55555555556', '(11) 98765-0054', 'graziela@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000055', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Iara Fonseca', '2006-05-28', '56565656565', '(11) 98765-0055', 'iara@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000056', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Irene Pires', '2003-10-10', '57575757575', '(11) 98765-0056', 'irene@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000057', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Janaina Cunha', '2005-03-03', '58585858585', '(11) 98765-0057', 'janaina@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000058', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Joice Barros', '2004-07-21', '59595959595', '(11) 98765-0058', 'joice@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000059', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Karine Borges', '2006-11-14', '60606060606', '(11) 98765-0059', 'karine@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000060', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Laís Andrade', '2003-06-07', '61616161616', '(11) 98765-0060', 'lais@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000061', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Larissa Peixoto', '2005-12-19', '62626262626', '(11) 98765-0061', 'larissa.p@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000062', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Leonardo Souza', '2004-04-02', '63636363636', '(11) 98765-0062', 'leonardo@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000063', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Liliane Tosta', '2006-08-15', '64646464646', '(11) 98765-0063', 'liliane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000064', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Luzia Fagundes', '2003-02-28', '65656565656', '(11) 98765-0064', 'luzia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000065', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Marcia Teles', '2005-06-11', '66666666667', '(11) 98765-0065', 'marcia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000066', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Margareth Dias', '2004-10-24', '67676767676', '(11) 98765-0066', 'margareth@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000067', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Naira Junqueira', '2006-01-06', '68686868686', '(11) 98765-0067', 'naira@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000068', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Nayara Braga', '2003-09-18', '69696969696', '(11) 98765-0068', 'nayara@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000069', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Neusa Maia', '2005-05-05', '70707070707', '(11) 98765-0069', 'neusa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000070', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Nubia Salgado', '2004-11-12', '71717171717', '(11) 98765-0070', 'nubia@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000071', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Odete Barreto', '2006-03-29', '72727272727', '(11) 98765-0071', 'odete@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000072', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Olga Machado', '2003-07-16', '73737373737', '(11) 98765-0072', 'olga@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000073', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Paloma Neves', '2005-01-23', '74747474747', '(11) 98765-0073', 'paloma@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000074', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Priscila Amaral', '2004-08-08', '75757575757', '(11) 98765-0074', 'priscila@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000075', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Regiane Rangel', '2006-12-01', '76767676767', '(11) 98765-0075', 'regiane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000076', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Rita Seabra', '2003-04-14', '77777777778', '(11) 98765-0076', 'rita@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000077', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Roberta Nogueira', '2005-10-07', '78787878787', '(11) 98765-0077', 'roberta@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000078', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Sandra Valente', '2004-02-20', '79797979797', '(11) 98765-0078', 'sandra@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000079', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Selma Pacheco', '2006-06-03', '80808080808', '(11) 98765-0079', 'selma@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000080', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Tainara Loyola', '2003-11-26', '81818181818', '(11) 98765-0080', 'tainara@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000081', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Talita Miranda', '2005-08-19', '82828282828', '(11) 98765-0081', 'talita@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000082', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Tatiane Rezende', '2004-12-12', '83838383838', '(11) 98765-0082', 'tatiane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000083', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Tereza Cristina', '2006-04-25', '84848484848', '(11) 98765-0083', 'tereza@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000084', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Thais Moreira', '2003-09-08', '85858585858', '(11) 98765-0084', 'thais@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000085', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Vanessa Lara', '2005-03-21', '86868686868', '(11) 98765-0085', 'vanessa@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000086', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Vera Lúcia Santos', '2004-07-04', '87878787878', '(11) 98765-0086', 'vera@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000087', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Viviane Peixoto', '2006-11-17', '88888888889', '(11) 98765-0087', 'viviane@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000088', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Wanda Ribeiro', '2003-05-30', '89898989898', '(11) 98765-0088', 'wanda@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000089', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Wilma Cordeiro', '2005-01-08', '90909090909', '(11) 98765-0089', 'wilma@email.com', true, NOW(), NOW()),
('A1000001-0000-0000-0000-000000000090', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Zuleica Fontes', '2004-09-21', '91919191919', '(11) 98765-0090', 'zuleica@email.com', true, NOW(), NOW());

-- =============================================================
-- 6. ALUNOS - BFA-Tiete (50 alunas)
-- =============================================================

INSERT INTO alunos (id, organizacao_id, usuario_id, nome_completo, data_nascimento, cpf, telefone, email, ativo, criado_em_utc, atualizado_em_utc) VALUES
('B2000001-0000-0000-0000-000000000001', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Adriana Correia', '2004-03-12', '10000000001', '(11) 97654-0001', 'adriana.t@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000002', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Alessandra Pinto', '2005-07-25', '10000000002', '(11) 97654-0002', 'alessandra@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000003', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Alice Moreira', '2006-01-08', '10000000003', '(11) 97654-0003', 'alice@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000004', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Aline Duarte', '2003-11-03', '10000000004', '(11) 97654-0004', 'aline@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000005', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Andressa Lima', '2005-06-16', '10000000005', '(11) 97654-0005', 'andressa@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000006', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Angela Rocha', '2004-09-28', '10000000006', '(11) 97654-0006', 'angela@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000007', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Antonia Ramos', '2006-04-10', '10000000007', '(11) 97654-0007', 'antonia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000008', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Berenice Santos', '2003-08-22', '10000000008', '(11) 97654-0008', 'berenice@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000009', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Catarina Melo', '2005-12-05', '10000000009', '(11) 97654-0009', 'catarina@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000010', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Cristiane Alves', '2004-02-18', '10000000010', '(11) 97654-0010', 'cristiane@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000011', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Dalila Mendes', '2006-05-30', '10000000011', '(11) 97654-0011', 'dalila@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000012', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Denise Carvalho', '2003-10-14', '10000000012', '(11) 97654-0012', 'denise@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000013', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Elen Martins', '2005-04-07', '10000000013', '(11) 97654-0013', 'elen@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000014', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Eliane Barbosa', '2004-08-19', '10000000014', '(11) 97654-0014', 'eliane.t@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000015', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Elsa Vieira', '2006-11-02', '10000000015', '(11) 97654-0015', 'elsa@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000016', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Eni Barros', '2003-07-25', '10000000016', '(11) 97654-0016', 'eni@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000017', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Fatima Borges', '2005-01-18', '10000000017', '(11) 97654-0017', 'fatima@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000018', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Fernanda Costa', '2004-06-01', '10000000018', '(11) 97654-0018', 'fernanda.t@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000019', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Gilma Nascimento', '2006-09-14', '10000000019', '(11) 97654-0019', 'gilma@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000020', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Gladys Braga', '2003-03-27', '10000000020', '(11) 97654-0020', 'gladys@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000021', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Gloria Teixeira', '2005-08-10', '10000000021', '(11) 97654-0021', 'gloria@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000022', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Graciela Prado', '2004-12-23', '10000000022', '(11) 97654-0022', 'graciela@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000023', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Heloisa Cardoso', '2006-04-06', '10000000023', '(11) 97654-0023', 'heloisa@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000024', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Idalina Gomes', '2003-09-19', '10000000024', '(11) 97654-0024', 'idalina@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000025', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ilma Rangel', '2005-02-02', '10000000025', '(11) 97654-0025', 'ilma@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000026', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ivone Nunes', '2004-05-15', '10000000026', '(11) 97654-0026', 'ivone@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000027', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Jacira Azevedo', '2006-10-28', '10000000027', '(11) 97654-0027', 'jacira@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000028', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Leila Freitas', '2003-06-11', '10000000028', '(11) 97654-0028', 'leila@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000029', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Liana Lopes', '2005-11-24', '10000000029', '(11) 97654-0029', 'liana@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000030', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Lidia Medeiros', '2004-04-07', '10000000030', '(11) 97654-0030', 'lidia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000031', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Linda Falconi', '2006-07-20', '10000000031', '(11) 97654-0031', 'linda@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000032', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Lucia Macedo', '2003-01-03', '10000000032', '(11) 97654-0032', 'lucia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000033', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Luciana Seixas', '2005-08-16', '10000000033', '(11) 97654-0033', 'luciana@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000034', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Mara Tavares', '2004-12-29', '10000000034', '(11) 97654-0034', 'mara@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000035', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Marilda Salles', '2006-03-12', '10000000035', '(11) 97654-0035', 'marilda@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000036', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Marlene Nogueira', '2003-09-25', '10000000036', '(11) 97654-0036', 'marlene@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000037', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Miriam Pires', '2005-05-08', '10000000037', '(11) 97654-0037', 'miriam@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000038', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Nair Cunha', '2004-10-21', '10000000038', '(11) 97654-0038', 'nair@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000039', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Narcisa Salgado', '2006-02-03', '10000000039', '(11) 97654-0039', 'narcisa@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000040', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Noemia Silveira', '2003-07-16', '10000000040', '(11) 97654-0040', 'noemia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000041', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Odila Brito', '2005-01-29', '10000000041', '(11) 97654-0041', 'odila@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000042', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Ortencia Fonseca', '2004-06-12', '10000000042', '(11) 97654-0042', 'ortencia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000043', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Raimunda Nonata', '2006-09-25', '10000000043', '(11) 97654-0043', 'raimunda@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000044', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Rosali Campos', '2003-03-08', '10000000044', '(11) 97654-0044', 'rosali@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000045', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Rosangela Ribeiro', '2005-08-21', '10000000045', '(11) 97654-0045', 'rosangela@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000046', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Rute Andrade', '2004-12-04', '10000000046', '(11) 97654-0046', 'rute@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000047', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Sonia Oliveira', '2006-04-17', '10000000047', '(11) 97654-0047', 'sonia@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000048', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Terezinha Rezende', '2003-09-30', '10000000048', '(11) 97654-0048', 'terezinha@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000049', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Vera Lucia Maia', '2005-02-13', '10000000049', '(11) 97654-0049', 'vera.l@email.com', true, NOW(), NOW()),
('B2000001-0000-0000-0000-000000000050', 'aa390a31-292b-477c-9ba6-4e549bad19b8', NULL, 'Zenaide Queiroz', '2004-07-26', '10000000050', '(11) 97654-0050', 'zenaide@email.com', true, NOW(), NOW());

-- =============================================================
-- 7. MATRICULAS - BFA-Cerquilho (90 matriculas ativas)
-- Distribuidas entre as 6 turmas (15 por turma)
-- =============================================================

-- Usando CTE para gerar as matriculas
WITH turmas_cerq AS (
    SELECT id AS turma_id, ROW_NUMBER() OVER () AS rn
    FROM turmas
    WHERE unidade_id = '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a' AND ativo = true
    ORDER BY id
),
alunos_cerq AS (
    SELECT id AS aluno_id, ROW_NUMBER() OVER () AS rn
    FROM alunos
    WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8'
      AND id >= 'A1000001-0000-0000-0000-000000000001'
      AND id <= 'A1000001-0000-0000-0000-000000000090'
),
turma_horarios_cerq AS (
    SELECT th.id AS turma_horario_id, th.turma_id,
           ROW_NUMBER() OVER (PARTITION BY th.turma_id ORDER BY th.dia_semana) AS rn
    FROM turmas_horarios th
    WHERE th.unidade_id = '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a' AND th.ativo = true
      AND th.vigencia_fim IS NULL
)
INSERT INTO matriculas (id, organizacao_id, unidade_id, aluno_id, plano_versao_id, data_inicio, data_fim_prevista, data_fim_real, status, valor_mensal_contratado, cobra_taxa_matricula, valor_taxa_matricula, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('C3000001-0000-0000-0000-' || LPAD(ac.rn::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a',
    ac.aluno_id,
    (SELECT id FROM planos_versoes WHERE plano_id = '39a7fadd-b363-4f98-9b9e-58adac062f37'::uuid AND vigencia_fim IS NULL LIMIT 1),
    '2026-09-11'::date,
    '2027-09-10'::date,
    NULL,
    'Ativa',
    280.00,
    true,
    200.00,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM alunos_cerq ac;

-- =============================================================
-- 8. MATRICULAS - BFA-Tiete (50 matriculas ativas)
-- =============================================================

WITH alunos_tiete AS (
    SELECT id AS aluno_id, ROW_NUMBER() OVER () AS rn
    FROM alunos
    WHERE organizacao_id = 'aa390a31-292b-477c-9ba6-4e549bad19b8'
      AND id >= 'B2000001-0000-0000-0000-000000000001'
      AND id <= 'B2000001-0000-0000-0000-000000000050'
)
INSERT INTO matriculas (id, organizacao_id, unidade_id, aluno_id, plano_versao_id, data_inicio, data_fim_prevista, data_fim_real, status, valor_mensal_contratado, cobra_taxa_matricula, valor_taxa_matricula, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('D4000001-0000-0000-0000-' || LPAD(at_.rn::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    'ba1c02c3-993b-4e5f-9271-ecbccaca39b4',
    at_.aluno_id,
    (SELECT id FROM planos_versoes WHERE plano_id = '39a7fadd-b363-4f98-9b9e-58adac062f37'::uuid AND vigencia_fim IS NULL LIMIT 1),
    '2026-09-11'::date,
    '2027-09-10'::date,
    NULL,
    'Ativa',
    280.00,
    true,
    200.00,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM alunos_tiete at_;

-- =============================================================
-- 9. MATRICULAS_HORARIOS - Distribuir alunos nos horarios
-- Cerquilho: 90 alunos, 6 turmas, ~15 por turma
-- Tiete: 50 alunos, 3 turmas, ~17 por turma
-- =============================================================

-- Cerquilho: 15 alunos por turma, 2 horarios por turma = ~8 por horario
WITH matriculas_cerq AS (
    SELECT m.id AS matricula_id, ROW_NUMBER() OVER () AS rn
    FROM matriculas m
    WHERE m.unidade_id = '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a' AND m.status = 'Ativa'
),
turma_horarios_cerq AS (
    SELECT th.id AS turma_horario_id, th.turma_id,
           ROW_NUMBER() OVER (PARTITION BY th.turma_id ORDER BY th.dia_semana) AS rn_horario
    FROM turmas_horarios th
    WHERE th.unidade_id = '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a' AND th.ativo = true AND th.vigencia_fim IS NULL
)
INSERT INTO matriculas_horarios (id, organizacao_id, unidade_id, matricula_id, turma_horario_id, vigencia_inicio, vigencia_fim, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('E5000001-0000-0000-0000-' || LPAD(mc.rn::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a',
    mc.matricula_id,
    thc.turma_horario_id,
    '2026-09-11'::date,
    NULL,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM matriculas_cerq mc
JOIN turma_horarios_cerq thc
  ON ((mc.rn - 1) / 15) = (thc.turma_id::int % 6)
 AND thc.rn_horario <= 2;

-- Tiete: ~17 alunos por turma, 3 horarios por turma
WITH matriculas_tiete AS (
    SELECT m.id AS matricula_id, ROW_NUMBER() OVER () AS rn
    FROM matriculas m
    WHERE m.unidade_id = 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4' AND m.status = 'Ativa'
),
turma_horarios_tiete AS (
    SELECT th.id AS turma_horario_id, th.turma_id,
           ROW_NUMBER() OVER (PARTITION BY th.turma_id ORDER BY th.dia_semana) AS rn_horario
    FROM turmas_horarios th
    WHERE th.unidade_id = 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4' AND th.ativo = true AND th.vigencia_fim IS NULL
)
INSERT INTO matriculas_horarios (id, organizacao_id, unidade_id, matricula_id, turma_horario_id, vigencia_inicio, vigencia_fim, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('F6000001-0000-0000-0000-' || LPAD(mt.rn::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    'ba1c02c3-993b-4e5f-9271-ecbccaca39b4',
    mt.matricula_id,
    tht.turma_horario_id,
    '2026-09-11'::date,
    NULL,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM matriculas_tiete mt
JOIN turma_horarios_tiete tht
  ON ((mt.rn - 1) / 17) = (tht.turma_id::int % 3)
 AND tht.rn_horario <= 2;

-- =============================================================
-- 10. AULAS - Cerquilho (ultimas 4 semanas, seg-sex)
-- =============================================================

WITH turmas_cerq AS (
    SELECT id AS turma_id, unidade_id,
           ROW_NUMBER() OVER (ORDER BY id) AS rn
    FROM turmas WHERE unidade_id = '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a' AND ativo = true
),
datas_aula AS (
    SELECT generate_series('2026-08-04'::date, '2026-08-29'::date, '1 day'::interval)::date AS data,
           EXTRACT(ISODOW FROM generate_series('2026-08-04'::date, '2026-08-29'::date, '1 day'::interval)::date)::int AS dia_semana
)
INSERT INTO aulas (id, organizacao_id, unidade_id, turma_id, turma_horario_id, data, hora_inicio, hora_fim, status, capacidade, observacoes, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('G7000001-0000-0000-0000-' || LPAD(ROW_NUMBER() OVER ()::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    '0fdc33ad-4424-4ba8-845a-3e0a4d7bf94a',
    tc.turma_id,
    (SELECT id FROM turmas_horarios WHERE turma_id = tc.turma_id AND dia_semana = da.dia_semana AND ativo = true AND vigencia_fim IS NULL LIMIT 1),
    da.data,
    '08:00'::time,
    '10:00'::time,
    CASE WHEN da.data < CURRENT_DATE THEN 'Concluida' ELSE 'Programada' END,
    15,
    NULL,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM turmas_cerq tc
CROSS JOIN datas_aula da
WHERE da.dia_semana BETWEEN 1 AND 5
  AND (SELECT id FROM turmas_horarios WHERE turma_id = tc.turma_id AND dia_semana = da.dia_semana AND ativo = true AND vigencia_fim IS NULL LIMIT 1) IS NOT NULL;

-- =============================================================
-- 11. AULAS - Tiete (ultimas 4 semanas, seg-sex)
-- =============================================================

WITH turmas_tiete AS (
    SELECT id AS turma_id, unidade_id,
           ROW_NUMBER() OVER (ORDER BY id) AS rn
    FROM turmas WHERE unidade_id = 'ba1c02c3-993b-4e5f-9271-ecbccaca39b4' AND ativo = true
),
datas_aula AS (
    SELECT generate_series('2026-08-04'::date, '2026-08-29'::date, '1 day'::interval)::date AS data,
           EXTRACT(ISODOW FROM generate_series('2026-08-04'::date, '2026-08-29'::date, '1 day'::interval)::date)::int AS dia_semana
)
INSERT INTO aulas (id, organizacao_id, unidade_id, turma_id, turma_horario_id, data, hora_inicio, hora_fim, status, capacidade, observacoes, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('H8000001-0000-0000-0000-' || LPAD(ROW_NUMBER() OVER ()::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    'ba1c02c3-993b-4e5f-9271-ecbccaca39b4',
    tt.turma_id,
    (SELECT id FROM turmas_horarios WHERE turma_id = tt.turma_id AND dia_semana = da.dia_semana AND ativo = true AND vigencia_fim IS NULL LIMIT 1),
    da.data,
    '08:00'::time,
    '10:00'::time,
    CASE WHEN da.data < CURRENT_DATE THEN 'Concluida' ELSE 'Programada' END,
    20,
    NULL,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM turmas_tiete tt
CROSS JOIN datas_aula da
WHERE da.dia_semana BETWEEN 1 AND 5
  AND (SELECT id FROM turmas_horarios WHERE turma_id = tt.turma_id AND dia_semana = da.dia_semana AND ativo = true AND vigencia_fim IS NULL LIMIT 1) IS NOT NULL;

-- =============================================================
-- 12. COBRANCAS - Mensalidades para todos os alunos matriculados
-- =============================================================

WITH matriculas_ativas AS (
    SELECT m.id AS matricula_id, m.unidade_id, m.aluno_id, m.valor_mensal_contratado,
           ROW_NUMBER() OVER () AS rn
    FROM matriculas m
    WHERE m.status = 'Ativa'
)
INSERT INTO cobrancas (id, organizacao_id, unidade_id, aluno_id, matricula_id, tipo, descricao, valor, valor_pago, data_emissao, data_vencimento, data_pagamento, status, observacoes, criado_por_usuario_id, atualizado_por_usuario_id, criado_em_utc, atualizado_em_utc)
SELECT
    ('I9000001-0000-0000-0000-' || LPAD(ma.rn::text, 12, '0'))::uuid,
    'aa390a31-292b-477c-9ba6-4e549bad19b8',
    ma.unidade_id,
    ma.aluno_id,
    ma.matricula_id,
    'Mensalidade',
    'Mensalidade ' || TO_CHAR(CURRENT_DATE, 'MM/YYYY'),
    ma.valor_mensal_contratado,
    CASE WHEN ma.rn % 5 = 0 THEN ma.valor_mensal_contratado ELSE 0 END,
    CURRENT_DATE - INTERVAL '5 days',
    CURRENT_DATE + INTERVAL '15 days',
    CASE WHEN ma.rn % 5 = 0 THEN CURRENT_DATE ELSE NULL END,
    CASE WHEN ma.rn % 5 = 0 THEN 'Paga'
         WHEN ma.rn % 7 = 0 THEN 'Atrasada'
         ELSE 'Pendente' END,
    NULL,
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    '219571e1-b69c-4b4f-a439-d9613b41b7b4',
    NOW(),
    NOW()
FROM matriculas_ativas ma;

COMMIT;

-- =============================================================
-- RESUMO
-- =============================================================
-- BFA-Cerquilho:
--   90 alunas
--   6 turmas (3 por quadra, 2 quadras)
--   Aulas ultimas 4 semanas
--   90 matriculas ativas
--   Cobranca mensal para cada aluno
--
-- BFA-Tiete:
--   50 alunas
--   3 turmas (1 quadra)
--   Aulas ultimas 4 semanas
--   50 matriculas ativas
--   Cobranca mensal para cada aluno
-- =============================================================
