# 0026 — SpellBook sem Odin: editor próprio em UI Toolkit

Status: Aceita (gate 2026-10-29, revisão sem Odin)
Data: 2026-10-29
Substitui: 0011

## Contexto

A 0011 tornou o Odin Inspector obrigatório. Em 2026-10-29 o usuário mudou a decisão (plano, seção I):

- **I45** (substitui as decisões 3, 25 e 33): o SpellBook não depende do Odin a partir da 0-M. O pacote ganha:
  - um seletor de tipo para `[SerializeReference]`;
  - atributos próprios, criados só sob demanda;
  - uma interface própria de validação.

  A serialização continua nativa.
- **I46:** drawers e janelas em UI Toolkit.
- **I47:** nenhuma compatibilidade com Odin: sem adaptador e sem cenário de teste.
- **I48:** nenhuma exceção IMGUI. O pacote tem:
  - um Inspector base próprio (`SpellBookEditor`), em UI Toolkit, com o HelpBox de validação no topo e pontos de extensão para botões;
  - um `[CustomEditor]` de uma linha por tipo do SpellBook;
  - o seletor de tipo como popup próprio em UI Toolkit.

  As mensagens de validação aparecem no Inspector e na janela do validador.
- **I49:** as perdas no Inspector do GAS ficam aceitas até os épicos do GAS.
- **I50:** o canal fica dentro da `EventEmission`.

Uso atual do Odin. São 8 arquivos, todos em `Runtime/GAS`, e só atributos de `Sirenix.OdinInspector`:

| Uso | Onde | Quantidade |
|---|---|---|
| `[ShowIf]` por campo irmão (enum ou bool) | `ModifierValueSource.cs:19,22,25`, `AttributeDefinition.cs:30`, `GameplayEffect.cs:40`, `AbilityDescription.cs:61` | 6 |
| `[ShowIf("@Period > 0")]` (expressão) | `GameplayEffect.cs:55,66` | 2 |
| `[InfoBox]` | `GameplayAbility.cs:47`, `GameplayEffect.cs:44`, `ExpressionFormula.cs:37` | 3 |
| `[Required]` | `AttributeDefinition.cs:32`, `CommonEffectActions.cs:23`, `ExpressionFormula.cs:28`, `SimpleAttributeFormula.cs:17` | 4 |
| `[ReadOnly]` + `[ShowInInspector]` num campo não serializado (pré-visualização) | `ExpressionFormula.cs:48-53` | 1 |
| `[OnValueChanged]` | `ExpressionFormula.cs:40` | 1 |
| Layout: `TabGroup`, `BoxGroup`, `HorizontalGroup`, `VerticalGroup`, `FoldoutGroup`, `Title`, `HideLabel`, `LabelText`, `LabelWidth`, `PreviewField`, `InlineEditor`, `TableList`, `ListDrawerSettings`, `EnumPaging`, `GUIColor`, `Searchable` | os 8 arquivos | — |

Campos `[SerializeReference]` que hoje dependem do seletor do Odin para receber um tipo:

- `GameplayAbility.Actions`;
- 3 listas em `GameplayEffect`;
- `AbilityDescription.Elements`;
- 4 campos em `FlowActions.cs`;
- 1 campo em `SpawnProjectileAction.cs`.

O Inspector padrão da Unity mostra os campos de uma instância que já existe, mas não oferece um jeito de escolher o tipo concreto. Sem um seletor, nenhum desses campos pode receber uma instância nova pelo Inspector.

A 0-M planejava usar:

- `ISelfValidator` e `SelfValidationResult` (validadores);
- `[Button]` e `[EnableIf]` (botão Raise do canal e do emissor);
- `[FoldoutGroup]` (escape do ouvinte);
- `[HideReferenceObjectPicker]` e `[OnValueChanged]` (emissão do emissor);
- `InfoBox` no drawer de canal;
- `OdinEditorWindow` (Event Debugger e janela de validação).

## Decisão

### 1. Nenhuma dependência do Odin

