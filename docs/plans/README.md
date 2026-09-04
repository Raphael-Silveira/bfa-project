# Planos de Implementação — BFA Platform

Este diretório contém planos de implementação persistentes para tarefas não triviais do projeto BFA.

## Formato

Cada plano segue o padrão sequencial:

```text
docs/plans/NN_nome-curto.md
```

Exemplo: `01_corrigir-salvamento-alterar-grade.md`

## Estrutura do Plano

1. **Objetivo** — o que será feito
2. **Contexto atual** — estado antes da mudança
3. **Problema** — o que precisa ser resolvido
4. **Escopo** — o que será incluído
5. **Fora do escopo** — o que NÃO será feito
6. **Arquivos/módulos envolvidos**
7. **Regras de negócio afetadas**
8. **Banco / migration** — schema e alterações
9. **Autorização / governança**
10. **Histórico / imutabilidade**
11. **Concorrência / locks** — quando aplicável
12. **Estratégia de implementação**
13. **Riscos**
14. **Testes automatizados**
15. **Testes manuais**
16. **Critérios de aceite**
17. **Resultado** — preenchido após implementação
18. **Status** — Planejado / Em andamento / Concluído / Bloqueado

## Regras

- Nunca apagar planos antigos
- Planos são memória técnica do projeto
- Atualizar Status e Resultado após implementação
- Documentar decisões arquiteturais também em `ARCHITECTURE.md`

## Planos Existentes

| Nº | Plano | Status |
|----|-------|--------|
| 01 | [corrigir-salvamento-alterar-grade](01_corrigir-salvamento-alterar-grade.md) | Concluído |
| 02 | [melhorar-ux-encerrar-cancelar](02_melhorar-ux-encerrar-cancelar.md) | Concluído |