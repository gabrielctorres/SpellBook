# 0028 — Autoria de `Samples~` por renomeação temporária para `Samples`

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O Sandbox (B-032, Spec `spec/sample-sandbox.md`) é o primeiro conteúdo de `Packages/com.spellbook/Samples~/`. Outros virão com o B-019 e com o HUD (B-015 e B-016).

A Unity não importa pastas terminadas em `~`. Por isso cena, prefabs, assets e scripts do `Samples~` não abrem no Editor, não ganham `.meta` e não compilam no projeto de desenvolvimento (ADR 0027). Mesmo assim, esses arquivos precisam ser criados e editados na Unity: o YAML de cena e de prefab não se escreve à mão (`CLAUDE.md`), e cada arquivo precisa de `.meta` com GUID.

Há três caminhos:

| Alternativa | Como funciona | Problema |
|---|---|---|
| A. Renomear `Samples~` → `Samples` durante a edição | O pacote embutido é gravável. Sem o til, a Unity importa e compila o Sample dentro do pacote. Depois, a pasta volta a ser `Samples~` e os `.meta` vão junto | A Unity cria um `Samples.meta` na raiz do pacote, que precisa ser apagado. Esquecer a pasta renomeada põe o Sample no pacote publicado |
| B. Importar o Sample em `Assets/`, editar e copiar de volta | Fluxo do Package Manager | Duas cópias com o mesmo GUID no mesmo projeto. Fere a 0027 ("`Assets/` guarda só cenários de verificação") e abre caminho para divergência entre as cópias |
| C. Escrever YAML e `.meta` à mão | Sem Unity | Proibido pelo `CLAUDE.md` e frágil (GUIDs e fileIDs) |

## Decisão

Adotar a **alternativa A**:

1. Para criar ou editar conteúdo de um Sample, quem tem a trava do Editor (0005) fecha a Unity e renomeia `Packages/com.spellbook/Samples~` para `Packages/com.spellbook/Samples`. Depois abre a Unity e edita.
2. Ao terminar, fecha a Unity e faz três coisas:
   - renomeia de volta para `Samples~`;
   - apaga `Packages/com.spellbook/Samples.meta`;
   - abre a Unity de novo e roda a suíte.
3. Os testes e o commit são sempre feitos com a pasta como `Samples~`. A guarda `Package_HasNoUntildedSamplesFolder` (Spec do Sandbox, T1) falha se existir `Samples/` ou `Samples.meta` na raiz do pacote.
4. O Sample nunca é importado pelo Package Manager **no projeto de desenvolvimento**, porque os GUIDs repetiriam os da pasta renomeada. A conferência do Import é feita num projeto vazio separado (Spec do Sandbox, T5).
5. O `Sandbox.meta` (ou o da pasta de cada Sample) que a Unity cria dentro do `Samples~` é commitado. Ele fica fora da pasta copiada no Import e não tem efeito.

## Motivo

- Uma só cópia do Sample no repositório, editada pela Unity, com os `.meta` e GUIDs que ela gera.
- Nenhuma regra da 0027 muda: o Sample continua só no pacote, e `Assets/` não ganha conteúdo de pacote.
- O esquecimento (pasta renomeada ou `Samples.meta` órfão) é pego por teste, antes do commit.

## Consequências

- Os tickets de Sample usam Unity e seguem o WIP 1 (0003, emenda de 2026-10-29).
- Com a pasta renomeada, os testes de estrutura que varrem o pacote (`AsmdefRulesTests.AllExpectedAssembliesExist`, `HelpUrlTests`) enxergam o Sample e podem falhar. É esperado: os testes só valem com `Samples~`.
- O código do Sample não compila na suíte normal. A compilação é conferida na Unity com a pasta renomeada (critério de pronto dos tickets) e, no fim, no projeto vazio do T5.
- O `/implementar` e o `/revisao` precisam citar esta ADR nos tickets de Sample. Com a ADR aceita, a Fábrica e o Examinador seguem este passo a passo pela Spec, e as skills não mudam nesta entrega.

## O que os agentes NÃO podem fazer

- Commitar com `Samples/` (sem til) ou com `Samples.meta` na raiz do pacote.
- Rodar a suíte com a pasta renomeada e reportar o resultado como o do ticket.
- Importar o Sample pelo Package Manager no projeto de desenvolvimento.
- Escrever à mão YAML de cena, prefab ou `.meta` do Sample.
- Renomear a pasta com a Unity aberta.

## Decisões incorporadas (2026-10-31)

Gate do B-032:
- A ADR foi aceita sem mudança de texto.
- Na Spec, o usuário aprovou as guardas EditMode que leem arquivos do `Samples~` (U1).
- A guarda de referências por GUID foi recusada (U2). Um "Missing" no YAML do Sample é pego só na conferência com a pasta renomeada e no projeto vazio do T5.

## Relacionadas

- 0003 (tamanho de ticket e WIP), 0004 (testes primeiro), 0005 (editor-lock), 0007 (documentação como código), 0027 (estrutura do repositório).