- Nenhum asmdef do pacote referencia assembly do Odin, nem em `references`, `precompiledReferences`, `defineConstraints` ou `versionDefines`.
- Nenhum arquivo do pacote contém `Sirenix` ou `ODIN_INSPECTOR`. Isso vale para `Runtime/`, `Editor/`, `Tests/` e `package.json`.
- Não existe `SpellBook.Setup.Editor`, aviso de Odin ausente, adaptador, `#if ODIN_INSPECTOR`, cenário de teste com Odin nem instrução de instalação do Odin (I47).
- **Isolamento de DLLs** (decisão técnica):
  - todo asmdef do pacote usa `overrideReferences: true`;
  - nos asmdefs de runtime e de Editor, `precompiledReferences: []`;
  - nos de teste e no `SpellBook.Testing`, só `nunit.framework.dll`.

  Motivo: com `overrideReferences: false`, a Unity referencia automaticamente toda DLL pré-compilada do projeto do usuário. Num projeto com Odin, um `using Sirenix` compilaria. Além disso, nomes como `ShowIf` poderiam ficar ambíguos dentro do pacote.

### 2. Serialização

Continua nativa, com `[SerializeField]` e `[SerializeReference]`, sob as regras da 0013. Remover os atributos do Odin não muda o formato serializado: os assets existentes continuam válidos.

### 3. Tecnologia do editor (I46)

- **Drawers:** `PropertyDrawer.CreatePropertyGUI`.
- **Inspectors de tipo:** `Editor.CreateInspectorGUI`.
- **Janelas:** `EditorWindow.CreateGUI`.
- **Estilos:** USS opcional, na pasta do Editor do módulo.

**IMGUI é proibido, sem exceção (I48):** nada de `OnGUI`/`OnInspectorGUI` com lógica, `AdvancedDropdown`, `GenericMenu`, `Editor.finishedDefaultHeaderGUI` ou `EditorGUILayout`.

O único `OnGUI` permitido é o fallback obrigatório de um `PropertyDrawer`, que desenha uma linha só, com `EditorGUI.HelpBox`: "Este campo precisa do Inspector em UI Toolkit". A Unity chama esse método quando um Inspector IMGUI de terceiros desenha o campo. Não há implementação IMGUI paralela.

### 3a. Inspector base `SpellBookEditor` (I48)

```csharp
namespace SpellBook.Core.Editor
{
    public class SpellBookEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI();          // monta: validação, padrão, extras
        protected virtual void BuildBeforeDefault(VisualElement root) { }
        protected virtual void BuildAfterDefault(VisualElement root) { }  // botões do módulo
        protected void RefreshValidation();                          // recalcula o HelpBox
    }
}
```

**O que `CreateInspectorGUI` monta**, nesta ordem:
1. um contêiner de validação no topo, com um `HelpBox` por mensagem (erro ou aviso). Fica vazio, e escondido, quando o alvo não implementa `IValidatable` ou não tem mensagem;
2. `BuildBeforeDefault`;
3. o Inspector padrão, por `InspectorElement.FillDefaultInspector(root, serializedObject, this)`;
4. `BuildAfterDefault`.

**Quando a validação é recalculada:** ao abrir o Inspector e em `root.TrackSerializedObjectValue(serializedObject, ...)`. O botão "Revalidar" chama `RefreshValidation` sem depender de repaint. Com vários objetos selecionados, a validação roda em cada alvo e as mensagens aparecem com o nome do objeto.

**Um `[CustomEditor]` por tipo.** Todo `MonoBehaviour` e `ScriptableObject` público e concreto dos assemblies de runtime do SpellBook tem um `[CustomEditor]` que herda de `SpellBookEditor`:

- uma linha basta: `[CustomEditor(typeof(X))] sealed class XEditor : SpellBookEditor { }`;
- um tipo base pode cobrir as subclasses com `editorForChildClasses: true`;
- o editor fica no asmdef de Editor do módulo do tipo;
- os Inspectors com botão (canal e emissor) sobrescrevem `BuildAfterDefault`.

Um teste de cobertura falha se faltar o editor.

### 4. Seletor de tipo para `[SerializeReference]`

**Atributo de marcação.** `[TypePicker]` (`SpellBook.Core.Inspector`) marca um campo `[SerializeReference]`, simples ou lista. Como a Unity aplica drawers de atributo a cada elemento de uma lista, o seletor aparece por elemento. Campo sem `[TypePicker]` não ganha seletor. Por exemplo, a `EventEmission` é criada por código (0013) e não usa o seletor.

