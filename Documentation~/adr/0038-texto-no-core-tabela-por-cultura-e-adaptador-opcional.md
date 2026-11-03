# 0038 — Texto no Core: `ITextProvider`, `LocalizedText`, `TextBuffer`, tabela por cultura e adaptador opcional do Unity Localization (respostas à 0023)

Status: Aceita (gate do Lote E, 2026-11-01; Q2, Q4 e Q5 aprovadas como propostas)
Data: 2026-10-31

## Contexto

A [0023](0023-texto-estruturado-e-itextprovider.md) decidiu a direção (linhas estruturadas, view formata, `ITextProvider`, adaptador opcional) e deixou em aberto o módulo do provedor, a forma da implementação padrão, o nome do asmdef do adaptador, a convenção das chaves e o plural. O B-008 (PRD `gas-descricao-estruturada.md`) e os itens de UI (B-015 a B-018) precisam dessas respostas antes do código. Restrições:

- O `SpellBook.UI` referencia só Core e Time; o `SpellBook.GAS` referencia Core, Tags e Time (0012). Os dois usam o provedor.
- O `TextRole` e o `TextRoleTheme` já vão para `Runtime/Core/Text/` (AR4 do B-005).
- A cultura nula do `AttributeDisplay.Format` é a invariante, sem configuração global (Q3 do B-005, aprovada).
- O teste `AsmdefRules_RuntimeAsmdefs_AutoReferencedWithoutConstraints` proíbe `defineConstraints` em asmdef de runtime, e o adaptador precisa de uma.
- A UI promete atualizar sem alocar (0017, item 5); uma `string` nova por atualização quebra isso.

## Decisão

1. **O texto mora no Core**, em `Runtime/Core/Text/`, namespace `SpellBook.Core.Text`. Não há módulo `SpellBook.Text`. São tipos de base sem domínio, usados por GAS e UI:
   - `ITextProvider`: `IFormatProvider Culture { get; }`, `string GetText(string key, string fallback)` (chave nula ou vazia devolve `fallback`; chave ausente também) e `event Action Changed` (troca de cultura ou de tabela);
   - `TextProviderAsset : ScriptableObject, ITextProvider` (abstrata), para campos de Inspector;
   - `LocalizedText` (`[Serializable] struct`): `key` (opcional) e `text` (o texto padrão do campo), com `Resolve(ITextProvider provider)`; provedor nulo devolve `text`;
   - `TextBuffer` (classe, `char[]` que cresce e é reaproveitado) e `TextSpan` (`readonly struct`: buffer, início, tamanho): escrever número com cultura e formato sem alocar (`float.TryFormat`), copiar texto, e `ToString()` só quando alguém pede;
   - `TextKey.IsValid(string)`: a convenção do item 3.
2. **Implementação padrão:** `TextTable : ScriptableObject` (nome da cultura e lista chave → texto) e `TableTextProvider : TextProviderAsset` (lista de tabelas e nome da cultura). A busca segue a cultura do provedor, depois a cultura pai (`pt-BR` → `pt`), depois as tabelas sem cultura, e por último o `fallback` do campo. O dicionário é montado uma vez por troca de cultura ou de tabela; ler uma chave já resolvida não aloca. Cultura vazia no provedor é a invariante (mesma regra do `AttributeDisplay`); o jogo troca por `SetCulture(CultureInfo)`, que dispara `Changed`.
3. **Chaves:** `<módulo>.<área>.<nome>`, segmentos em `[a-z0-9_]`, separados por ponto (`gas.activation.no_charges`). Chave é identificador, não texto visível, e pode ficar no código. As chaves do pacote (`gas.*`) têm texto padrão em duas tabelas que vêm no pacote (`pt-BR` e `en`), que o provedor do projeto lista. Sem provedor, um campo com `LocalizedText` mostra o próprio texto padrão; uma chave do pacote sem campo mostra a chave.
4. **Plural e gênero** ficam fora do contrato (P1 do PRD do B-008, respondida pelo usuário); o adaptador resolve pelos recursos do Unity Localization.
5. **Cultura em um lugar só:** quem exibe texto recebe a cultura do provedor que está usando (`provider.Culture`). Na UI, o provedor vem do `UITheme` (0039); na descrição, do `DescriptionContext`. Nenhum provedor global, estático ou singleton.
6. **Adaptador opcional:** asmdefs `SpellBook.Localization` (`Runtime/Localization/`, referencia `SpellBook.Core` e `Unity.Localization`) e `SpellBook.Localization.Editor` (`Editor/Localization/`, referencia `SpellBook.Core`, `SpellBook.Localization`, `SpellBook.Core.Editor`). Os dois têm `versionDefines` de `com.unity.localization` para o símbolo `SPELLBOOK_UNITY_LOCALIZATION` e esse símbolo como única `defineConstraints`. O `package.json` não declara o pacote; nenhum outro asmdef referencia `Unity.Localization` nem os dois asmdefs do adaptador.
7. **Emenda à 0012:** entra na tabela a categoria **módulo opcional de integração** (os dois asmdefs do item 6). A regra "runtime sem `defineConstraints`" passa a ter a exceção: um módulo opcional tem exatamente uma constraint, igual ao símbolo do próprio `versionDefines`. O teste de asmdefs cobra isso.
8. **Projeto host:** para o adaptador ser compilado e testado, o `com.unity.localization` entra só no `Packages/manifest.json` do projeto host (pergunta Q2 do gate; sem ele, o adaptador fica sem compilação verificada).

## Motivo

- Um contrato de 4 membros não justifica um módulo novo, e o Core já tem o `TextRoleTheme`.
- `LocalizedText` dá a mesma forma a todo texto visível (nome de habilidade, efeito, atributo, categoria, elementos de descrição); um campo de chave ao lado de cada `string` seria um mecanismo paralelo.
- `TextBuffer` resolve o "texto sem alocar" de uma vez, para a descrição e para a UI.
- Cultura no provedor evita uma segunda configuração e mantém a regra da Q3 do B-005.

## Consequências

- `DisplayName` de habilidade, efeito, atributo e categoria mudam de `string` para `LocalizedText`, com campo legado e migrador (mesmo padrão do VN2); os campos legados saem junto da pendência do B-007.
- A 0023 tem os "Em aberto" respondidos; a 0012 ganha a emenda do item 7 ao ser aceita esta ADR (uma linha apontando para cá, como na 0032).
- Quem não usa UI nem descrição não paga nada: os tipos são pequenos e sem dependência.

## O que os agentes NÃO podem fazer

- Criar provedor global, estático ou singleton, ou ler cultura de `CultureInfo.CurrentCulture` no runtime.
- Montar `string` por atualização onde um `TextBuffer` resolve.
- Referenciar `Unity.Localization` fora dos dois asmdefs do adaptador, ou declarar o pacote no `package.json`.
- Escrever texto visível no código; só chaves.
- Dar a um módulo opcional outra `defineConstraints` além do símbolo do próprio `versionDefines`.

## Relacionadas

- 0009 (quebra), 0012 (módulos), 0013 (costura), 0017 e 0039 (UI), 0023 (direção do texto), 0026 (editor), 0030 (migração com campo legado).
