# Design — Day Use V1

## Decisões

- A configuração é `unidades.valor_day_use_sugerido`.
- O registro é uma entidade `DayUse` com `AlunoId` opcional e snapshot avulso opcional.
- `Pago` pertence ao Day Use nesta versão.
- A cobrança existente não será alterada.
- AdminUnidade e Professor usam o mesmo Application Service.
- A V025 cria `day_uses` e a coluna de configuração da Unidade.

## Autorização

AdminUnidade opera apenas na Unidade autorizada. Professor opera apenas em Unidade com vínculo ativo de Professor. O servidor resolve Organização, Unidade, Aluno e usuário autenticado.

## Telas

Cada área recebe uma entrada Day Use, com configuração somente na Unidade, formulário de registro e histórico paginado.

## Extensão: busca e exclusão

- O filtro por participante usa `EF.Functions.ILike` no PostgreSQL para aluno e avulso, antes da contagem/paginação. O termo é aparado e caracteres-curinga são escapados; acentos permanecem distintos.
- A exclusão é hard delete no caso de uso compartilhado. AdminUnidade opera apenas após autorização da Unidade. Professor fornece o próprio `UsuarioId`; a consulta filtra também Organização, Unidade e criador. AdminRede não recebe acesso operacional.
- A exclusão ocorre por POST com antiforgery, confirmação pelo `BfaConfirm`, PRG e filtros/página preservados. A página solicitada é limitada ao intervalo válido após remoção.
- A V025 permanece imutável. Como ela não concede DELETE ao papel runtime, a V026 concede somente DELETE em `day_uses` e registra a versão no histórico.
- `Registrado por` usa `PerfilUsuario.NomeCompleto`; sem perfil, a UI mostra apenas “Usuário”, nunca GUID.