**Filtro por tipo base.** O tipo base é o tipo declarado do campo (lido de `managedReferenceFieldTypename`). O candidato:

- deriva do tipo base (por `TypeCache.GetTypesDerivedFrom`), ou é o próprio tipo base, se for concreto;
- não é abstrato nem interface;
- não é genérico, nem aberto nem fechado (0013: o designer nunca escolhe um genérico);
- não deriva de `UnityEngine.Object`;
- tem `[Serializable]`;
- tem construtor sem parâmetros.

Entram tipos de qualquer assembly carregado: o módulo dono, as pontes e o projeto do usuário. Essa regra fica numa função pura do Editor, testável sem UI.

**Categorias.** `[TypeCategory("Caminho/Sub", Summary = "resumo de uma linha")]` vai no tipo concreto:

- o caminho vira a hierarquia do dropdown;
- o nome exibido é o do tipo em `ObjectNames.NicifyVariableName`;
- o resumo aparece como dica e no cabeçalho do elemento;
- tipo sem `[TypeCategory]` aparece na raiz;
- a ordem é alfabética, primeiro as categorias e depois os tipos.

É o mesmo metadado que o catálogo da doc lê (0007, item 5; o nome `[ActionCategory]` da 0007 passa a ser `[TypeCategory]`).

**Busca.** Por nome exibido, nome do tipo e caminho da categoria, sem diferenciar maiúsculas e minúsculas.

**Popup próprio em UI Toolkit (I48).** O `TypePickerPopup` é um `EditorWindow` aberto por `ShowAsDropDown`, ancorado no botão do campo. Ele tem:

- `ToolbarSearchField` no topo, com foco ao abrir;
- sem busca, uma lista por nível de categoria (`ListView`), com navegação para dentro e para fora (cabeçalho com "voltar") e "(Nenhum)" no primeiro nível;
- com busca, uma lista plana dos tipos que casam, cada um com o caminho da categoria;
- o resumo do item selecionado no rodapé;
- teclado: setas, Enter escolhe, Esc fecha, Backspace com busca vazia volta um nível.

A árvore, a navegação e a busca ficam num modelo puro (`TypePickerTree`), testável sem janela.

**Operações:**

- "(Nenhum)" deixa o campo nulo.
- Escolher um tipo cria uma instância nova pelo construtor sem parâmetros e substitui a anterior. Os dados da anterior são descartados, com Undo.
- O elemento mostra um cabeçalho com o tipo e o resumo, e abaixo os campos da instância (um `PropertyField` por filho).

**Referência compartilhada.** Duplicar um elemento de lista no Inspector pode deixar dois elementos apontando para o mesmo `managedReferenceId`, o que a 0013 proíbe. Ao ligar o elemento, o drawer verifica se o id já aparece antes na mesma lista. Se aparecer, troca o elemento por uma cópia: instância nova do mesmo tipo, com os dados copiados por `EditorJsonUtility`.

**Tipo removido ou renomeado:**

- Quando o tipo de uma referência não existe mais, a Unity guarda os dados da referência no asset como "tipo ausente". A API fica em `UnityEditor.SerializationUtility`: `HasManagedReferencesWithMissingTypes`, `GetManagedReferencesWithMissingTypes` e `ClearManagedReferenceWithMissingType`. O comportamento é verificado no red do 0M-41.
- **Drawer:** num campo com tipo ausente, mostra um `HelpBox` de erro, "Tipo não encontrado: `<assembly>` `<namespace.Classe>`", com o botão "Limpar referência". **Nunca limpa sozinho.** Restaurar o tipo (por exemplo, voltando o código) recupera os dados.
- **Validador do projeto:** acusa como **erro** todo asset ou objeto de cena aberta que tenha uma referência com tipo ausente.
- **Renomear tipo do pacote:**
  - na 3.0, a quebra é aceita sem `[MovedFrom]` (0009), e o CHANGELOG lista o tipo;
  - depois da `3.0.0`, renomear exige `[MovedFrom]` (`UnityEngine.Scripting.APIUpdating`) e versão MAJOR (0008).
