# Design — Portal Aluno Perfil Completo V1

## Persistência

Os campos opcionais são adicionados diretamente em `alunos` por V023. Estado e Município usam as FKs existentes para `estados.codigo_ibge` e `municipios.codigo_ibge`; a Application valida a relação Estado/Município usando `ILocalidadesConsulta`.

## Aplicação

O fluxo existente de Perfil continua sendo a única porta de atualização. O Aluno é resolvido pelo usuário autenticado e Unidade autorizada. O domínio normaliza apelido, CEP e campos textuais; a Application valida e-mail, telefone e localidades.

## Fotos

SkiaSharp 4.152.0 foi adotado por licença MIT e compatibilidade declarada com net10.0. A implementação valida o codec real, recorta o centro, gera WebP 256x256 e grava sob storage privado. A tabela guarda apenas chave lógica, content-type e data de atualização.

## Segurança e consistência

O novo arquivo é salvo antes da atualização do Aluno; se a atualização falhar, o arquivo novo é removido. A foto anterior só é removida depois do commit da referência nova. A rota de leitura não recebe chave de arquivo: ela resolve o Aluno pelo usuário e contexto autorizado.
