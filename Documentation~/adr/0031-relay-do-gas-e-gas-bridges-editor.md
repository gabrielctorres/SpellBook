# 0031 — Forma do relay do GAS e o asmdef `SpellBook.GAS.Bridges.Editor`

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

A 0016 (item 6) decidiu que a ponte do GAS para os canais é "um relay opcional por ator em `SpellBook.GAS.Bridges`". O B-004 (PRD `gas-resultado-de-aplicacao-e-relay.md`) detalha o relay e pede ao Arquiteto (P6, P7, P5):

- a estrutura dos pares "evento → canal + `UnityEvent`";
- se o relay dispara dentro do evento C# do ASC ou enfileira;
- como as ações passam a enviar dados do GAS no payload sem que o módulo Events conheça o GAS.

O relay precisa de um Inspector em UI Toolkit herdando do `SpellBookEditor` (0026), e a guarda `SpellBookEditorCoverageTests` cobra isso de todo `MonoBehaviour` público do `SpellBook.GAS.Bridges`. Hoje não existe asmdef de Editor para o Bridges. O `SpellBook.GAS.Editor` não pode referenciar Events nem Bridges (`AsmdefRulesTests.cs:290-301`), e um asmdef de Editor de módulo não pode referenciar o Editor de outro módulo (`:304-327`). A 0012 proíbe criar asmdef sem ADR.

## Decisão

1. **Asmdef novo `SpellBook.GAS.Bridges.Editor`**, em `Editor/GAS.Bridges/`, `autoReferenced: true`, `includePlatforms: ["Editor"]`. Referencia: Core, Tags, Time, GAS, Events, GAS.Bridges e Core.Editor. **Não** referencia `SpellBook.GAS.Editor` nem `SpellBook.Events.Editor`. Os drawers de `EventEmission` e de canal do Events.Editor continuam valendo nos campos do Bridges, porque a Unity os aplica pelo tipo do campo, sem referência de assembly. O `AsmdefRulesTests` ganha o módulo na lista de Editor e um teste com as referências exatas.
2. **Pares fixos, não lista.** O relay tem um campo por evento repassado, agrupado por categoria (efeito, habilidade, atributo, tag, bloqueio). Cada par é um `RelayPair<T>` serializável com:
   - o canal tipado do payload do GAS (`EventChannel<T>`);
   - um canal de valor simples opcional (`EventChannel` sem payload, `Float`, `String` ou `GameObject`), com o valor tirado do payload por uma regra fixa por evento, escrita na Spec;
   - o `UnityEvent<T>` local.
   Motivo: serialização nativa estável, sem `[SerializeReference]` nem seletor de tipo, validação simples por campo e nenhum custo por evento sem configuração.
3. **Disparo síncrono, dentro do evento C# do ASC.** Sem fila e sem estado de gameplay no relay. A reentrada é segura porque os eventos C# do ASC são delegates imutáveis durante a chamada e os canais já iteram com segurança (0016, item 2).
4. **Payload dinâmico só no Bridges.** A `RaiseEventAction` e a `RaiseEventEffectAction` guardam a mesma `EventEmission` de hoje, mais um campo `GasPayloadSource` (enum do Bridges) e, quando preciso, o atributo ou o índice do modificador. Com a fonte `Fixed`, a ação chama `EventEmission.Raise`, como hoje. Com outra fonte, a ação lê o canal da emissão (`EventEmission.Channel`) e dispara o `EventChannel<T>` com o valor calculado. O módulo Events não muda.

## Motivo

- O asmdef próprio é o único jeito de dar ao relay um Inspector UI Toolkit sem quebrar as regras de dependência da 0012 e as guardas de asmdef.
- Pares fixos deixam o Inspector e a validação diretos e mantêm o custo zero de evento sem par ligado.
- Disparar dentro do evento mantém a ordem de causa e efeito que o designer espera (o som de "imune" toca no mesmo quadro do bloqueio).

## Consequências

- Um evento novo do ASC (por exemplo, `OnDepleted` do B-005) entra no relay como mais um campo, de forma aditiva.
- O payload dinâmico tem uma lista fechada de fontes por tipo de canal. Um tipo de canal fora da lista só aceita payload fixo, e o validador acusa a combinação inválida.
- O Inspector do relay não reaproveita o seletor filtrado do Events.Editor: o filtro vem do tipo do campo (`EventChannel<T>`) e, no canal de valor simples, do validador.

## O que os agentes NÃO podem fazer

- Fazer o `SpellBook.GAS.Bridges.Editor` referenciar outro asmdef de Editor de módulo.
- Enfileirar disparos do relay para outro quadro, ou guardar estado de gameplay nele.
- Fazer o `SpellBook.Events` conhecer tipos do GAS para o payload dinâmico.

## Emenda (2026-11-01): sem `RaiseEventEffectAction`

Pendente de gate parcial. O item 4 deixa de citar a `RaiseEventEffectAction`: ela não é criada. A `EffectAction` de hoje não recebe o efeito, o `EffectContext` nem o evento (`AbilitySystemComponent.cs:995`, `EffectAction.cs:16`), e o B-012 já apaga a `EffectAction` e funde a ação de efeito na `RaiseEventAction` (0034, item 4). O payload dinâmico fica numa ação só, a `RaiseEventAction`: o lado da habilidade no RL6, o lado do efeito no EV15, lido do `ActionContext` (asset do efeito, `EffectContext`, instância e evento). O resto da ADR não muda.

## Relacionadas

- 0012 (mapa de módulos), 0013 (`[SerializeReference]`), 0016 (eventos), 0017 (a UI não lê relay), 0026 (Inspector e validação).