- **Renomear tipo do usuário:** o manual ensina a usar `[MovedFrom]`.

### 5. Atributos próprios

- **Onde:**
  - os atributos ficam em `Runtime/Core/Inspector/`, namespace `SpellBook.Core.Inspector`, um por arquivo;
  - os drawers, o `TypePickerPopup` e o `SpellBookEditor` ficam em `SpellBook.Core.Editor`, em `Editor/Core/Inspector/`;
  - o Inspector específico de um módulo (canal, emissor, Event Debugger) fica no asmdef de Editor do módulo, como Inspector ou drawer do tipo, sem atributo novo.
- **Regra de referência dos asmdefs de Editor:**
  - um `SpellBook.<Módulo>.Editor` **pode** referenciar o `SpellBook.Core.Editor` (para herdar o `SpellBookEditor` e abrir o `TypePickerPopup`), além do runtime do módulo e das dependências dele;
  - um `.Editor` não referencia o `.Editor` de outro módulo;
  - o `SpellBook.Core.Editor` referencia só o `SpellBook.Core`;
  - nenhum runtime referencia um `.Editor`.
- **Lista inicial (0-M):**
  - `[TypePicker]` e `[TypeCategory]` (item 4);
  - `[ShowIf(string field, object value)]`: mostra o campo só quando o campo **irmão** serializado `field`, `bool` ou `enum`, é igual a `value`. Não aceita expressão, propriedade ou método, e não se aplica a listas como um todo, porque a Unity aplica o drawer por elemento.
- **Não criados na 0-M, com o substituto de cada um:**

  | Odin | Substituto |
  |---|---|
  | `InfoBox` | `[Tooltip]` da Unity |
  | `Button`, `EnableIf` | Inspector UI Toolkit do tipo (botão Raise do canal e do emissor) |
  | `ReadOnly`, `ShowInInspector` | Inspector do tipo, ou nada |
  | `Required` | Regra em `IValidatable`, onde a Spec pedir |
  | `OnValueChanged` | `OnValidate` ou o drawer do tipo |
  | `HideReferenceObjectPicker` | Drawer do tipo |
  | `Title` | `[Header]` |
  | Grupos, abas, tabela, pré-visualização, `InlineEditor`, rótulos | `[Header]`, `[Space]`, `[TextArea]`, `[Tooltip]` nativos, ou nada |
  | `ShowIf` com expressão | Campo sempre visível, com `[Tooltip]` explicando quando vale |

- **Critério "só sob demanda".** Um atributo novo no Core só nasce quando valem as três condições:
  1. um ticket de Spec aprovada precisa dele;
  2. nenhum atributo nativo da Unity (`[Header]`, `[Tooltip]`, `[Space]`, `[TextArea]`, `[Range]`, `[Min]`, `[Multiline]`, `[HideInInspector]`) resolve;
  3. a mesma necessidade aparece em pelo menos dois tipos de módulos diferentes. Se não aparecer, a solução é um Inspector ou drawer do tipo, no Editor do módulo.

  O ticket que cria o atributo cita esta ADR, traz teste do comportamento e entrega o drawer em UI Toolkit.
- **Limite conhecido:** a Unity usa um único `PropertyDrawer` por campo, então um campo não combina `[TypePicker]` com `[ShowIf]`. Quando isso for preciso, a solução vem por emenda a esta ADR.

### 6. Validação

```csharp
namespace SpellBook.Core.Validation
{
    public enum ValidationSeverity { Warning, Error }

    public readonly struct ValidationMessage
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
        public string PropertyPath { get; }   // opcional; null quando não se aplica
    }

    public sealed class ValidationResult
    {
        public ValidationResult(UnityEngine.Object context);
        public UnityEngine.Object Context { get; }
        public IReadOnlyList<ValidationMessage> Messages { get; }
        public bool HasErrors { get; }
        public void AddError(string message, string propertyPath = null);
        public void AddWarning(string message, string propertyPath = null);
    }

    public interface IValidatable
    {
        void Validate(ValidationResult result);
    }
}
```

