# 0039 — UI: ligação criada pela fonte, fontes fechadas por modelo, âncora não genérica, listas por chave e uGUI declarada (respostas à 0017 e ao "Em aberto" da 0012)

Status: Aceita (gate do Lote E, 2026-11-01; Q1 aprovada como proposta)
Data: 2026-10-31

## Contexto

A [0017](0017-ui-em-tres-camadas.md) fixou as três camadas (fonte → binder → view) e deixou em aberto o contrato da ligação, os genéricos em `[SerializeReference]` e a dependência da uGUI. A [0012](0012-mapa-de-modulos-e-asmdefs.md) deixou em aberto declarar `com.unity.ugui`. Os PRDs B-015 a B-018 precisam das respostas. Restrições do código e das Specs aprovadas:

- O seletor de tipo só oferece tipos concretos não genéricos; um campo de tipo base genérico fechado funciona (0013, 0032).
- A Unity não serializa campo de tipo interface para `UnityEngine.Object`.
- O `RuntimeAnchor<T>` do CU1 (B-013) é genérico; o `SpellBook.UI` não conhece o tipo do dono (não referencia o GAS).
- A fonte guarda só configuração; o estado da ligação não pode ficar nela (0013, item 4).
- A uGUI 2.0.0 traz uGUI e TextMeshPro na Unity 6 (pacote embutido conferido na 6000.3.13f1: `com.unity.ugui` 2.0.0, asmdefs `UnityEngine.UI` e `Unity.TextMeshPro`). O projeto host não a tem hoje.

## Decisão

1. **Ligação criada pela fonte, uma por binder.** `UISource<TModel>` (abstrata, `[Serializable]`) cria **uma vez** um `UIBinding<TModel>` para cada binder, que guarda o estado da ligação. O binder chama `Attach(dono, contexto)` e `Detach()` na mesma ligação a cada troca de dono (respawn), sem alocar. A ligação entrega o modelo por `Publish(in TModel)` e pede ou solta o tick por `RequestTick(bool)`. O `UIUpdateDriver` chama `Tick(double tempo)` só das ligações que pediram, lendo um `ITimeSource` (0014). Não é `IDisposable`: o ciclo é `Attach`/`Detach`.
2. **Fontes fechadas por modelo.** Os campos dos binders são do tipo base genérico fechado (`[SerializeReference, TypePicker] UISource<BarModel>`); toda fonte concreta é não genérica (`AttributeBarSource : UISource<BarModel>`). Binders: base abstrata genérica (`UIBinder<TModel> : MonoBehaviour`) e um binder concreto não genérico por modelo. Listas usam `UIListSource<TItem>` e `UIListBinder<TItem>` com o mesmo ciclo.
3. **View.** `IView<TModel>` com `void Render(in TModel model)`, implementada por um `MonoBehaviour`. O binder guarda a view num campo `MonoBehaviour`, converte uma vez ao ligar e o validador acusa componente que não implementa a interface.
4. **Texto nos modelos sem alocar.** O modelo carrega `TextSpan`/`TextBuffer` (0038) que pertencem à ligação, e `string` só quando ela já existe (texto vindo do provedor). A cultura e o provedor chegam pelo `UITheme` do binder, no contexto da ligação.
5. **Dono.** `ActorResolver` (`[Serializable] sealed class`, não `[SerializeReference]`) com os modos `SelfOrParent`, `FromAnchor` e `Explicit`; o tipo procurado é o `OwnerType` da fonte. O Core ganha a base não genérica `RuntimeAnchor` (valor sem tipo, `ValueType`, evento `Changed`, `Provide`/`Unset` sem tipo), da qual o `RuntimeAnchor<T>` do CU1 passa a derivar, e o componente `AnchorRegistrar` (qualquer âncora, qualquer componente). O GAS ganha `AbilitySystemAnchor : RuntimeAnchor<AbilitySystemComponent>`. O binder com `FromAnchor` religa no `Changed`.
6. **Listas por chave.** A ligação de lista acrescenta, muda e remove itens por um identificador inteiro estável; a ordem é a de chegada. O binder de lista cria e devolve cada item pelo `SpawnerAsset` (0015), reaproveita o item quando só o conteúdo muda, aplica `MaxVisible` e entrega a contagem de ocultos a uma view própria. Um componente `UIItemContext` no item guarda o dono (escrito pelo binder) e o assunto (escrito pela view), para o tooltip e outros consumidores do item.
7. **Prévia em edição.** Binders são `[ExecuteAlways]`: fora do Play, desenham `CreatePreview(UIPreviewContext)` da fonte e não ligam nada; no Play, a prévia não roda. O Inspector do binder pede o redesenho quando a configuração muda. Nenhum código de runtime usa `UnityEditor`.
8. **uGUI declarada.** O `package.json` declara `"com.unity.ugui": "2.0.0"`. `SpellBook.UI.UGUI` referencia `SpellBook.Core`, `SpellBook.Time`, `SpellBook.UI`, `UnityEngine.UI` e `Unity.TextMeshPro`. `SpellBook.UI` e `SpellBook.GAS.UI` continuam sem uGUI e sem TextMeshPro.
9. **Asmdefs de Editor** (item 2 da 0012, sem asmdef fora do mapa): `SpellBook.UI.Editor` (Core, Time, UI, `Core.Editor`), `SpellBook.UI.UGUI.Editor` (Core, Time, UI, UI.UGUI, `Core.Editor`, `UnityEngine.UI`, `Unity.TextMeshPro`) e `SpellBook.GAS.UI.Editor` (Core, Tags, Time, GAS, UI, GAS.UI, `Core.Editor`).

## Motivo

- Uma ligação por binder, reaproveitada, cumpre "sem alocar ao atualizar" e "estado fora da fonte" de uma vez.
- O tipo do campo filtra as fontes no seletor, como as condições (0032), sem atributo extra.
- A âncora não genérica deixa a UI achar qualquer dono sem conhecer o GAS, e um registrador só serve a qualquer ator.
- Um mecanismo de lista único atende a barra de habilidades (B-016) e a lista de efeitos (B-018).
- A uGUI é embutida na Unity 6 e não tem custo para quem não usa os widgets; a 0012 já dizia que ela "precisa ser declarada".

## Consequências

- O CU1 não muda: o ticket UI1 do B-015 acrescenta a base ao `RuntimeAnchor.cs` depois dele, sem quebra.
- A 0017 e a 0012 têm os "Em aberto" respondidos e ganham, ao ser aceita esta ADR, uma emenda de uma linha apontando para cá.
- Todo projeto que instala o SpellBook passa a ter `com.unity.ugui` (Q1 do gate).
- Os prefabs do pacote usam fonte nula no TextMeshPro (cai na fonte padrão do `TMP_Settings`); o projeto importa os TMP Essentials.

## O que os agentes NÃO podem fazer

- Guardar estado da ligação na fonte ou criar uma ligação por atualização.
- Fazer a view buscar componente, dono ou fonte; ela só recebe o modelo.
- Ler o tempo fora do `UIUpdateDriver` ou rodar `Update` por widget.
- Pôr uGUI, TextMeshPro ou `UnityEngine.EventSystems` em `SpellBook.UI` ou `SpellBook.GAS.UI`.
- Criar um segundo mecanismo de lista, de dono ou de prévia.

## Relacionadas

- 0012 (módulos), 0013 (costura), 0014 (tempo), 0015 (spawn), 0017 (direção da UI), 0026 (editor), 0032 (base genérica fechada no campo), 0037 (âncora do roteador de cues), 0038 (texto).
