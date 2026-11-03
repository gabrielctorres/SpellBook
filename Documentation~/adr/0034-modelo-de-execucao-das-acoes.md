# 0034 — Modelo de execução das ações: executor por quadros de pilha no `Tick` do ASC, relógio lógico e dois contextos; respostas aos "Em aberto" da 0018

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O B-012 (PRD `gas-evento-acoes-alvos.md`, DS `inspector-de-acoes.md`, Spec `gas-evento-acoes-alvos.md`) entrega a hierarquia única da 0018. Hoje:

- `AbilityAction.Execute` devolve `IEnumerator` e roda em `StartCoroutine` no ASC (`AbilitySystemComponent.cs:275`) e no impacto do projétil (`Projectile.cs:324`). Cada `Execute` aloca um iterador, e cada corrotina aninhada que termina custa um quadro ao pai (B-030).
- `EffectAction.Execute` é síncrona (`EffectAction.cs:16`) e não pode durar.
- `AbilityContext.WaitSeconds` (`AbilityContext.cs:52-67`) mede a espera a partir do quadro em que a corrotina retoma, então um `Tick` no quadro em que o passo anterior terminou não conta.
- O PRD exige 0 byte por execução em regime (C22), esperas pelo `ITimeSource` (0014) e a regra do B-030.

## Decisão

1. **Contrato da ação:** `GameplayAction` (classe base única, `[SerializeReference]`) com `protected internal abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame)` e `OnCancel(ActionContext, ref ActionFrame)`. A ação é dado; todo estado da execução fica no `ActionFrame` (`struct` com fase, índice, contador, prazo e uma referência livre) e no `ActionContext`. Não há `IEnumerator`.
2. **Executor próprio, avançado pelo `Tick` do ASC.** O ASC guarda as execuções em curso (pilha de quadros por execução, em listas reutilizadas e execuções em pool). `ActionStatus` diz ao executor o próximo passo: terminou, esperar N segundos, rodar uma lista filha, rodar filhas em paralelo. Ações sem espera rodam em sequência na mesma chamada (mesmo quadro). Nenhuma ação usa corrotina da Unity.
3. **Relógio lógico por execução (regra do B-030).** Cada execução tem um cursor de tempo que começa no `TickedTime` do ASC dono quando ela começa. Um passo instantâneo não move o cursor. Uma espera de `s` segundos termina quando `TickedTime ≥ cursor + s`, e então o cursor passa a `cursor + s` (o excesso do `Tick` é levado adiante). Assim, uma espera conta a partir do fim do passo anterior, um `Tick` no mesmo quadro conta, e o resultado não depende de como o tempo foi fatiado em `Tick`s.
4. **Dois contextos (fecha o "Tipo do contexto").** `ActionContext` (classe, em pool, mutável) substitui o `AbilityContext`: origem, dono, alvos do evento, ponto, habilidade, spec, nível, instância de efeito, cancelamento e dados livres. O `EffectContext` (0030) continua o dado imutável do efeito; o `ActionContext` o carrega por cópia quando a execução vem de um efeito. Confirma a recomendação da 0030, item 5.
5. **Eventos por dono são campos anotados.** Cada lista de ações de um dono é um campo `[SerializeReference] List<GameplayAction>` com `[ActionEvent]` (nome mostrado e alvo padrão). O Inspector descobre as abas pelo atributo; um dono novo (thinker, Fase 8) só declara campos. Lista fechada na 3.0 na Spec do B-012.
6. **Ações de efeito podem durar.** A execução disparada por um evento do efeito pertence à instância ativa; remover a instância cancela as execuções dela (`OnCancel` em ordem reversa), salvo as marcadas "continuar".
7. **`TargetSelector` substitui a `TargetingStrategy`.** O seletor por ação (`[SerializeReference]`) é o único mecanismo de escolha de alvo; a `TargetingAction` passa a usar um seletor para preencher os alvos do evento. `TargetingStrategy`, `CircleTargeting` e `MouseTargetingStrategy` saem (0009).
8. **Aleatoriedade injetável no Core.** `SpellBook.Core.IRandomSource` (com uma implementação determinística por semente). O ASC tem a sua fonte, trocável por código como o `ITimeSource`.
9. **Resumo de uma linha por modelo declarativo.** O resumo da linha no Inspector vem de um modelo no `[TypeCategory(..., Line = "...")]` (por exemplo, `"Esperar {Duration}"`), resolvido no Editor a partir do `SerializedProperty`. Não há método de resumo no runtime.