- **Quem implementa:**
  - `MonoBehaviour` e `ScriptableObject` do pacote que têm regra de validação;
  - tipos `[SerializeReference]` com regra (`Reaction`, `EventResponse`, `AbilityAction`). Esses também implementam `IValidatable`, e o dono (`EventListener`, `GameplayAbility`) repassa a chamada para cada item da lista;
  - um tipo que precisa de contexto extra recebe uma sobrecarga própria, por exemplo `Reaction.Validate(ValidationResult, Type payloadType)`.
- **Regras do `Validate`:**
  - não muda estado;
  - não escreve no console;
  - não acessa `UnityEditor`, porque o tipo está no runtime.

  As mensagens são em português, curtas, e dizem o que corrigir.
- **Validador do projeto** (`SpellBook.Core.Editor`, `ProjectValidator`):
  1. reúne os alvos: assets de `ScriptableObject`, componentes de prefabs e componentes das cenas abertas;
  2. para cada `IValidatable`, cria um `ValidationResult(alvo)` e chama `Validate`;
  3. acrescenta o erro de tipo ausente (item 4) e a regra de `[HelpURL]` por reflexão (0007).

  Uma exceção dentro de `Validate` vira uma mensagem de erro com o tipo e a mensagem da exceção, e não interrompe a varredura. O menu `SpellBook/Validar projeto` abre a janela em UI Toolkit, e o teste EditMode (0004) usa o mesmo `ProjectValidator`.
- **Inspector (I48):** as mensagens aparecem no topo do Inspector do próprio objeto, pelo `SpellBookEditor` (item 3a). No caso de um componente, aparecem no Inspector daquele componente. O recálculo acontece ao abrir e quando o `SerializedObject` muda, nunca a cada repaint. As mesmas mensagens aparecem na janela `SpellBook/Validar projeto`.

### 7. Regra verificada por teste

O teste de estrutura (`Tests/Core/EditMode/Structure/NoOdinTests.cs`, ticket 0M-07) varre:

- todo `.asmdef`, em `references`, `precompiledReferences`, `defineConstraints` e `versionDefines`;
- todo `.cs`, `.asmdef`, `.asmref`, `.uxml` e `.uss` de `Runtime/`, `Editor/` e `Tests/`;
- o `package.json`.

Ele falha se encontrar `Sirenix` ou `ODIN_INSPECTOR`. O `AsmdefRulesTests` cobra o item 1: `overrideReferences: true` e `precompiledReferences` dentro da lista permitida.

## Motivo

- Quem instala o SpellBook não precisa de licença paga de terceiros.
- O CI roda sem baixar nada privado. A P21 (licença do Odin no CI) deixa de existir, e o 0M-11 é desbloqueado.
- O Inspector padrão da Unity já usa UI Toolkit, então o pacote usa uma tecnologia só e a mesma da Unity.
- A serialização não muda: os assets continuam válidos, e a troca de editor não tem custo de migração de dados.
- O seletor e o validador próprios aplicam as regras da 0013 (sem genéricos, uma instância por entrada) e da 0007 (categoria e resumo para o catálogo), em vez de depender do comportamento de outra ferramenta.

## Consequências

- O pacote mantém o próprio seletor, o popup, o Inspector base, os drawers e as janelas. Isso dá dez tickets novos na 0-M (0M-41 a 0M-50).
- Cada tipo público concreto ganha um `[CustomEditor]` de uma linha. Os módulos Time, Tags, Pooling e GAS ganham um asmdef `.Editor` cada (emenda 0012).
- Se o usuário tiver um Inspector próprio para um tipo do SpellBook, o dele concorre com o `SpellBookEditor` e a Unity escolhe um. Para manter a validação no topo, o Inspector do usuário deve herdar de `SpellBookEditor`; o manual explica.
- **Perdas no Inspector do GAS, aceitas pela I49 até os épicos do GAS** (sem dado perdido):
  - abas e grupos;
  - edição inline do efeito de custo, do efeito de recarga e da fórmula (`InlineEditor`);
  - pré-visualização da `ExpressionFormula`;
  - destaque de `[Required]`;
  - tabela de mapeamentos;
  - miniatura do ícone;
  - busca na lista de ações;
  - `ShowIf` por expressão (`Period > 0`).

  Os textos de `InfoBox` viram `[Tooltip]`.
