# 0017 — UI em três camadas (fonte → binder → view)

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

O pacote não tem código de UI. Quem usa o GAS precisa mostrar barras de atributo, slots de habilidade com recarga e cargas, tooltips, lista de efeitos e números flutuantes, e cada projeto tem o seu kit visual. A decisão I.6 do usuário: contratos em `SpellBook.UI`, widgets em `SpellBook.UI.UGUI`; UI Toolkit fica para depois.

## Decisão

1. Toda UI do pacote segue três camadas:
   - **Fonte** (`UISource<TModel>`, classe abstrata `[SerializeReference]`): sabe ler um sistema (por exemplo, o GAS) para um dono e produzir um **modelo** (struct). Tem um modelo de prévia para o modo de edição.
   - **Binder** (componente, um por tipo de modelo): resolve o dono, liga a fonte, recebe atualizações e entrega o modelo à view. Atualiza por **push** (eventos C# do componente); só pede tick enquanto existe algo correndo no tempo (recarga, duração), por um único `UIUpdateDriver` que lê uma fonte de tempo (0014).
   - **View** (`IView<TModel>`): desenha. Recebe o struct e não conhece o GAS nem a fonte.
2. **Dono** resolvido por `ActorResolver`: no próprio objeto ou pai, por `RuntimeAnchor` (asset que o ator registra ao nascer) ou explícito. Nunca por singleton nem por canal global (0016).
3. **Módulos** (0012):
   - `SpellBook.UI`: contratos, binders, resolver, driver, tema e modelos. Sem uGUI, sem TextMeshPro, sem GAS.
   - `SpellBook.UI.UGUI`: widgets padrão (uGUI + TextMeshPro) que implementam `IView<T>`; listas e números flutuantes usam `SpawnerAsset` (0015).
   - `SpellBook.GAS.UI`: fontes que leem o GAS. Não depende de Bridges.
4. São dados (assets): tema, estilos, loadout de habilidades, âncoras, prefabs de widget e a escolha de fonte, resolver e filtros. É código só o que estende: novas fontes, novas views, provedores de rótulo de tecla e de texto.
5. Modelos são structs; atualizar a UI não aloca.

## Motivo

- O kit visual de cada projeto implementa `IView<T>` e reaproveita fontes e binders.
- Push com tick sob demanda evita `Update` em todo widget.
- A prévia em edição deixa montar a HUD sem entrar no Play.

## Consequências

- Os widgets do pacote servem para protótipo, Samples e projetos sem kit próprio; não precisam cobrir todo estilo visual.
- Uma view em UI Toolkit, no futuro, é só outra implementação de `IView<T>` num asmdef novo.
- Os eventos C# do GAS precisam cobrir o que as fontes leem (efeitos aplicados/removidos, habilidade concedida/removida, cargas), o que entra nas fases do GAS.

## O que os agentes NÃO podem fazer

- Fazer uma view conhecer o GAS, uma fonte ou um componente de gameplay.
- Achar o dono por `FindObjectOfType`, singleton, tag de GameObject ou canal global.
- Fazer polling em `Update` de cada widget quando há evento disponível.
- Colocar uGUI ou TextMeshPro em `SpellBook.UI` ou `SpellBook.GAS.UI`.
- Alocar por atualização (LINQ, concatenação de string, coleções novas).

## Em aberto

- Contrato do bind: a fonte devolve `IDisposable` ou um `IUIBinding` com `WantsTick`/`Tick`?
- Fontes tipadas (`UISource<TModel>`) dependem do suporte a genéricos em `[SerializeReference]` (0013); sem ele, o campo do binder usa o tipo base fechado e cada fonte concreta é não genérica.
- Dependência de `com.unity.ugui` para TextMeshPro (0012).

## Relacionadas

- 0012 (módulos), 0013 (fontes por `[SerializeReference]`), 0014 (driver de tempo), 0015 (spawn de widgets), 0016 (regra por instância), 0023 (texto).
