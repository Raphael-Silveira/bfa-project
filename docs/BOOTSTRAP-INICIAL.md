# Bootstrap inicial da BFA

O bootstrap inicial é uma operação manual e explícita, disponível somente no ambiente `Development`. Ele não é executado pelo startup normal da aplicação e não possui endpoint HTTP.

O comando garante:

- uma organização ativa chamada `Brazilian Footvolley Academy`, com slug `bfa`;
- dois usuários criados pelo ASP.NET Core Identity por meio de `UserManager`;
- um vínculo ativo `AdministradorRede`, sem Unidade, para cada usuário;
- reutilização dos dados equivalentes já existentes, sem duplicação.

O bootstrap pressupõe que V001, V002 e V003 já foram aplicadas por meio do processo de deploy de banco. Ele não cria schema, não executa migrations e não cria Unidade.

## Configuração em Development

Na raiz do repositório, entre no projeto Web:

```powershell
cd backend/src/BFA.Web
```

Configure as credenciais com .NET User Secrets. Os valores abaixo são apenas exemplos e devem ser substituídos antes da execução:

```powershell
dotnet user-secrets set "Bootstrap:Admin1:Email" "admin1@exemplo.com"
dotnet user-secrets set "Bootstrap:Admin1:Password" "SENHA_FORTE"

dotnet user-secrets set "Bootstrap:Admin2:Email" "admin2@exemplo.com"
dotnet user-secrets set "Bootstrap:Admin2:Password" "SENHA_FORTE"
```

A conexão Development também deve estar configurada em `ConnectionStrings:BfaDatabase` conforme `docs/ENVIRONMENTS.md`.

## Execução manual

Ainda dentro de `backend/src/BFA.Web`, execute:

```powershell
dotnet run -- --bootstrap-inicial
```

Sem a flag, o comando abaixo inicia a aplicação normalmente e não executa o bootstrap:

```powershell
dotnet run
```

O bootstrap é recusado fora de `Development`. Não há senha ou email padrão no código ou nos arquivos `appsettings`.

## Consistência e segurança

A organização, os usuários e os vínculos são processados em uma única transação de banco. Uma falha impede o commit do conjunto parcial.

Usuários são sempre localizados e criados por `UserManager<UsuarioIdentity>`. O código não manipula `PasswordHash` e não insere diretamente na tabela `usuarios`.

Um vínculo ativo equivalente é reutilizado. Um vínculo equivalente inativo é tratado como inconsistência e interrompe o bootstrap, pois uma autorização revogada não deve ser reativada silenciosamente.

O console mostra somente o estado das operações, sem email, senha, hash, `SecurityStamp`, tokens ou connection string.