- Um Inspector IMGUI do usuário que desenhe um campo do SpellBook por `EditorGUILayout.PropertyField` mostra o `HelpBox` do fallback no lugar do seletor.
- Num projeto que tenha o Odin, o Odin pode desenhar os tipos do SpellBook com o Inspector dele e ignorar os atributos do SpellBook. Isso não tem suporte (I47).
- Em código do usuário com `using Sirenix.OdinInspector;` e `using SpellBook.Core.Inspector;` juntos, `ShowIf` fica ambíguo. O manual mostra como resolver com nome qualificado ou alias.
- A 0011 deixa de valer inteira, incluindo as decisões incorporadas:
  - `defineConstraints: ["ODIN_INSPECTOR"]`;
  - `SpellBook.Setup.Editor`;
  - DLL de atributos;
  - versão mínima 3.3;
  - plano B do `SelfValidationResult`.
- As ADRs 0004, 0007, 0010, 0012, 0013, 0016 e 0025 recebem emenda datada que aponta para esta.

## O que os agentes NÃO podem fazer

- Escrever `using Sirenix`, `Sirenix.` ou `ODIN_INSPECTOR` em qualquer arquivo ou asmdef do pacote, inclusive em testes.
- Criar adaptador, `#if ODIN_INSPECTOR`, cenário de teste ou instrução de instalação para o Odin.
- Pôr `overrideReferences: false` num asmdef do pacote, ou incluir DLL pré-compilada fora da lista do item 1.
- Escrever drawer, Inspector ou janela em IMGUI, ou usar `AdvancedDropdown`, `GenericMenu` ou `Editor.finishedDefaultHeaderGUI`. O único `OnGUI` aceito é o fallback de uma linha do item 3.
- Criar `MonoBehaviour` ou `ScriptableObject` público concreto sem `[CustomEditor]` derivado de `SpellBookEditor`.
- Fazer um `.Editor` de módulo referenciar o `.Editor` de outro módulo que não seja o `SpellBook.Core.Editor`.
- Criar atributo no Core fora do critério do item 5, ou atributo que receba expressão em string.
- Oferecer tipo genérico, abstrato ou derivado de `UnityEngine.Object` no seletor de tipo.
- Limpar automaticamente dados de referência com tipo ausente.
- Validar no Inspector (cor, aviso visual) em vez de `IValidatable`, ou escrever no console dentro de `Validate`.
- Desligar ou pular o `NoOdinTests` para um ticket passar.

## Emenda (2026-10-29): tipo ausente relatado por objeto, D6 da Spec

Decisão do usuário (D6 da `spec/modularizacao.md`), achado no red do 0M-41. Esta emenda substitui o trecho "Drawer" do item 4 sobre tipo ausente.

- A Unity deixa o campo com tipo ausente com `managedReferenceId = -2`, igual a um campo nulo, e nenhuma API pública liga o campo ao `referenceId` do tipo ausente.
- Por isso o aviso não fica no drawer. O `SpellBookEditor` mostra no topo do Inspector um `HelpBox` de erro por tipo ausente do objeto, "Tipo não encontrado: `<assembly>` `<namespace.Classe>`", com o botão "Limpar referência" (com confirmação), que age pelo `referenceId`.
- **Nunca limpa sozinho.** O campo aparece como "(Nenhum)". Restaurar o tipo recupera os dados.
- Um tipo do usuário sem `SpellBookEditor` só vê o erro no validador do projeto (item 4, validador; CA64).
- O tipo ausente é implementado no 0M-53, não no 0M-41.

## Em aberto

- Nada além da aceitação desta ADR. As três questões da versão anterior foram respondidas pelas decisões I48 (popup e Inspector base em UI Toolkit, sem IMGUI), I49 (perdas aceitas) e I50 (canal dentro da `EventEmission`).

## Relacionadas

- Substitui a 0011.
- 0004 (host e CI sem Odin), 0007 (validadores e catálogo), 0009 (renomes na 3.0), 0010 (APIs da 6000.0), 0012 (asmdefs), 0013 (`[SerializeReference]`), 0016 (Inspector do Events), 0025 (CI sem Odin).
