BEGIN;

DO $$
DECLARE
    duplicidade RECORD;
BEGIN
    SELECT
        organizacao_id,
        unidade_id,
        matricula_id,
        EXTRACT(YEAR FROM data_vencimento)::integer AS ano,
        EXTRACT(MONTH FROM data_vencimento)::integer AS mes,
        array_agg(id::text ORDER BY id) AS cobranca_ids
    INTO duplicidade
    FROM cobrancas
    WHERE tipo = 'Mensalidade'
    GROUP BY
        organizacao_id,
        unidade_id,
        matricula_id,
        EXTRACT(YEAR FROM data_vencimento),
        EXTRACT(MONTH FROM data_vencimento)
    HAVING COUNT(*) > 1
    LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION
            'V021 bloqueada: duplicidade de mensalidade para organizacao %, unidade %, matricula %, competencia %/%; saneamento previo necessario. Cobrancas: %',
            duplicidade.organizacao_id,
            duplicidade.unidade_id,
            duplicidade.matricula_id,
            duplicidade.mes,
            duplicidade.ano,
            duplicidade.cobranca_ids;
    END IF;

    SELECT
        organizacao_id,
        unidade_id,
        matricula_id,
        array_agg(id::text ORDER BY id) AS cobranca_ids
    INTO duplicidade
    FROM cobrancas
    WHERE tipo = 'Matricula'
    GROUP BY organizacao_id, unidade_id, matricula_id
    HAVING COUNT(*) > 1
    LIMIT 1;

    IF FOUND THEN
        RAISE EXCEPTION
            'V021 bloqueada: duplicidade de taxa de matricula para organizacao %, unidade %, matricula %; saneamento previo necessario. Cobrancas: %',
            duplicidade.organizacao_id,
            duplicidade.unidade_id,
            duplicidade.matricula_id,
            duplicidade.cobranca_ids;
    END IF;
END;
$$;

CREATE UNIQUE INDEX uq_cobrancas_mensalidade_competencia
    ON cobrancas (
        organizacao_id,
        unidade_id,
        matricula_id,
        tipo,
        (EXTRACT(YEAR FROM data_vencimento)),
        (EXTRACT(MONTH FROM data_vencimento))
    )
    WHERE tipo = 'Mensalidade';

CREATE UNIQUE INDEX uq_cobrancas_taxa_matricula
    ON cobrancas (
        organizacao_id,
        unidade_id,
        matricula_id,
        tipo
    )
    WHERE tipo = 'Matricula';

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V021', 'garantir idempotencia de cobrancas automaticas');

COMMIT;
