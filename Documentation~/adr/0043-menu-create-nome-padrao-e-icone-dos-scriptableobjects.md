# 0043 — Menu Create, nome padrão e ícone dos ScriptableObjects do pacote

Status: Proposta
Data: 2026-11-04

## Contexto

O pacote tem 44 tipos com `[CreateAssetMenu]` em `Runtime/` (levantamento de 2026-11-04, B-038 e B-039). O menu e o nome do arquivo novo não seguem uma regra:

- a raiz varia: `GAS/…` em 8 tipos do GAS e no `GameplayTagDefinition`, e `SpellBook/…` no resto. A issue #344 (B-034) corrige só a raiz para `SpellBook/`;
- o grupo nem sempre é o módulo: o `AbilityLoadout` (`SpellBook.GAS.UI`) está em `SpellBook/UI/`, enquanto o `FloatingTextRules`, do mesmo módulo, está em `SpellBook/GAS/UI/`. O `TextRoleTheme` está solto na raiz (`SpellBook/Text Role Theme`), e os outros tipos de texto ficam em `SpellBook/Text/`;
- o `fileName` tem cinco estilos: `GA_NewAbility`, `NewTag`, `Formula_Simple`, `PoolConfig_`, `Cue`. Os canais e as fontes de tempo não têm `fileName`, e a Unity usa `New <Tipo>`;
- nenhum tipo tem ícone próprio. No Project window, todo asset do pacote aparece com o ícone genérico de ScriptableObject.

Os novos tipos de cada épico (o `AbilityTemplate` do B-019a, por exemplo) precisam de uma regra que o Examinador possa conferir.

## Decisão

1. **Menu.** `menuName` = `SpellBook/<Grupo>/[<Subgrupo>/]<Nome>`. O `<Nome>` fica em inglês e em Title Case. Ele é o nome do tipo sem o sufixo técnico (`Asset`, e `Definition` quando não ajuda a ler) e sem o `Gameplay`, que já está implícito no grupo. Exemplos: `GameplayAbility` → `Ability`, `GameplayEffect` → `Effect`, `GameplayTagDefinition` → `Tag`.
2. **Grupo por módulo.** O grupo é o módulo do asmdef, sem `SpellBook.`. Há três exceções, todas na tabela abaixo:
   - `GAS.Bridges` e `GAS.UI` ficam dentro de `GAS` (`GAS/Events`, `GAS/UI`);
   - `Text` reúne os tipos de texto do Core e o adaptador do `Localization`;
   - `Spawn` reúne os spawners do Core e do Pooling.

   | Grupo do menu | Módulos (asmdef) | Subgrupos |
   |---|---|---|
   | `Core` | Core (âncoras) | — |
   | `Text` | Core (texto), Localization | — |
   | `Spawn` | Core, Pooling (spawners) | — |
   | `Time` | Time | — |
   | `Events` | Events | — |
   | `Pooling` | Pooling (configuração) | — |
   | `Tags` | Tags | — |
   | `Teams` | Teams | — |
   | `Cues` | Cues | — |
   | `GAS` | GAS, GAS.Bridges, GAS.UI | `Attributes`, `Attributes/Formulas`, `Events` (GAS.Bridges), `UI` (GAS.UI) |
   | `UI` | UI, UI.UGUI | — |

   Um subgrupo novo só entra quando o grupo passa de oito itens.
3. **Nome padrão do arquivo, sem prefixo.** Todo tipo declara `fileName` = `New <Nome>`, com o mesmo `<Nome>` do último trecho do `menuName`. Exemplos: `New Ability`, `New Effect`, `New Int Channel`. O pacote não impõe prefixo. Os prefixos (`GA_`, `GE_`, `CH_`…) são uma convenção do projeto: o Sandbox os usa nos assets dele, e no B-019a eles ficam nas regras de nome de cada template.

   **Famílias** (para o ícone do item 4 e para o item 6):

   | Família | Tipos |
   |---|---|
   | Habilidade | `GameplayAbility` |
   | Efeito | `GameplayEffect` |
   | Atributo | `AttributeDefinition` |
   | Categoria de atributo | `AttributeCategory` |
   | Fórmula | `SimpleAttributeFormula`, `ExpressionFormula` |
   | Política de recarga | `CooldownReductionPolicy` |
   | Tag | `GameplayTagDefinition` |
   | Canal | os 7 do Events e os 6 do GAS.Bridges |
   | Fonte de tempo | as 3 do Time |
   | Spawner | `InstantiateSpawnerAsset`, `PooledSpawnerAsset` |
   | Configuração de pool | `PoolConfiguration` |
   | Time | `TeamDefinition` |
   | Relação entre times | `TeamRelationMatrix` |
   | Cue | `CueDefinition` |
   | Biblioteca de cues | `CueLibrary` |
   | Âncora | `TransformAnchor`, `AbilitySystemAnchor`, `CueRouterAnchor`, `TooltipBinderAnchor` |
   | Texto | `TextTable`, `TableTextProvider`, `LocalizationTextProvider` |
   | Tema e estilo | `TextRoleTheme`, `UITheme`, `FloatingTextStyle` |
   | Regras de UI | `AbilityLoadout`, `FloatingTextRules`, `FixedKeyLabelProvider` |

