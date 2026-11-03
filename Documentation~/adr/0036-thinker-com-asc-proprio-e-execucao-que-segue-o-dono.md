# 0036 — Thinker com ASC próprio e execução que segue o dono

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O B-014 (PRD `gas-thinkers-auras-e-projeteis.md`) cria o `GameplayThinker`, um objeto no mundo com eventos de ações. O B-013 (Cues) precisa de um cue contínuo que dure "enquanto o efeito ou a ativação durar". As duas coisas dependem do executor da 0034, que hoje (Spec do B-012) tem:

- execuções que vivem no ASC dono e avançam pelo `Tick` dele;
- donos lógicos: a ativação da habilidade e a instância de efeito;
- a regra do `Delayed`: a ativação só termina quando as execuções dela terminam, e cancelar o dono cancela as execuções dele;
- execução sem dono (o `Delayed` com "continuar").

Os pedidos que o executor ainda não atende:

1. **Thinker sem criador:** um thinker colocado na cena (PRD P2) não tem ASC que o avance.
2. **Thinker que sobrevive ao criador:** sem `FollowOwner` e sem `EndWithExecution`, o thinker vive só pela duração (R6). Se as execuções dele morassem no ASC de quem o criou, morreriam com esse ASC.
3. **Ligado ao dono, mas sem segurá-lo:** um thinker com `EndWithExecution` e um cue contínuo devem acabar quando o efeito sai ou quando a ativação termina. Isso não pode manter a ativação viva, porque uma bola de fogo instantânea ficaria "ativa" enquanto a zona existe.

## Decisão

1. **Todo thinker tem um `AbilitySystemComponent` no mesmo objeto** (`[RequireComponent]`), que é o dono das execuções dele. Os eventos do thinker (`OnCreated`, `OnPulse`, `OnEnded`) rodam no runner desse ASC, com dono lógico = o thinker. O tempo é o do ASC do thinker:
   - na criação por `CreateThinkerAction`, o ASC do thinker recebe a fonte de tempo do criador (`SetTimeSource(criador.TimeSource)`);
   - colocado na cena, usa a fonte configurada no ASC dele.
2. **O criador é o instigador, não o host.** O `ActionContext` das execuções do thinker tem `Owner` = o ASC do thinker e `Instigator` = o criador (pode ser nulo ou destruído). Habilidade, spec, nível e `EffectContext` são copiados do contexto de criação. Efeitos que a aura aplica têm o criador como instigador.
3. **A vida do thinker é uma execução.** Uma ação interna do GAS (`ThinkerLifeAction`) roda no ASC do thinker e conta pulsos e duração com `ActionStatus.Wait` (cursor lógico da 0034, item 3). Por isso a regra do B-030 vale para o `PulseInterval` sem código à parte. Cada pulso roda `OnPulse` como execução própria do thinker: as esperas de um pulso não atrasam o seguinte.
4. **Vínculo de execução (`ExecutionLink`).** `RunActions` ganha um parâmetro:
   - `Holds` (padrão, comportamento do B-012): o dono espera a execução terminar e a cancela ao ser cancelado;
   - `FollowsOwner`: a execução é cancelada quando o dono **termina ou é cancelado** (fim de ativação, de canalização, toggle desligado, remoção da instância de efeito, por qualquer motivo), e o dono **não espera** por ela.

   O ASC cancela as execuções `FollowsOwner` de uma ativação no ponto único de fim (`EndAbility`), antes de `OnAbilityEnded`. As vigias usam `ActionStatus.WaitUntilCancelled` (novo), uma espera sem tempo que só termina por cancelamento.
5. **Quem usa o vínculo `FollowsOwner`:**
   - a `CreateThinkerAction` com `EndWithExecution`: roda no ASC do criador uma execução de vigia, cujo `OnCancel` termina o thinker com `ExecutionEnded`;
   - a `AddCueAction` do B-013: o `OnCancel` da vigia remove o cue.

   Nenhum outro mecanismo de "avisar quando o dono acaba" entra no GAS.
6. **Criador que some.** Com `FollowOwner` ou `EndWithExecution`, o thinker confere a cada quadro (no mesmo `LateUpdate` que copia a posição, só lendo o transform e a flag de ativo, sem alocar) se o criador continua ativo, e termina com `OwnerGone` quando não continua. A vigia de `EndWithExecution` também é cancelada no `OnDisable` do ASC do criador (`CancelAllAbilities`, que já existe): os dois caminhos levam ao mesmo término, que só acontece uma vez.

## Motivo

- Reaproveita o ASC e o executor inteiros. Não há segundo relógio nem segundo runner, e as ações do thinker são as mesmas da habilidade (0018).
- O thinker na cena, o thinker que sobrevive ao criador e o thinker ligado ao efeito saem do mesmo modelo.
- `FollowsOwner` é uma regra do executor, que fica num lugar só. O thinker e o cue não precisam de inscrição própria em eventos de fim.

## Consequências

- O prefab de thinker leva um ASC (sem `AttributeSet` obrigatório). O validador acusa o prefab sem ASC, e o `[RequireComponent]` o acrescenta ao criar.
- O ASC do thinker volta ao pool junto com o objeto. O `OnDisable` dele já cancela as execuções.
- Em habilidade `Instant`, `EndWithExecution` termina o thinker quando a ativação termina. Para uma zona que sobrevive ao lançamento, o designer desliga `EndWithExecution`, e o validador avisa nesse caso (pergunta ao usuário na Spec do B-014).
- O ASC do criador ganha execuções `FollowsOwner`, que não aparecem como "segurando" a ativação no indicador de "rodando" do EV22.

## O que os agentes NÃO podem fazer

- Contar a duração ou os pulsos do thinker por `Update` próprio, corrotina ou relógio da Unity. O `LateUpdate` do item 6 só segue o criador.
- Rodar as ações do thinker no ASC do criador.
- Criar outro jeito de ligar uma execução ao fim do dono além do `ExecutionLink`.

## Em aberto

Nada.

## Relacionadas

- 0013 (estado fora da configuração), 0014 (tempo), 0015 (spawner), 0018 e 0034 (ações e executor), 0035 (filtro da aura), 0037 (cue contínuo), Specs `gas-thinkers-auras-e-projeteis.md` e `cues.md`.
