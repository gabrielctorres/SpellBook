# 0011 — Odin obrigatório: só atributos no runtime, serialização nativa

Status: Substituída por 0026
Data: 2026-10-29

## Contexto

O runtime já usa `Sirenix.OdinInspector` em 7 arquivos (por exemplo `Runtime/GAS/Abilities/GameplayAbility.cs`, `Runtime/GAS/Effects/GameplayEffect.cs`, `Runtime/GAS/Attributes/AttributeDefinition.cs`), mas nem o `package.json` nem o `Runtime/SpellBook.Core.asmdef` declaram a dependência. O Odin vem da Asset Store e não pode entrar em `dependencies` do `package.json`. O projeto host local não tem o Odin instalado.

O Inspector do pacote depende de escolher tipos `[SerializeReference]` (0013), e o seletor do Odin resolve isso. A decisão I.3 do usuário torna o Odin obrigatório, com serialização nativa da Unity.

## Decisão

1. O Odin Inspector é **pré-requisito** do SpellBook, declarado no `README.md` do pacote e na página de instalação do manual.
2. **Runtime:**
   - usa só os atributos do Odin (namespace `Sirenix.OdinInspector`, assembly de atributos);
   - a serialização é a nativa da Unity (`[SerializeField]`, `[SerializeReference]`);
   - **proibidos:** `SerializedScriptableObject`, `SerializedMonoBehaviour`, `Sirenix.Serialization`, `[OdinSerialize]` e qualquer tipo do Odin na assinatura da API pública.
3. **Editor:** Odin livre (drawers, `OdinEditorWindow`, menus, validação).
4. **Validação de asset:** `ISelfValidator` do Odin (0007).
5. Um asmdef `SpellBook.Setup.Editor`, sem nenhuma referência ao Odin, detecta a ausência dele e mostra um erro claro com o link da instalação.

## Motivo

- Serialização nativa mantém os dados legíveis pela Unity: os assets continuam válidos mesmo se o Odin for removido, e não há custo de serialização do Odin em runtime.
- O Odin dá Inspector rico e seletor de tipos sem o pacote escrever drawers próprios.

## Consequências

- Todo projeto que usa o SpellBook precisa de uma licença do Odin.
- O projeto host de testes e qualquer CI precisam do Odin instalado (0004).
- Recursos do Inspector que dependem de serialização do Odin (dicionários, tipos polimórficos fora de `[SerializeReference]`) não estão disponíveis no runtime.

## O que os agentes NÃO podem fazer

- Usar `SerializedScriptableObject`, `SerializedMonoBehaviour`, `Sirenix.Serialization` ou `[OdinSerialize]` em qualquer asmdef de runtime.
- Expor tipos do Odin na API pública do runtime.
- Guardar estado que só o Odin serializa, contando com `[ShowInInspector]` para persistir dado.
- Referenciar assemblies de editor do Odin num asmdef de runtime.

## Decisões incorporadas (2026-10-29)

- **`defineConstraints: ["ODIN_INSPECTOR"]`** (decisão I25) em **todos** os asmdefs do pacote, exceto `SpellBook.Setup.Editor` (runtime, Editor, testes e `SpellBook.Testing`). Motivo de ir além de "os que usam Odin": todo asmdef do pacote depende do Core, direta ou indiretamente, e um módulo ligado sem o resto ainda mostraria erros. Um teste EditMode cobra a regra (CA53).
- **Símbolo verificado:** num projeto local com Odin 3.3.x instalado (fora deste repositório), o `ProjectSettings.asset` tem `ODIN_INSPECTOR`, `ODIN_INSPECTOR_3`, `ODIN_INSPECTOR_3_1`, `ODIN_INSPECTOR_3_2` e `ODIN_INSPECTOR_3_3` nos scripting define symbols, escritos pelo próprio Odin ao ser importado. Consequência: num projeto novo, a primeira compilação acontece sem o símbolo; o Odin o grava e a Unity recompila com os assemblies do SpellBook ligados. O CI precisa contar com essa segunda compilação (ver riscos na Spec da 0-M).
- **`ISelfValidator` verificado:** está no namespace `Sirenix.OdinInspector`, no assembly `Sirenix.OdinInspector.Attributes` (documentado no `Sirenix.OdinInspector.Attributes.xml` do Odin 3.3.x), então é acessível a partir do runtime. O primeiro ticket que implementa um validador confirma no red que `SelfValidationResult` pode ser criado e lido fora do Inspector (o runner do menu precisa disso); se não puder, o plano B está na Spec.
- **Referências de runtime** (decisão técnica): os asmdefs de runtime usam `overrideReferences: true` com `precompiledReferences: ["Sirenix.OdinInspector.Attributes.dll"]`. Assim o compilador, e não só o teste CA3, impede o uso de `Sirenix.Serialization` e do Editor do Odin no runtime. Os asmdefs de Editor ficam com `overrideReferences: false` (Odin livre).
- **Setup sem Odin:** `SpellBook.Setup.Editor` não referencia nenhum assembly (nem o Core, que fica desligado sem Odin). Ele usa `#if !ODIN_INSPECTOR`, mostra o diálogo uma vez por sessão (`SessionState`), não mostra diálogo em batchmode (`Application.isBatchMode`), escreve o erro no console e monta o link a partir do `documentationUrl` do `package.json` (0007).
- **Versão mínima do Odin** (decisão técnica, **Inferido**): **3.3**, a versão em que o símbolo e o `ISelfValidator` foram verificados. O README e a página de instalação dizem "testado com Odin 3.3.x". Baixar a mínima exige testar numa versão anterior.
- **Limite conhecido:** ao remover o Odin, o símbolo `ODIN_INSPECTOR` pode ficar nos Player Settings, e então os asmdefs do SpellBook ficam ligados e mostram erros. A página de instalação explica como apagar o símbolo.

## Em aberto

Nada.

## Relacionadas

- 0004 (projeto host), 0007 (validadores), 0012 (asmdefs), 0013 (`[SerializeReference]`).
