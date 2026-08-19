BEGIN;

ALTER TABLE franqueados
    DROP CONSTRAINT ck_franqueados_documento_tipo_pessoa;

ALTER TABLE franqueados
    ADD CONSTRAINT ck_franqueados_documento_tipo_pessoa CHECK (
        (tipo_pessoa = 'PessoaFisica' AND documento ~ '^[0-9]{11}$')
        OR
        (tipo_pessoa = 'PessoaJuridica' AND documento ~ '^[A-Z0-9]{12}[0-9]{2}$')
    );

INSERT INTO bfa_schema_history (version, descricao)
VALUES ('V005', 'adequar cnpj alfanumerico');

COMMIT;
