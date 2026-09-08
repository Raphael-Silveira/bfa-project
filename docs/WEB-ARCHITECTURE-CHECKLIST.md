# Checklist de Arquitetura do BFA.Web

**Conclusão:** coerente com ajustes.

## O que está correto

- `Program.cs` funciona como composition root do host único.
- `Areas` separa Franqueadora, Unidade, Professor e Aluno sem criar outro backend.
- Controllers delegam para Application em vez de acessar DbContext diretamente.
- Views não consultam banco e recebem ViewModels próprios.
- Shell administrativo compartilhado e layouts por área estão coerentes.
- CSS e componentes compartilhados existem para padronizar a UI.

## O que ainda merece ajuste

- Alguns controllers concentram paginação, parsing e formatação demais.
- Algumas views ainda têm lógica de apresentação mais pesada do que o ideal.
- Há variação de padrão entre áreas (principalmente no Professor).
- A nomenclatura de alguns arquivos/pastas de ViewModel merece alinhamento.
- A documentação precisa acompanhar o estado real do código com mais frequência.

## Regra prática para a próxima fase

- Se a tela repete markup, extrair partial ou componente.
- Se o controller só estiver montando ViewModel, mantê-lo fino.
- Se houver regra de apresentação recorrente, centralizar em um padrão comum.
- Se houver divergência entre docs e código, o código vence e a doc deve ser atualizada.

## Próxima leitura obrigatória antes de novas mudanças de UI

1. `docs/ARCHITECTURE.md`
2. `docs/UI-ADMIN-STANDARDS.md`
3. `brand/guide/brand-guide.md`
4. Este checklist
