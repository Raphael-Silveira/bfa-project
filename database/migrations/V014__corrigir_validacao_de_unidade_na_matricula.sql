BEGIN;

CREATE OR REPLACE FUNCTION proteger_plano_disponibilidade_unidade()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    plano_ativo boolean;
    plano_unidade_id uuid;
    unidade_ativa boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.plano_id IS DISTINCT FROM OLD.plano_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'A identidade, o escopo e a auditoria de criacao da disponibilidade nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    SELECT ativo, unidade_id
    INTO plano_ativo, plano_unidade_id
    FROM planos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.plano_id;

    IF plano_ativo IS NULL THEN
        RAISE EXCEPTION
            'A disponibilidade exige um plano do mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF plano_unidade_id IS NOT NULL THEN
        RAISE EXCEPTION
            'Somente um plano da rede pode possuir disponibilidade por unidade.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.ativo = true THEN
        SELECT ativa
        INTO unidade_ativa
        FROM unidades
        WHERE organizacao_id = NEW.organizacao_id
          AND id = NEW.unidade_id;

        IF plano_ativo IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma disponibilidade ativa exige um plano da rede ativo.'
                USING ERRCODE = '23514';
        END IF;

        IF unidade_ativa IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'Uma disponibilidade ativa exige uma unidade ativa no mesmo tenant.'
                USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION proteger_matricula()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    plano_id_da_versao uuid;
    vigencia_inicio_da_versao date;
    vigencia_fim_da_versao date;
    plano_ativo boolean;
    plano_unidade_id uuid;
    disponibilidade_ativa boolean;
    unidade_ativa boolean;
    aluno_ativo boolean;
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id IS DISTINCT FROM OLD.id
            OR NEW.organizacao_id IS DISTINCT FROM OLD.organizacao_id
            OR NEW.unidade_id IS DISTINCT FROM OLD.unidade_id
            OR NEW.aluno_id IS DISTINCT FROM OLD.aluno_id
            OR NEW.plano_versao_id IS DISTINCT FROM OLD.plano_versao_id
            OR NEW.data_inicio IS DISTINCT FROM OLD.data_inicio
            OR NEW.data_fim_prevista IS DISTINCT FROM OLD.data_fim_prevista
            OR NEW.valor_mensal_contratado IS DISTINCT FROM OLD.valor_mensal_contratado
            OR NEW.cobra_taxa_matricula IS DISTINCT FROM OLD.cobra_taxa_matricula
            OR NEW.valor_taxa_matricula IS DISTINCT FROM OLD.valor_taxa_matricula
            OR NEW.criado_por_usuario_id IS DISTINCT FROM OLD.criado_por_usuario_id
            OR NEW.criado_em_utc IS DISTINCT FROM OLD.criado_em_utc THEN
            RAISE EXCEPTION
                'O escopo e o snapshot contratual da matricula nao podem ser alterados.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status IN ('Encerrada', 'Cancelada')
            AND (
                NEW.status IS DISTINCT FROM OLD.status
                OR NEW.data_fim_real IS DISTINCT FROM OLD.data_fim_real
            ) THEN
            RAISE EXCEPTION
                'Uma matricula em estado terminal nao pode ser alterada.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.status = 'Ativa'
            AND NEW.status IS DISTINCT FROM OLD.status
            AND NEW.status NOT IN ('Encerrada', 'Cancelada') THEN
            RAISE EXCEPTION
                'Uma matricula ativa somente pode ser encerrada ou cancelada.'
                USING ERRCODE = '23514';
        END IF;

        IF OLD.data_fim_real IS NOT NULL
            AND NEW.data_fim_real IS DISTINCT FROM OLD.data_fim_real THEN
            RAISE EXCEPTION
                'A data final real da matricula so pode ser preenchida uma vez.'
                USING ERRCODE = '23514';
        END IF;

        RETURN NEW;
    END IF;

    IF NEW.status <> 'Ativa' OR NEW.data_fim_real IS NOT NULL THEN
        RAISE EXCEPTION
            'Uma nova matricula deve ser criada ativa e sem data final real.'
            USING ERRCODE = '23514';
    END IF;

    SELECT plano_id, vigencia_inicio, vigencia_fim
    INTO plano_id_da_versao, vigencia_inicio_da_versao, vigencia_fim_da_versao
    FROM planos_versoes
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.plano_versao_id
    FOR UPDATE;

    IF plano_id_da_versao IS NULL THEN
        RAISE EXCEPTION
            'A matricula exige uma versao de plano do mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF NEW.data_inicio < vigencia_inicio_da_versao
        OR (
            vigencia_fim_da_versao IS NOT NULL
            AND NEW.data_inicio > vigencia_fim_da_versao
        ) THEN
        RAISE EXCEPTION
            'A data de inicio deve pertencer a vigencia inclusiva da versao do plano.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo, unidade_id
    INTO plano_ativo, plano_unidade_id
    FROM planos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = plano_id_da_versao
    FOR UPDATE;

    IF plano_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige um plano ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    IF plano_unidade_id IS NULL THEN
        SELECT ativo
        INTO disponibilidade_ativa
        FROM planos_disponibilidades_unidades
        WHERE organizacao_id = NEW.organizacao_id
          AND plano_id = plano_id_da_versao
          AND unidade_id = NEW.unidade_id
        FOR UPDATE;

        IF disponibilidade_ativa IS DISTINCT FROM true THEN
            RAISE EXCEPTION
                'O plano da rede deve estar disponivel e ativo para a unidade da matricula.'
                USING ERRCODE = '23514';
        END IF;
    ELSIF plano_unidade_id <> NEW.unidade_id THEN
        RAISE EXCEPTION
            'Um plano local somente pode ser contratado em sua propria unidade.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativa
    INTO unidade_ativa
    FROM unidades
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.unidade_id
    FOR UPDATE;

    IF unidade_ativa IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige uma unidade ativa no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    SELECT ativo
    INTO aluno_ativo
    FROM alunos
    WHERE organizacao_id = NEW.organizacao_id
      AND id = NEW.aluno_id
    FOR UPDATE;

    IF aluno_ativo IS DISTINCT FROM true THEN
        RAISE EXCEPTION
            'Uma nova matricula exige um aluno ativo no mesmo tenant.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V014', 'corrigir validacao de unidade na matricula');

COMMIT;