4. **Ícone.** Cada família da tabela do item 3 tem um ícone próprio no pacote, ligado ao script do tipo. Toda a família usa o mesmo ícone. Ele aparece no Project window e no Inspector. O estilo e a forma de produção ficam em "Em aberto" e vão para a emenda que os responder.
5. **Guarda.** Um teste EditMode lê todos os tipos do pacote com `[CreateAssetMenu]`, fora dos assemblies de teste. Ele falha se:
   - o `menuName` não começa por `SpellBook/` e por um grupo permitido ao assembly do tipo (item 2);
   - o `fileName` não segue o item 3;
   - dois tipos têm o mesmo `menuName`;
   - um tipo não tem ícone, depois que o item 4 entrar.
6. **Tipo novo.** Um tipo novo com `[CreateAssetMenu]` entra na tabela do item 3 (família existente ou nova) no ticket que o cria. Uma família nova pede o ícone no mesmo ticket.

## Motivo

- Quem usa o pacote acha o asset pelo módulo que já conhece. Os grupos `Text` e `Spawn` ficam juntos porque quem cria um provedor de texto ou um spawner escolhe entre as opções do mesmo papel, de qualquer módulo.
- Sem prefixo, o pacote não impõe uma convenção de nomes ao jogo (decisão do usuário, 2026-11-04). `New <Nome>` repete o que o designer acabou de escolher no menu, e quem usa prefixo renomeia o asset como já faz.
- O ícone por família mostra o tipo do asset no Project window sem depender do nome, e custa uma imagem por família, não uma por tipo.
- A guarda transforma a regra em teste, então um tipo novo fora dela quebra a suíte, e o Examinador não precisa conferir à mão.

## Alternativas consideradas

- **Grupo só por módulo, sem `Text` e `Spawn`.** A guarda fica mais simples, mas o `LocalizationTextProvider` ficaria longe do `TableTextProvider`, e o `PooledSpawner` longe do `InstantiateSpawner`.
- **`fileName` com prefixo por família (`GA_New`, `CH_New`).** Ordena os assets no Project window, mas impõe ao jogo a convenção do pacote. Recusada no gate (Q1).
- **Sem `fileName`.** A Unity usa `New <Tipo>` (`New Gameplay Ability`), que não bate com o nome do menu. Recusada.
- **Um ícone por tipo.** Seriam 44 imagens. Os 13 canais e as 3 fontes de tempo, por exemplo, ficariam com ícones quase iguais.

## Consequências

- Os assets já criados não quebram: o menu e o `fileName` só valem para assets novos.
- As guardas existentes que fixam o menu (`ChannelMenuTests`, `GasChannelsTests`, `TimeSourceTests`) passam a conferir o `fileName` também, ou dão lugar à guarda do item 5.
- Os assets do Sandbox mantêm os prefixos deles, que são a convenção do Sample, não do pacote.
- O manual e o catálogo citam caminhos de menu. Eles são atualizados no passe de documentação (0007).
- Os ícones entram no pacote como imagens, cada uma com o próprio `.meta`, e o `.meta` de cada script passa a apontar para o ícone.

## O que os agentes NÃO podem fazer

- Criar `[CreateAssetMenu]` fora da regra dos itens 1 a 3 ou deixar um tipo de fora da tabela do item 3.
- Pôr exceção na guarda para um tipo do pacote. Os tipos de exemplo dentro de `Tests/` ficam fora da guarda porque estão em assemblies de teste.
- Usar um ícone de terceiros sem licença registrada.

## Decisões do gate parcial (2026-11-04)

- **Q1, nome padrão:** sem prefixo (`New Ability`, não `GA_New`). O item 3 foi reescrito, e a coluna de prefixo saiu da tabela.
- **Q2, grupos:** `Text` e `Spawn` ficam como grupos por função (item 2, como proposto).
- **Q3 e Q4 (estilo e variantes dos ícones):** continuam **em aberto** até o protótipo. O item 4 vale como direção (um ícone por família) e ganha uma emenda com a resposta.

A ADR continua Proposta até o usuário aceitá-la por inteiro. Os itens 1, 2, 3, 5 e 6 não dependem de Q3 e Q4.

## Em aberto

- **Estilo dos ícones (B-038, Q3, aberta até o protótipo):** vetorial (SVG feito pelo Claude e convertido em PNG) ou imagem pronta (PNG gerado pelo Codex). A escolha sai do protótipo com três famílias nos dois estilos. Se for SVG, falta definir onde fica a fonte e quem converte. A importação de SVG pela Unity pede o pacote Vector Graphics, a menos que a 6000.3 já o traga embutido. Isso ainda não foi conferido, e uma dependência nova pede aprovação.
- **Tamanho e variantes (Q4, aberta até o protótipo):** PNG de 128×128 com uma versão só, legível nos temas claro e escuro, ou duas versões, uma por tema.

## Relacionadas

- 0007 (documentação), 0012 (módulos e asmdefs), 0026 (editor próprio), 0028 (Samples), 0042 (tamanho do ticket).
