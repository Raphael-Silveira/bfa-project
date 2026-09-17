# Portal Aluno / Responsável — Fundação V1

Status: Fase 1 autorizada; Fase 2 bloqueada até o gate de segurança.

## Escopo desta fase

Implementar somente as correções de segurança e consistência da Área Aluno:

- aplicar a policy `PoliticasAcesso.Aluno` aos endpoints;
- resolver o aluno exclusivamente por usuário autenticado, vínculo ativo, organização e unidade;
- restringir agenda às aulas relacionadas às matrículas/horários do aluno;
- construir frequência somente com presenças reais do aluno;
- corrigir `OrganizacaoId` no ViewModel do dashboard;
- corrigir a janela usada para localizar a próxima aula;
- adicionar testes de isolamento e regressão.

Não fazem parte desta fase provisionamento, login por CPF, edição de cadastro, Área Responsável, Financeiro, Hangfire ou migrations.

## Identidade e provisionamento futuro

1. A pessoa é a identidade do sistema. Aluno com acesso próprio usa um `UsuarioIdentity`; responsável com acesso usa outro `UsuarioIdentity`.
2. Não haverá conta por matrícula nem por unidade.
3. Um `Aluno.UsuarioId` existente deve reutilizar a identidade correspondente.
4. Sem `UsuarioId`, o provisionamento futuro deverá localizar identidade compatível por regra explícita e segura; na ausência, criar uma identidade única e associá-la ao aluno.
5. O provisionamento deverá criar ou reativar apenas um `VinculoAcesso` ativo com `PerfilAcesso.Aluno`, na organização e unidade corretas.
6. Repetições deverão ser idempotentes: nunca criar segunda identidade nem segundo vínculo ativo.
7. A transação de matrícula não deverá conter criação improvisada de Identity. A ordem, retry e falha parcial serão definidos antes da Fase 2.

## Login por CPF futuro

1. CPF será normalizado para onze dígitos e tratado como identificador, nunca como senha.
2. O login atual por e-mail/username deverá continuar funcionando para todos os perfis existentes.
3. A estratégia futura preferida é entrada única `CPF ou e-mail`, com resolução de CPF somente para Aluno/Responsável.
4. CPF não será usado como senha, data de nascimento ou telefone não serão usados como senha.

## Primeiro acesso futuro

1. Reutilizar o mecanismo existente de primeiro acesso, token e definição de senha.
2. Não criar senha padrão compartilhada.
3. A credencial temporária deverá ser aleatória e imprevisível.
4. A senha definitiva deverá ser definida antes de liberar o portal.
5. Antes da Fase 2 será confirmado se existe estado persistido de troca obrigatória; se não existir, a necessidade de schema será apresentada antes de qualquer V022.

## Regra futura de Aluno e Responsável

1. Aluno com acesso próprio mantém acesso vinculado ao próprio `Aluno.UsuarioId`.
2. A existência de qualquer responsável não remove automaticamente o acesso próprio do aluno.
3. Antes do acesso de responsável, deve ser identificado qual vínculo representa autorização de acesso. `principal_contato`, `responsavel_financeiro` e contato não serão tratados como equivalentes sem decisão de produto.
4. Se o modelo atual não distinguir responsável de acesso, a implementação deverá parar e apresentar alternativas antes de criar migration.
5. Um responsável com múltiplos alunos usará uma única identidade e os vínculos existentes deverão limitar a consulta somente aos alunos relacionados.

## Gate obrigatório antes da Fase 2

Devem estar verdes testes que provem:

- Aluno A não acessa dados do Aluno B;
- organização A não acessa organização B;
- unidade A não acessa unidade B sem vínculo;
- agenda contém apenas aulas ligadas ao aluno;
- frequência contém apenas presenças reais do aluno;
- AdministradorRede, AdministradorUnidade e Professor não são tratados como Aluno.

## Regressão obrigatória

Executar cobertura existente da Franqueadora, Unidade e Professor, incluindo login, destino pós-login e fluxos de Usuários, Unidades, Franqueados, Alunos, Matrículas, Professores, Turmas e Cobranças. Nenhum layout, rota ou regra desses módulos deverá ser alterado nesta fase.

## Migration

V001–V021 permanecem imutáveis. A Fase 1 não cria migration e usa somente o schema existente. Qualquer necessidade de persistência para CPF, troca obrigatória de senha ou autorização de responsável será apresentada com motivo, campos, alternativas sem migration e impacto antes de propor V022.

## Critérios de aceitação da Fase 1

- endpoints da Área Aluno exigem policy de Aluno;
- consultas de agenda usam o vínculo matrícula → horário → turma/horário → aula;
- frequência usa `Presenca.AlunoId`, `Presenca.MatriculaId` e o período solicitado;
- dashboard usa a organização real e uma janela que inclui aulas futuras;
- testes de segurança e regressão passam;
- Fase 2 não é iniciada;
- nenhum código financeiro, Hangfire ou migration é alterado.