## Motivo

- Contrato por quadro de pilha não aloca por execução e é testável com `ManualTimeSource` sem esperar quadros.
- O cursor lógico garante a regra do B-030 para `Wait`, `Repeat.Interval` e `DelayBetweenShots` com o mesmo código, sem caso especial.
- Campos anotados mantêm a serialização nativa e o vocabulário único entre donos (DS, princípio 2).
- Seletor e estratégia ao mesmo tempo seriam dois mecanismos de alvo.

## Consequências

- Todas as ações são reescritas; ações próprias de projetos 2.x precisam ser portadas (CHANGELOG e página de migração).
- `Wait(0)` e `Repeat` com `Interval = 0` deixam de custar um quadro.
- As execuções vivem no ASC dono: o impacto de projétil roda no ASC de quem lançou, como hoje.
- A 0018 ganha, ao ser aceita esta ADR, uma emenda datada de uma linha apontando para cá.

## O que os agentes NÃO podem fazer

- Usar `IEnumerator`, `StartCoroutine`, `WaitForSeconds` ou relógio da Unity em ação.
- Guardar estado de execução em campo da ação ou do seletor.
- Manter `TargetingStrategy` ao lado do `TargetSelector`.
- Usar `UnityEngine.Random` direto em ação ou seletor.

## Emenda (2026-11-02): ponto de extensão `protected`

Status da emenda: Proposta (gate parcial da Spec `gas-evento-acoes-alvos.md`, emenda "ponto de extensão `protected`" do EV3).

**Contexto.** O item 1 declarou o `Execute` como `protected internal`, e o EV1b fez o mesmo com o `OnCancel`. No EV3, o primeiro override de fora do assembly do GAS (`RaiseEventAction`, em `SpellBook.GAS.Bridges`) quebrou a geração da referência da API. A Unity compila o Bridges à parte, sem os internals do GAS, e exige o override `protected`. O docfx compila o pacote como um assembly só e exige `protected internal`. Cada um dá CS0507 na forma do outro, e uma ação escrita pelo usuário em outro assembly cai no mesmo conflito.

**Decisão.** O item 1 passa a ser:

- `protected abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame)` e `protected virtual void OnCancel(ActionContext context, ref ActionFrame frame)` são o ponto de extensão, com o mesmo nome e a mesma forma para o pacote, o Bridges e o usuário.
- A base tem `internal ActionStatus InvokeExecute(...)` e `internal void InvokeCancel(...)`, não virtuais, que só repassam a chamada. O executor chama esses dois e mais nada.
- `UsesTargets`, `Validate` e `CollectEffects` continuam `public virtual`.
- Vale para o pacote inteiro: um membro que se sobrescreve de fora do assembly onde foi declarado é `public` ou `protected`, nunca `protected internal`. Quando o pacote precisa chamar um membro `protected`, a base expõe um método `internal` não virtual que repassa a chamada.

**Motivo.** `protected` sobre `protected` tem a mesma regra em qualquer compilação, junta ou separada, e não pede `InternalsVisibleTo`. Um `InternalsVisibleTo` para o Bridges resolveria o pacote e deixaria o usuário com a forma oposta. Mudar a geração deixaria o contrato preso ao modo de compilar de cada ferramenta. Manter os nomes poupa os overrides de teste, que já usam `protected override`.

**Consequências.** Nenhuma ação chama o `Execute` ou o `OnCancel` de outra ação. Listas filhas rodam por `ActionStatus.Run` e `RunParallel`, como o item 2 já previa. A API não foi lançada, e o `CHANGELOG.md` registra a forma `protected` na entrada da base nova. O `GasSourceScanTests` ganha a guarda `NoProtectedInternal_InActions`.

**O que os agentes NÃO podem fazer (acréscimo).** Declarar `protected internal` em membro sobrescrevível de `Runtime/**`. Resolver acesso entre assemblies do pacote com `InternalsVisibleTo` para um módulo de runtime.

## Em aberto

Nada.

## Relacionadas

- 0009 (quebras), 0013 (estado), 0014 (tempo), 0018 (hierarquia), 0019 e 0032 (condições), 0026 (Inspector e `TypeCategory`), 0030 (contextos).
