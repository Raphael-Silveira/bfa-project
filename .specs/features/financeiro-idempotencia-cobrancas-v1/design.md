# Design — Idempotência Concorrente de Cobranças V1

## Decisão

Usar o PostgreSQL como autoridade da identidade financeira por meio de dois
índices únicos parciais. Os índices incluem cobranças canceladas na identidade
e excluem somente `Avulso` pelo predicado de tipo.

## Identidade

```text
Mensalidade: organizacao_id + unidade_id + matricula_id + tipo + ano/mês(data_vencimento)
Taxa:        organizacao_id + unidade_id + matricula_id + tipo
Avulso:      sem unicidade automática
```

`DataVencimento` continuará exercendo o papel técnico de competência nesta
fase. Não será criada coluna `competencia`.

## Migration V021

1. Validar duplicidades em uma transação antes dos índices.
2. Falhar com `RAISE EXCEPTION` claro se houver duplicidade.
3. Criar índice único de mensalidade por ano/mês de `data_vencimento`.
4. Criar índice único de taxa por matrícula.
5. Registrar V021 somente depois da criação dos índices.

O SQL usará `Mensalidade` e `Matricula` somente após confirmação da conversão
string do enum e das constraints existentes. A migration não fará limpeza.

## Aplicação

Manter as consultas de existência como caminho rápido. Criar uma operação de
persistência específica para cobranças automáticas que:

- tenta inserir;
- captura somente `PostgresException.SqlState == 23505` em um dos dois índices
  financeiros conhecidos;
- desanexa a entidade que falhou;
- consulta a cobrança vencedora pela mesma identidade;
- devolve a cobrança criada ou já existente ao job;
- relança violações de outros índices.

A criação manual continua usando o caminho existente e não deve mascarar
conflitos não idempotentes.

## Testes

Usar PostgreSQL temporário real, seguindo o padrão de concorrência já presente
na suíte. A migration será aplicada contra uma estrutura isolada, e duas
instâncias do job usarão contextos/conexões distintos para disputar o mesmo
INSERT.

Hangfire não será habilitado. A aplicação não será iniciada nesta fase.
