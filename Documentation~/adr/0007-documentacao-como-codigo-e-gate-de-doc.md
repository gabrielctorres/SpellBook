# 0007 — Documentação como código e gate de doc no `/revisao`

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O SpellBook entrega sistemas e ferramentas para dev e designer. Sem página, um componente não é usado direito. Hoje o `package.json` tem `documentationUrl` igual a `https://example.com/` e nenhum componente tem `[HelpURL]`. O plano (seção H.7 e H.8) prevê validadores de asset, `[HelpURL]` e um catálogo de ações e condições gerado a partir dos mesmos metadados do Inspector. A decisão I.15 do usuário: o `/revisao` **reprova** ticket sem documentação.

## Decisão

1. O manual público é código: vive em `Documentation~/manual/`, em Markdown, e muda **no mesmo ticket e PR** que muda o comportamento.
2. **Gate no `/revisao`.** O Examinador registra achado (e portanto reprova) quando o ticket:
   - adiciona ou muda um componente (`MonoBehaviour`) ou `ScriptableObject` que o usuário do pacote cria ou configura, sem página nova ou atualizada;
   - adiciona ou muda uma ação, condição, reação ou outro tipo escolhido por `[SerializeReference]`, sem página **ou** sem entrada no catálogo gerado;
   - muda comportamento público sem atualizar a página correspondente.
3. **`[HelpURL]`.** Todo componente e `ScriptableObject` público do pacote tem `[HelpURL]` apontando para a página dele. A URL base é uma constante única no `SpellBook.Core`.
4. **Validadores.** Validadores de asset (0011: `ISelfValidator`), o menu "SpellBook/Validar projeto" e um teste EditMode (0004) que roda todos os validadores e cobra o `[HelpURL]` por reflexão.
5. **Catálogo.** O seletor do Inspector e o catálogo da doc leem os mesmos metadados (`[ActionCategory]`, resumo, XML docs), para nunca divergirem.
6. Quebras e migração: o ticket que quebra algo registra a migração na seção "Quebras de compatibilidade" da Spec; o DevOps a leva ao CHANGELOG (0008).
7. Os documentos internos (`prd/`, `spec/`, `adr/`, `planos/`, `backlog.md`, `TIME.md`) não são publicados.

## Motivo

- Documentação escrita depois não é escrita.
- O `[HelpURL]` leva o usuário do Inspector direto à página.
- Um teste automático cobra a regra sem depender da memória do revisor.

## Consequências

- Tickets ficam um pouco maiores (ver a pergunta em aberto da 0003 sobre a contagem de linhas).
- O gerador do catálogo precisa existir antes que a opção "entrada no catálogo" seja usada; até lá, ações e condições exigem página.
- O primeiro épico cria o esqueleto do manual, a constante da URL e o teste de `[HelpURL]`.

## O que os agentes NÃO podem fazer

- Aprovar ticket sem a página ou a entrada de catálogo exigida.
- Criar componente ou `ScriptableObject` público sem `[HelpURL]`.
- Escrever a URL da doc fora da constante única.
- Publicar documentos internos no site.
- Desligar ou pular o teste de validadores para o ticket passar.

## Decisões incorporadas (2026-10-29)

- **Conteúdo real desde a 0-M, sem stubs** (decisão I26). Uma página publicada nunca é só título ou "em breve"; a árvore do H.8 cresce fase a fase, e a navegação do site lista só páginas que existem. Um teste EditMode cobra que todo `[HelpURL]` aponte para uma página (e âncora) existente em `Documentation~/manual/` (CA54).
- **URL base:** `https://gabrielctorres.github.io/SpellBook/` (decisão I29, site público), na constante `SpellBook.Core.DocUrls.Base`. O `[HelpURL]` usa constantes derivadas dela (`DocUrls.Base + "modulos/eventos/#eventlistener"`), que o C# aceita em atributo por serem `const`. O `SpellBook.Setup.Editor`, que não pode referenciar o Core (0011), lê a URL do `documentationUrl` do `package.json` em vez de repetir a constante.
- **Escopo do `[HelpURL]`** (decisão técnica): todo tipo **público, concreto (não abstrato)** que deriva de `MonoBehaviour` ou `ScriptableObject` nos assemblies **de runtime** do SpellBook. Janelas e tipos de Editor ficam fora, porque o usuário não os adiciona a objetos.
- **Gerador do catálogo** (decisão técnica): fica para a Fase 9 (B-019). Até lá, ações, reações e condições escolhidas por `[SerializeReference]` exigem seção numa página do manual (por exemplo, as reações prontas da 0-M ficam na página Eventos, com uma âncora por reação).

## Emenda (2026-10-29): sem Odin, pela 0026

Esta emenda vale quando a 0026 for aceita.

- **Item 4 (Validadores):** os validadores implementam `SpellBook.Core.Validation.IValidatable` (0026, item 6), não o `ISelfValidator` do Odin. O menu e o teste EditMode continuam como estão.
- **Item 5 (Catálogo):** o metadado de categoria e resumo é o `[TypeCategory("Caminho", Summary = "...")]` do Core (0026, item 4), que vale para ações, reações e condições. O nome `[ActionCategory]` não é criado.
- **URL base no Setup:** o `SpellBook.Setup.Editor` não existe mais (0026). Por isso deixa de valer o trecho que diz que ele lê o `documentationUrl` do `package.json`. A constante `DocUrls.Base` continua sendo a única fonte da URL dentro do código.

## Em aberto

Nada.

## Relacionadas

- 0001 (pastas), 0003 (tamanho de ticket), 0004 (teste de validadores), 0011 (`ISelfValidator`), 0013 (`[SerializeReference]`), 0025 (ferramenta do site).
