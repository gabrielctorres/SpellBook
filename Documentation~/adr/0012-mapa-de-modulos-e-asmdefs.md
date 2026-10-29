# 0012 — Mapa de módulos e asmdefs sem ciclos

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

Hoje existe um único assembly de runtime, `SpellBook.Core` (`Runtime/SpellBook.Core.asmdef`), com GAS, eventos e pooling juntos, e o barramento depende do GAS (`Runtime/Systems/Events/GAS_Events.cs`). O `Editor/SpellBook.Editor.asmdef` referencia o Core pelo GUID `7834e8f3d0902ad45bfb6fab58f7b919`.

As decisões I.5 e I.6 do usuário definem os módulos da 3.0: Events, Time, Pooling, Tags, Teams e Cues, além de GAS, GAS.Bridges, UI (só contratos, sem uGUI), UI.UGUI e GAS.UI. Todo sistema de que o GAS precisa vira módulo próprio, e o usuário escolhe quais usar.

## Decisão

1. **Módulos de runtime** (namespace igual ao nome do asmdef):

   | asmdef | Responsabilidade | Referencia |
   |---|---|---|
   | `SpellBook.Core` (mantém o `.meta` e o GUID atuais) | Tipos de base sem domínio: `Void`, `RuntimeSet<T>`, `RuntimeAnchor<T>`, `SpawnerAsset` (0015), base de `Condition` (0019), `TextRoleTheme`, constante da URL da doc, matemática | Unity, atributos do Odin |
   | `SpellBook.Time` | `ITimeSource` e fontes de tempo (0014) | Core |
   | `SpellBook.Events` | Canais, barramento, ouvinte, emissor, reações prontas (0016) | Core, Time (decisão I38) |
   | `SpellBook.Pooling` | Pool de objetos e `PooledSpawnerAsset` | Core |
   | `SpellBook.Tags` | `GameplayTag`, definição, container e query | Core |
   | `SpellBook.Teams` | Times e relação entre times | Core |
   | `SpellBook.Cues` | Cues por tag, roteador e parâmetros; spawn via `SpawnerAsset` | Core, Tags, Time |
   | `SpellBook.GAS` | ASC, atributos, efeitos, habilidades, ações e condições puras, descrição. **Só expõe eventos C# por instância** | Core, Tags, Time |
   | `SpellBook.GAS.Bridges` | Integrações do GAS com os demais módulos: relay de eventos, ações de cue, projétil e spawn, condições e filtros de time | Core, Tags, Time, GAS, Events, Cues, Pooling, Teams |
   | `SpellBook.UI` | Contratos de UI **sem uGUI**: fonte, binder, view, resolver de dono, driver de tick, tema, modelos (0017) | Core, Time |
   | `SpellBook.UI.UGUI` | Widgets padrão em uGUI + TextMeshPro | Core, Time, UI, uGUI/TMP |
   | `SpellBook.GAS.UI` | Fontes de UI que leem o GAS | Core, Tags, Time, GAS, UI (**não** Bridges) |

2. **Editor e testes:** um `SpellBook.<Módulo>.Editor` por módulo que precisar, referenciando só o runtime correspondente e as dependências dele; `SpellBook.Setup.Editor` sem referências (0011); asmdefs de teste por módulo (0004).
3. **Regras de dependência:**
   - O grafo é acíclico e só aponta "para baixo" na tabela.
   - Infraestrutura (Core, Time, Events, Pooling, Tags, Teams) nunca referencia GAS, Cues ou UI.
   - O GAS nunca referencia Events, Pooling, Teams, Cues, UI nem Bridges. Integrações ficam em assemblies de ponte.
   - `SpellBook.UI` não referencia uGUI, TextMeshPro nem GAS.
   - Referências de asmdef não são transitivas: cada asmdef lista explicitamente tudo o que usa.
4. **Pastas:** `Runtime/<Módulo>/`, `Editor/<Módulo>/`, `Tests/<Módulo>/`, cada uma com o seu asmdef.
5. Um teste EditMode lê os asmdefs do pacote e falha se alguma referência sair da tabela acima.

