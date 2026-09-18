# Portal Aluno — Perfil Completo V1

## Escopo

Expandir o perfil do Aluno com apelido, endereço baseado no catálogo IBGE local e foto privada, mantendo nome, CPF, nascimento, tenant e vínculos protegidos.

## Requisitos

- **PERFIL-01**: O sistema SHALL persistir apelido opcional normalizado, limitado a 80 caracteres e sem caracteres de controle.
- **PERFIL-02**: O sistema SHALL persistir CEP somente com oito dígitos e validar Estado/Município no catálogo local.
- **PERFIL-03**: O aluno SHALL editar somente os campos aprovados; POSTs manipulados SHALL preservar os campos somente leitura.
- **PERFIL-04**: O sistema SHALL armazenar fotos somente em storage privado, com chave aleatória, aceitando JPEG/PNG/WebP reais de até 2 MB.
- **PERFIL-05**: O sistema SHALL processar a foto para avatar WebP 256x256 e remover a exposição do arquivo original.
- **PERFIL-06**: A foto SHALL ser servida somente após autorização do usuário autenticado, Organização e Unidade.
- **PERFIL-07**: A atualização administrativa existente SHALL preservar apelido, endereço e foto.
- **PERFIL-08**: V023 SHALL ser versionada em SQL, sem alterar V001–V022 e sem execução automática no startup.

## Fora do escopo

Histórico de endereços, integração externa de CEP, redesign da Área Unidade, alteração de login/Identity e aplicação da V023 em banco compartilhado.
