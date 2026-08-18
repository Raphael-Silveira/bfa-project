# Identidade visual BFA

Este diretório preserva a identidade visual oficial da BFA — Brazilian Footvolley Academy.

## Organização

```text
brand/
├── references/
│   └── identidade-v1/       Arquivos oficiais da identidade visual V1
├── guide/
│   └── brand-guide.md       Regras e documentação da marca
└── README.md
```

Os demais diretórios de referências já existentes, como `uniforms`, `courts` e `social`, permanecem reservados aos respectivos materiais.

### `brand/references`

Fonte oficial dos arquivos visuais e das referências aprovadas. Os PNGs em `identidade-v1` não devem ser redesenhados, comprimidos destrutivamente ou editados para uso direto pela aplicação.

### `brand/guide`

Documentação da identidade, da paleta, dos usos aprovados e das restrições da marca.

### `BFA.Web/wwwroot/images/brand`

Cópias dos assets aprovados utilizadas pelo site MVC/Razor. O projeto web nunca deve referenciar diretamente arquivos fora de `wwwroot`.

Alterações importantes na identidade visual devem atualizar o `brand-guide.md` e, quando aplicável, renovar as cópias publicadas no `wwwroot` a partir das referências oficiais.
