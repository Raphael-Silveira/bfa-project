# Design

## Persistência

V024 adiciona à tabela `aulas` as colunas nullable `motivo_cancelamento varchar(500)`, `cancelada_em_utc timestamptz` e `cancelada_por_usuario_id uuid`, com FK para `usuarios(id)` conforme o padrão de auditoria existente. V001–V023 permanecem inalteradas.

## Domínio e aplicação

`Aula.Cancelar` recebe motivo, usuário e instante UTC, normaliza o motivo e preserva os dados originais em repetição. O caso de uso do Professor consulta o vínculo ativo, a aula e os alunos elegíveis, reutiliza o padrão de upsert de `Presenca` e bloqueia operações em aula cancelada.

## Interface

A tela de confirmações existente passa a ser a tela de gestão da aula: resumo, participação somente leitura, chamada com `Presente`/`Ausente`/`Não marcado`, salvar chamada e modal de cancelamento. Aula cancelada fica somente leitura.

## Concorrência

O cancelamento verifica presença no mesmo fluxo de persistência e o trigger existente continua impedindo transições inválidas. A gravação de chamada revalida o status antes de persistir.