## Motivo

- Quem usa o pacote leva só os módulos de que precisa (por exemplo, só Events e Time, sem GAS).
- Fronteiras explícitas impedem que o GAS volte a depender do barramento ou de pooling.
- `[SerializeReference]` (0013) permite que as pontes estendam o GAS sem que o GAS as conheça.

## Consequências

- Mover tipos entre assemblies quebra assets com `[SerializeReference]` (aceito pela 0009).
- `SpellBook.UI.UGUI` depende de uGUI/TextMeshPro, que na Unity 6 vêm do pacote `com.unity.ugui`; isso precisa ser declarado.
- A tabela é a referência do Examinador no eixo ADR do `/revisao`. Um módulo novo exige emenda ou ADR nova.

## O que os agentes NÃO podem fazer

- Adicionar referência de asmdef fora da tabela ou criar ciclo.
- Fazer o GAS chamar barramento, canal, pool ou time diretamente.
- Colocar código uGUI ou TextMeshPro em `SpellBook.UI` ou em `SpellBook.GAS.UI`.
- Fazer `SpellBook.GAS.UI` referenciar `SpellBook.GAS.Bridges`.
- Trocar o `.meta` do `SpellBook.Core.asmdef`.
- Criar asmdef novo sem ADR.

## Emenda à regra de dependência (2026-10-29)

- **`SpellBook.Events` referencia Core e Time** (decisão I38), por causa da reação "Invoke com delay", que conta o atraso por um `TimeSourceAsset`. O grafo continua acíclico: Time só referencia Core. A frase "Infraestrutura nunca referencia GAS, Cues ou UI" continua valendo; infraestrutura pode referenciar infraestrutura que esteja acima dela na tabela.
- **Na 0-M, `SpellBook.GAS.Bridges` referencia só Core, Tags, Time, GAS e Events** (contém só a `RaiseEventAction`, decisões I22 e I39). Pooling, Cues e Teams entram nas referências quando o primeiro tipo que os usa entrar no Bridges.
- **Spawn e cue no GAS** (decisões I22 e I37): `SpawnProjectileAction`, `Projectile`, `SpawnVFXEffectAction` e `PlayCueAction` ficam em `SpellBook.GAS` e criam objetos só pelo `SpawnerAsset` do Core (0015). O GAS continua sem referência a Pooling.

## Decisões incorporadas (2026-10-29)

- **Asmdefs auxiliares** (decisão técnica; entram na tabela porque o item "criar asmdef novo sem ADR" é proibido):

  | asmdef | Pasta | Conteúdo | Referencia |
  |---|---|---|---|
  | `SpellBook.Core.Editor` (renomeia `SpellBook.Editor`, mantém o `.meta`) | `Editor/Core/` | Runner de validação, menu "SpellBook > Validar projeto", janela de resultados, `FolderStructureCreator` existente | Core, Odin Editor |
  | `SpellBook.Events.Editor` | `Editor/Events/` | Seletor de canal filtrado por payload, Event Debugger, Find references | Core, Time, Events, Odin Editor |
  | `SpellBook.Setup.Editor` | `Editor/Setup/` | Aviso de Odin ausente | nada |
  | `SpellBook.Testing` | `Tests/Testing/` | Factories de teste (sem testes) | todos os módulos de runtime; `defineConstraints` com `UNITY_INCLUDE_TESTS` |
  | `SpellBook.<Módulo>.Tests.EditMode` / `.PlayMode` | `Tests/<Módulo>/EditMode`, `Tests/<Módulo>/PlayMode` | Testes | o módulo, Time, `SpellBook.Testing` e o que o teste exercita |

- **Namespaces:** igual ao nome do asmdef (`SpellBook.Core`, `SpellBook.Time`, `SpellBook.Events`, `SpellBook.Pooling`, `SpellBook.Tags`, `SpellBook.GAS`). Os sub-namespaces do GAS (`SpellBook.GAS.Abilities`, `.Effects`, `.Attributes`, `.Core`, `.Targeting`) continuam.
- **`autoReferenced`** (decisão técnica): `true` nos asmdefs de runtime e de Editor, para que scripts sem asmdef (Assembly-CSharp) enxerguem os módulos instalados sem configuração; `false` nos de teste e no `SpellBook.Testing`, para que nada do projeto do usuário dependa deles. Módulos opcionais não ficam `false`: quem não usa um módulo simplesmente não o referencia, e o custo de compilação é o mesmo.
- **Pooling mantido** (decisão técnica): `PoolManager`, `ComponentPooler`, `PoolConfiguration`, `PoolableObject` e `IPoolable` mudam de pasta e de namespace (`SpellBook.Systems.Pooling` → `SpellBook.Pooling`) sem reescrita. O `PooledSpawnerAsset` usa o `ComponentPooler` internamente e cria os pools sob demanda, sem `PoolManager.Instance`. O `PoolManager` continua como componente opcional de pré-aquecimento. Motivo: o código funciona; reescrever não traz comportamento novo à 0-M.
- **`Math.cs`** (decisão I31): a classe estática `MathTools` vai para `Runtime/Core/MathTools.cs`, namespace `SpellBook.Core`; o `MonoBehaviour` `Math` é apagado.

## Emenda (2026-10-29): sem Odin, pela 0026

Esta emenda vale quando a 0026 for aceita.

- **`SpellBook.Core`:** referencia só a Unity. Sai "atributos do Odin" da tabela. O Core ganha:
  - os atributos de Inspector (`Runtime/Core/Inspector/`, namespace `SpellBook.Core.Inspector`: `TypePicker`, `TypeCategory`, `ShowIf`);
  - os tipos de validação (`Runtime/Core/Validation/`, namespace `SpellBook.Core.Validation`: `IValidatable`, `ValidationResult`, `ValidationMessage`, `ValidationSeverity`).
- **Asmdefs auxiliares:**
  - `SpellBook.Core.Editor` referencia só o Core. Ganha o catálogo, o popup e o drawer do seletor de tipo, o drawer do `ShowIf` e o Inspector base `SpellBookEditor` (validação no topo). Nasce no ticket 0M-41, não no 0M-38.
  - `SpellBook.Events.Editor` referencia Core, Time, Events e `Core.Editor`. Ganha os Inspectors do canal e do emissor e o drawer de `EventEmission`. Nasce no ticket 0M-45, não no 0M-34.
  - **Novos** (decisão I48, um `[CustomEditor]` por tipo):
    - `SpellBook.Time.Editor` (`Editor/Time/`): Core, Time, `Core.Editor`;
    - `SpellBook.Tags.Editor` (`Editor/Tags/`): Core, Tags, `Core.Editor`;
    - `SpellBook.Pooling.Editor` (`Editor/Pooling/`): Core, Pooling, `Core.Editor`;
    - `SpellBook.GAS.Editor` (`Editor/GAS/`): Core, Tags, Time, GAS, `Core.Editor`.

    Cada um contém só os Inspectors dos tipos do módulo.
  - **Regra:**
    - um `.Editor` de módulo pode referenciar o `SpellBook.Core.Editor`, o próprio runtime e as dependências dele;
    - um `.Editor` nunca referencia o `.Editor` de outro módulo;
    - o `Core.Editor` referencia só o Core.
  - Sai "Odin Editor" das referências de todos.
  - **`SpellBook.Setup.Editor` não existe** e sai da tabela.
- **Referências pré-compiladas:**
  - todo asmdef do pacote usa `overrideReferences: true`;
  - runtime e Editor usam `precompiledReferences: []`;
  - testes e `SpellBook.Testing` usam só `nunit.framework.dll`.

  O teste de asmdefs do item 5 cobra isso.

## Em aberto

- Aprovar `com.unity.ugui` como dependência declarada no `package.json` (dependência nova; só no épico de UI).
- `SpellBook.Cues` precisa mesmo de `Time` (para duração de cue) ou isso fica no spawner? (Decide-se na Spec do B-013.)

## Relacionadas

- 0009 (quebras), 0011 (Odin), 0013 (costura), 0014 (Time), 0015 (spawn), 0016 (Events), 0017 (UI).
