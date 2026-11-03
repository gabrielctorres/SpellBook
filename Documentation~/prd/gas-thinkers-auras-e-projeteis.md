# PRD: Thinkers, auras e projéteis
Backlog: B-014 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção B ("Thinkers e auras"), seção C (item 14 do relatório: `Projectile.HandleCollision`), seção E (Fase 8) e decisão I.8. ADRs [0018](../adr/0018-hierarquia-unica-de-acoes.md) (hierarquia única de ações; o thinker e o projétil são donos de eventos), [0015](../adr/0015-spawnerasset-como-seam-de-spawn-e-pool.md) (`SpawnerAsset`; thinkers criados por spawner), [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (módulos; filtros de time em Bridges), [0013](../adr/0013-serializereference-como-costura-entre-modulos.md) (costura e estado) e [0014](../adr/0014-tempo-injetavel-itimesource.md) (tempo).
Depende de: **B-012** (a base de ação única, os eventos nomeados por dono, o `TargetSelector`, `Delayed`, `ForEachTarget`, o contexto de execução e o modelo de execução sem corrotinas), **B-003** (lista e remoção de efeitos ativos, eventos de efeito), **B-007** (`EffectContext`, `ScalableFloat` e `%nome` para raio, intervalo e duração por nível), **B-009** (hierarquia `Condition` e `GameplayTagQuery`, usados como filtros) e **B-002** (correções do núcleo). A relação de time (aliado, inimigo) entra **só** pelo **B-011** (Teams) em `SpellBook.GAS.Bridges`, e os cues por tag, pelo **B-013** (Cues). Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
Este PRD não tem foco em nenhum jogo. A menção a uma "GroundZone" do jogo no plano é histórico.

---

## 1. Problema (contexto)

O pacote não tem "algo que fica no mundo e age por um tempo", e o projétil é fechado:

- **Sem thinker.** Uma zona de fogo no chão, uma torre, uma bomba-relógio, um totem: "objeto no mundo, com tempo de vida, que a cada intervalo faz algo" não existe. O jogo reimplementou como `GroundZone`.
- **Sem aura.** "Enquanto estiver dentro do raio, aplica este efeito; ao sair, tira" é código do jogo, que guarda a lista de quem está dentro e se esquece de remover o efeito quando o dono morre ou a zona expira.
- **O projétil acerta o primeiro e some.** `Projectile.HandleImpact` volta ao pool a cada impacto: não há perfuração (atravessar N alvos), nem "só acerta cada alvo uma vez".
- **Sem evento de fim.** O projétil que expira por tempo (`Lifetime`) volta ao pool **sem rodar nenhuma ação**: "explodir no fim do trajeto" exige código.
- **Sem filtro de relação.** O projétil acerta aliado, inimigo e neutro igual (só `DamageOwner` filtra o próprio dono). Não existe "não acerta aliados".
- **Acerto por componente no próprio objeto.** `Projectile.HandleCollision` usa `GetComponent<AbilitySystemComponent>`: um alvo cujo ASC está num **pai** do colisor (o caso comum de personagem com colisor em um filho) **não é acertado**, enquanto o targeting usa `GetComponentInParent`.
- **Criação sem contrato de pool.** O que cria e devolve objetos de gameplay tem de usar o `SpawnerAsset` (ADR 0015); thinkers são um deles ("itens de lista de UI, thinkers", ADR 0015, item 3).

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem script no caso comum:

1. criar um **thinker**: um objeto de jogo em **pool**, com dono (quem o criou), tempo de vida, eventos próprios (ao criar, a cada pulso, ao terminar) e uma **lista de ações** por evento, com a mesma base e os mesmos seletores das habilidades (B-012);
2. criar um thinker por uma ação (**`CreateThinkerAction`**), de uma habilidade, de um efeito ou de um projétil, com posição, rotação, duração e se **segue** o dono ou fica no lugar;
3. usar uma **aura**: uma peça pronta do thinker que **aplica um efeito a quem entra no raio e o remove de quem sai** (e de todos quando o thinker termina), com filtros por tag e, via Bridges, por relação de time;
4. dar ao **projétil** a **perfuração** (atravessa N alvos ou todos), o acerto **uma vez por alvo**, o **evento de término** (por tempo, por obstáculo, por acabar a perfuração ou por devolução por código) e um **filtro de impacto** (por tag e, via Bridges, por time);
5. acertar alvos cujo ASC está num **pai** do colisor, como o targeting já faz;
6. ter tudo isso **no tempo do ASC e do `ITimeSource`** (pausa e câmera lenta valem), **sem corrotina da Unity** e **sem alocar** em regime.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Montar uma zona de fogo, uma aura de bênção e um projétil que perfura no Inspector, com ações por evento e filtros |
| **Desenvolvedor de gameplay** | `CreateThinkerAction` e um thinker próprio por subclasse; ouvir o término do projétil; thinker sem NRE por pool ausente |
| **Designer de nível** | Colocar um thinker numa cena como "zona" sem passar por uma habilidade |
| **Mantenedor do pacote** | Um vocabulário só (habilidade, efeito, projétil, thinker): as mesmas ações e seletores; objetos criados e devolvidos pelo `SpawnerAsset` |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, thinker com pulsos:** um thinker com `Duração = 5 s` e `Intervalo = 1 s` e uma ação `DebugLog` em "A cada pulso" roda a ação 5 vezes (pulsos em 1, 2, 3, 4 e 5 s, **contados pelo tempo do ASC dono**, regra do B-030) e roda "Ao terminar" uma vez; depois volta ao pool.
- **C2, ações no mesmo vocabulário:** a mesma `ApplyEffect` roda na lista "A cada pulso" do thinker e na lista "Ao ativar" de uma habilidade (B-012, C1).
- **C3, `CreateThinkerAction`:** a ação cria o thinker pelo `SpawnerAsset` na posição escolhida; o thinker sabe quem o criou (instigador, habilidade, nível: o `EffectContext` do B-007).
- **C4, seguir o dono:** com "segue o dono", o thinker acompanha o `Transform` do ASC; se o dono é destruído ou desativado, o thinker **termina** (com motivo "dono saiu") sem exceção. Com "fica no lugar", ele permanece na posição de criação.
- **C5, termina junto com quem o criou:** um thinker criado por uma ação de efeito, com "termina com a execução", termina quando o efeito é removido (B-012 C12) e quando a habilidade que o criou é cancelada; "Ao terminar" roda com o motivo certo.
- **C6, aura entra e sai:** uma aura de raio 5 aplica o efeito "Bênção" (infinito) ao alvo que **entra** no raio e o **remove** quando **sai**; um alvo que **já estava dentro** quando a aura nasceu recebe o efeito no primeiro pulso.
- **C7, aura termina:** quando o thinker da aura termina, o efeito sai de **todos** que estavam dentro; um efeito removido por outra via (purge, B-003) é reaplicado só no próximo pulso se o alvo continua dentro (padrão) (P3).
- **C8, filtros da aura:** com filtro de tag (só quem tem `Estado.Vivo`) e, via Bridges, "só aliados" (B-011), só esses recebem o efeito; um alvo que **deixa de casar** com o filtro enquanto está dentro perde o efeito.
- **C9, aura por nível:** o raio e o intervalo vêm de `%raio` e `%intervalo` do B-007; no nível 3, o raio é maior (sem recriar o thinker).
- **C10, projétil perfura:** `Pierce = 2` (atravessa 2 alvos além do primeiro): acerta 3 alvos em linha, roda "No impacto" 3 vezes (uma por alvo) e **termina** no terceiro; cada alvo é acertado **uma vez** mesmo que o colisor continue em contato (P4).
- **C11, perfuração sem limite:** "sem limite" atravessa todos até o fim do `Lifetime`; o término chega **por tempo**.
- **C12, término por tempo roda ações:** o projétil sem acerto termina por `Lifetime`, e a lista "Ao terminar" roda **uma vez** no ponto onde ele está, com o motivo "tempo". Sem ação em "Ao terminar", o comportamento é o de hoje (volta ao pool, sem ação).
- **C13, motivo do término:** os quatro motivos (`Hit` quando acabou a perfuração, `Lifetime`, `Obstacle` quando bate em colisor sem ASC, `Released` quando devolvido por código) chegam ao contexto de "Ao terminar" (P5).
- **C14, filtro de impacto:** com filtro "não acerta aliados" (B-011 em Bridges), o projétil **atravessa** o aliado sem impacto, sem gastar perfuração e sem terminar; com `DamageOwner` desligado, o dono segue ignorado como hoje.
- **C15, ASC no pai:** um colisor num filho cujo ASC está no pai é acertado (`GetComponentInParent`), e o alvo do impacto é esse ASC.
- **C16, sem regressão do projétil:** um projétil sem perfuração, sem filtro e sem "Ao terminar" se comporta como o de hoje (acerta o primeiro e volta ao pool; `OnHitActions` rodam uma vez).
- **C17, spawner ausente:** `CreateThinkerAction` com o spawner sem instância e `canExpand` desligado **pula** a criação com aviso, sem exceção (continuidade do RP-07).
- **C18, sem lixo:** 1000 pulsos de uma aura com 20 alvos dentro, e 1000 impactos de projétil com perfuração, alocam 0 bytes gerenciados depois do aquecimento.
- **C19, Inspector (S):** no host, o thinker e o projétil mostram as abas por evento, o resumo de uma linha e o seletor de alvo inline, conforme o [DS do Inspector de ações](../ds/inspector-de-acoes.md); a aura mostra raio, intervalo, efeito e filtros.

## 4. Requisitos funcionais

### 4.1 Thinker

R1. **`GameplayThinker`.** Um `MonoBehaviour` do `SpellBook.GAS` que representa um "objeto de ação no mundo". Guarda a **configuração** (prefab é do spawner; os campos abaixo são do componente) e **nunca** o estado de uma execução em campo de ação (ADR 0013). Reinicia o seu estado em `OnEnable` (como o `Projectile` hoje, ADR 0015), para voltar ao pool e sair dele limpo.

R2. **Configuração do thinker.**
- `Duration` (`ScalableFloat`, B-007): quanto vive; **zero ou "sem duração"** significa até terminar por outra via (P1);
- `PulseInterval` (`ScalableFloat`): de quanto em quanto tempo roda "A cada pulso"; zero ou vazio desliga os pulsos;
- `FollowOwner` (bool): segue o `Transform` do dono ou fica no lugar;
- `EndWithExecution` (bool, padrão **ligado**, P1): termina junto com a habilidade ou o efeito que o criou;
- `Spawner` (`SpawnerAsset`): usado para devolver o próprio thinker ao fim (ADR 0015).

R3. **Eventos do thinker** (ADR 0018, item 2; a lista fechada é do Arquiteto, P10): **Ao criar** (ao nascer, uma vez), **A cada pulso** e **Ao terminar** (com o **motivo**: `Expired`, `OwnerGone`, `ExecutionEnded`, `Released`). Cada evento tem uma lista de ações da **base única** (B-012), com `TargetSelector` por ação, e roda com o contexto do thinker: dono, nível, `EffectContext`, ponto e o alvo padrão do evento (o próprio thinker como ponto; **os alvos** de cada evento são decididos por seletor, normalmente de **área** centrada nele).

R4. **Tempo e esperas.** O tempo do thinker é o do `ITimeSource` do dono (ASC) e avança pelo `Tick`/modelo de execução do B-012; os pulsos contam **a partir do fim do pulso anterior** e um `Tick` no mesmo quadro conta (regra do B-030, R6 do PRD do B-012). Nenhuma corrotina da Unity. Com pausa ou câmera lenta, o thinker acompanha.

R5. **Término e limpeza.** Ao terminar (por qualquer motivo), o thinker: roda "Ao terminar" **uma vez**, cancela as ações em curso e pendentes dele (`Delayed`, esperas; B-012 R15), **remove os efeitos de aura** (R10) e volta ao `SpawnerAsset`. Terminar duas vezes não repete nada.

R6. **Término do dono.** Com `FollowOwner` ou `EndWithExecution`, se o dono é destruído ou desativado, o thinker **termina** com o motivo `OwnerGone`, sem exceção. Sem nenhum dos dois, o thinker vive só pela duração (P1).

R7. **Criação.** Um thinker é criado **só** pelo `SpawnerAsset` (campo `Spawner`; vazio usa o padrão, ADR 0015) e **nunca** por `PoolManager.Instance`, `Instantiate` ou `Destroy`. Spawner sem instância pula a criação com aviso (C17). O thinker pode também ser **colocado na cena** pelo designer (sem criador; dono nulo): ele roda os pulsos com contexto vazio, e o validador avisa quando uma ação precisa de dono (P2).

R8. **`CreateThinkerAction`.** Uma ação da base de ações (B-012), em `SpellBook.GAS`, que cria o thinker por spawner na posição escolhida por `TargetSelector` (ponto, alvo, quem lançou), com rotação, e liga o thinker ao contexto: dono = quem lançou, instigador, habilidade, nível, `EffectContext` do B-007. Não bloqueia a sequência; devolve o thinker criado ao contexto (para uma ação seguinte configurá-lo, por exemplo, mudar a duração por nível). Tem `[HelpURL]` e página no manual.

### 4.2 Aura

R9. **`AuraBehaviour`** (peça pronta do thinker, **configuração** `[SerializeReference]` no `GameplayThinker`, ADR 0013). Define: o **`GameplayEffect`** aplicado, o **raio** (`ScalableFloat`), o **intervalo de verificação** (o `PulseInterval` do thinker, R2) e os **filtros** (R11). A cada pulso a aura calcula **quem está dentro** (consulta de área do `TargetSelector` de B-012, **sem alocar**) e compara com o conjunto do pulso anterior (que fica no **estado de execução** do thinker, não na configuração):
- **entrou:** aplica o efeito (instigador = dono do thinker; habilidade e nível do contexto);
- **saiu**, ou **deixou de casar com o filtro**: remove o efeito **que a aura aplicou** (pela instância; um efeito igual vindo de outra fonte não é removido, P3);
- **continua dentro:** nada (o efeito da aura é infinito ou de duração própria; ver P3).

R10. **Remoção ao terminar.** Ao terminar o thinker (R5) ou ao `OwnerGone`, a aura remove o efeito de **todos** que ainda o têm por causa dela. Um efeito retirado por outra via (purge, expiração, B-003) **não** é reaplicado no mesmo pulso; é reaplicado no pulso seguinte **se o alvo continua dentro** (padrão, P3).

R11. **Filtros da aura.** Por **tags** do alvo (`GameplayTagQuery`, B-006: exigidas, ignoradas), por **incluir o dono**, por **máximo de alvos** e por **ordem** (mais perto primeiro); e, **no `GAS.Bridges`**, por **relação de time** (`TeamRelationMask` do B-011). O filtro de time **é o mesmo** do B-011 (R14 do PRD dele); o B-014 só o usa. Sem o módulo Teams, a aura funciona sem filtro de time.

R12. **Aura que segue uma unidade (estilo buff).** Uma aura "presa a uma unidade" é a **combinação**: um efeito infinito no dono, com uma ação "Ao ser aplicado" (`CreateThinkerAction` com `FollowOwner` e `EndWithExecution`) e o thinker com a aura. Não existe tipo "aura de efeito" separado; o PRD documenta a receita (ADR 0018, item 6 e 7: ações de fluxo são ações como as outras).

R13. **Aura ao estilo de pulso (reaplicação).** O caso "aplica a cada pulso um efeito **com duração um pouco maior que o intervalo**" (renovação) é montado com a lista "A cada pulso": `ForEachTarget` + `ApplyEffect` com o seletor de área. Não é o `AuraBehaviour`; é uma **receita** do manual. Aplicar o mesmo efeito sem `CanStack` **renova** (B-002 R6a).

### 4.3 Projétil: perfuração, término e filtro

R14. **Perfuração.** O `Projectile` ganha a configuração `Pierce`: o **número de alvos que ele atravessa depois do primeiro** (0 = o comportamento de hoje: acerta o primeiro e termina) e a opção **"sem limite"** (P4). A cada impacto válido, roda "No impacto"; se ainda há perfuração, **continua**; se acabou, **termina** com o motivo `Hit`.

R15. **Um acerto por alvo.** Enquanto atravessa, um mesmo alvo é acertado **uma vez** por vida do projétil (um conjunto de "já acertou", **limpo** em `OnEnable`, ao voltar do pool, e **sem alocar** em regime; capacidade pré-alocada com uma política para o limite, P6). Configurável por `HitEachTargetOnce` (padrão **ligado** quando `Pierce > 0`).

R16. **Evento "Ao terminar".** O projétil tem a lista de ações "**Ao terminar**" (ADR 0018, item 2; o B-012 deixou o evento para este item, R2 e seção 6 do PRD dele), que roda **uma vez** quando ele termina, no ponto onde ele está, com o contexto do projétil e o **motivo**: `Hit` (acabou a perfuração), `Lifetime` (expirou), `Obstacle` (colidiu com algo sem ASC, P7) e `Released` (devolvido por código). Sem ação nessa lista, o término é **como hoje**: volta ao pool, sem rodar nada.

R17. **Filtro de impacto.** O projétil tem uma lista de **condições de impacto** (`[SerializeReference]`, hierarquia `Condition` do B-009, ADR 0019) avaliadas por **candidato a alvo** (origem = quem lançou; alvo = o ASC acertado): tag exigida ou bloqueadora (`TagQueryCondition`, B-009) e, **no Bridges**, `TeamRelationCondition` (B-011). Um candidato que **não passa** é **atravessado sem impacto**: não roda "No impacto", não gasta perfuração e não termina o projétil (C14). Sem condição, todo candidato passa (como hoje, salvo `DamageOwner`).

R18. **ASC no pai.** `HandleCollision` encontra o ASC do alvo pelo próprio objeto **ou pelo pai** (`GetComponentInParent`), como o targeting. Dois colisores do mesmo alvo contam como **um** só acerto quando `HitEachTargetOnce` está ligado.

R19. **Mantém o que existe.** `DamageOwner`, `InitialInvulnerabilityDuration`, `Lifetime`, `IsHoming`, movimento, flip e o uso do `SpawnerAsset` para criar e devolver (`Setup`, ADR 0015) **não mudam**. O projétil roda "No impacto" na **base de ações nova** do B-012 (ele deixa de usar `Source.StartCoroutine`; é decisão do B-012, P15).

R20. **Término por obstáculo.** Colisor **sem ASC** (parede, chão) **termina** o projétil, como hoje (P7). Um colisor sem ASC **dentro** do filtro de camadas do projétil é ignorado pela física da Unity (a matriz de camadas é do projeto; o pacote não a gerencia).

### 4.4 Cues (opcional, sem dependência obrigatória)

R21. **Feedback por evento.** O GAS dispara cues **só por ações**: o thinker e o projétil tocam cues com `ExecuteCueAction` (toca) ou `AddCueAction` (liga, some quando o efeito ou a ativação acaba) numa lista de eventos (impacto, término, pulso). Este item **não** acrescenta cue próprio, lista de cues nem componente de ponte ao thinker ou ao projétil. O `SpawnVFXAction` **sai** do GAS junto com o `PlayCueAction`: VFX fica só no Cues. Sem roteador configurado, a ação avisa e não toca. Sem o módulo Cues, o thinker e o projétil funcionam, sem feedback visual.

### 4.5 Editor, validação e documentação

R22. **Inspector** (UI Toolkit, ADR 0026, herdando de `SpellBookEditor`): `GameplayThinker` e `Projectile` mostram **abas por evento**, o resumo de uma linha por ação e o seletor de alvo inline, exatamente como no **[DS do Inspector de ações](../ds/inspector-de-acoes.md)** (a Spec cita a seção 3.2 do DS para as abas do thinker e do projétil). A aura mostra o raio, o intervalo, o efeito e os filtros no cabeçalho da seção, e a **prévia de um círculo** de raio no Gizmo da cena (desenhado só no Editor, com a cor do time quando o Bridges está presente).

R23. **Validação** (`IValidatable`, ADR 0026), com mensagens clicáveis:
- **Thinker:** sem duração e sem pulso e sem `EndWithExecution` (nunca termina, aviso); `PulseInterval` negativo; `Duration` negativa; ação em "A cada pulso" sem pulso configurado (aviso); ação que precisa de dono num thinker colocado na cena sem dono (aviso, P2); `Spawner` vazio (sem aviso: ADR 0015).
- **Aura:** sem efeito; efeito instantâneo (aviso: não há o que remover ao sair); raio zero ou negativo; sem intervalo (não verifica); filtro de time sem o módulo Teams (aviso); aura com intervalo menor que um quadro (aviso).
- **Projétil:** `Pierce` negativo; condição nula; "sem limite" com `Lifetime` zero (nunca termina, aviso); "Ao terminar" com ação que cria outro projétil igual (aviso de laço potencial); `HitEachTargetOnce` desligado com perfuração (aviso: acertos repetidos por quadro).
- **`CreateThinkerAction`:** sem prefab de thinker; thinker sem o componente `GameplayThinker`; duração por nível com `%nome` inexistente.

R24. **Documentação.** A página **Thinkers e auras** descreve o thinker, os eventos, `CreateThinkerAction`, a aura, e as **três receitas** (zona de fogo no chão, aura de bênção presa a uma unidade R12, aura de renovação por pulso R13). A página do **Projétil** descreve a perfuração, "um acerto por alvo", o término e o filtro. Uma seção de **migração** lista o que muda no projétil (`OnHitActions` na base nova, B-012). Os tipos novos têm `[HelpURL]` (decisão I15).

R25. **CHANGELOG.** Cita: `GameplayThinker`, `CreateThinkerAction`, `AuraBehaviour`, perfuração e "um acerto por alvo", o evento "Ao terminar" do projétil, as condições de impacto, o acerto por ASC no pai (correção do item 14 do relatório) e a regra de pulso do B-030.

## 5. Requisitos não funcionais

N1. **Alocação:** pulsos de aura, consulta de área, conjunto "já acertou" e término não alocam depois do aquecimento (C18); buffers de física pré-alocados (B-012 R13). Criar o thinker sai do pool, sem `Instantiate` em regime.
N2. **Módulos (ADR 0012, 0018):** `GameplayThinker`, `CreateThinkerAction`, `AuraBehaviour` e as mudanças do `Projectile` ficam em **`SpellBook.GAS`** e criam objetos só pelo `SpawnerAsset` do Core. O GAS **não** referencia Pooling, Teams, Cues nem Events. Filtro e condição de time ficam no **`GAS.Bridges`**.
N3. **Tempo:** só `ITimeSource` e `Tick` do dono (ADR 0014); nenhuma corrotina da Unity; testes com `ManualTimeSource`.
N4. **Estado:** o conjunto "quem está dentro" da aura e o "já acertou" do projétil são **estado de execução** do componente, nunca da configuração `[SerializeReference]` (ADR 0013). Duas auras iguais criadas pelo mesmo asset não compartilham estado.
N5. **Reentrada:** uma ação de "A cada pulso" que cria ou destrói thinkers, aplica ou remove o efeito da aura ou muda o conjunto de alvos não corrompe a iteração (a mesma regra do B-012 N7).
N6. **Sem regressão:** a suíte do GAS (com B-002 a B-012) passa sem edição de teste antigo, **exceto** os testes do `Projectile` que usam `OnHitActions` da hierarquia antiga ou o `StartCoroutine` (já listados pelo B-012, N8). Um projétil sem os campos novos se comporta como o de hoje (C16).
N7. **Serialização:** campos novos com padrão que reproduz o comportamento de hoje (`Pierce = 0`, sem condições, sem "Ao terminar"); thinker e aura são tipos novos, sem asset antigo a migrar; os assets do `Projectile` abrem sem perda de dados (a mudança de `OnHitActions` é a quebra do B-012, não deste item).
N8. **Física:** o thinker usa consulta de área (overlap) por pulso, sem depender de gatilho de colisor, para o resultado ser **determinístico no teste** (P8).

## 6. Fora do escopo

- **A base de ação única, os eventos nomeados, `TargetSelector`, `Delayed`, `ForEachTarget`, `Chance`, gatilhos e o modelo de execução:** **B-012**. O thinker e o projétil **usam** isso; não recriam.
- **Projétil rodando "No impacto" sem `StartCoroutine`** (P15 do B-012): decisão do B-012. Aqui só entra o que é **novo** (perfuração, término, filtro).
- **Teams e filtros de relação:** **B-011**. Aqui só **usa** o filtro e a condição de time (R11, R17).
- **Cues:** **B-013**. Aqui nenhuma dependência (R21).
- **`ConditionalAction` e a hierarquia `Condition`:** **B-009** e **B-012**.
- **`EffectContext` e valores por nível:** **B-007**. **Lista, remoção e eventos de efeito:** **B-003**.
- **Efeito concede habilidade, itens e inventário:** sem item no backlog.
- **Zona com forma além do círculo** (caixa, cone, polígono): a **consulta de área** do `TargetSelector` do B-012 é a fonte; formas novas são dela. Aqui, o círculo (e a esfera em 3D) de raio configurável.
- **Projétil com trajetória curva, balística, ricochete e divisão em vários:** fora (só perfuração e término). Ricochete e divisão ficam como **receita** com a ação "Ao terminar" criar outros projéteis (R23, aviso de laço).
- **Arma, dano contínuo por contato (colisor "queima enquanto encosta")** além da aura: fora; a aura cobre o caso por pulso.
- **UI** (barra de vida da torre, círculo de alcance no HUD): módulo de UI.
- **IA do thinker** (torre que mira e atira): do jogo; o thinker apenas roda ações por pulso.
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Thinker**
- CA1 (T). Cenário C1: o thinker roda "Ao criar" uma vez, "A cada pulso" nos instantes certos (pelo `ManualTimeSource`) e "Ao terminar" uma vez; depois volta ao pool.
- CA2 (T). Cenário C2: a mesma ação roda na lista de pulso do thinker e na de ativação de uma habilidade.
- CA3 (T). Cenário C3: `CreateThinkerAction` cria o thinker pelo `SpawnerAsset` com dono, instigador, habilidade e nível; devolve o thinker ao contexto.
- CA4 (T). Cenário C4: `FollowOwner` acompanha o `Transform`; dono destruído ou desativado termina o thinker com `OwnerGone`, sem exceção; sem seguir, fica no lugar.
- CA5 (T). Cenário C5: `EndWithExecution` termina o thinker junto com o efeito removido e a habilidade cancelada; "Ao terminar" roda com o motivo certo, uma vez.
- CA6 (T). Terminar cancela as ações em curso e pendentes do thinker; terminar duas vezes não repete "Ao terminar".
- CA7 (T). Cenário C17: spawner sem instância pula a criação com aviso, sem exceção.
- CA8 (T). O thinker voltando do pool nasce limpo (sem conjunto de aura, sem temporizador, sem dono antigo).
- CA9 (T). A regra do B-030 vale para os pulsos: "Tick no mesmo quadro do fim do pulso conta para o intervalo".

**Aura**
- CA10 (T). Cenário C6: o efeito é aplicado ao entrar (e ao nascer a aura com o alvo dentro) e removido ao sair, pela instância que a aura aplicou.
- CA11 (T). Cenário C7: ao terminar o thinker, o efeito sai de todos; um efeito igual vindo de outra fonte **não** é removido pela aura.
- CA12 (T). Cenário C8: os filtros de tag, "incluir dono", máximo e ordem; um alvo que deixa de casar perde o efeito.
- CA13 (T). Cenário C9: raio e intervalo por `%nome` (B-007) mudam com o nível sem recriar o thinker.
- CA14 (T). Com o módulo Teams, o filtro de relação ("só aliados", "só hostis") funciona; sem o módulo, a aura funciona sem filtro de time e o validador avisa o uso de filtro de time sem o módulo.
- CA15 (T). Duas auras do mesmo asset não compartilham o conjunto de quem está dentro (N4).
- CA16 (T). Receita R12 (aura presa a uma unidade) e R13 (reaplicação por pulso) como testes de ponta a ponta.

**Projétil**
- CA17 (T). Cenário C10: perfura `Pierce` alvos, roda "No impacto" uma vez por alvo e termina com `Hit`; "um acerto por alvo" ignora o contato repetido.
- CA18 (T). Cenário C11: "sem limite" atravessa todos e termina por `Lifetime`.
- CA19 (T). Cenários C12 e C13: "Ao terminar" roda uma vez com o motivo certo (`Hit`, `Lifetime`, `Obstacle`, `Released`); sem ação, o término é o de hoje.
- CA20 (T). Cenário C14: o filtro de impacto atravessa o candidato que não passa, sem rodar "No impacto", sem gastar perfuração e sem terminar.
- CA21 (T). Cenário C15: colisor num filho com o ASC no pai é acertado e o alvo é o ASC do pai; dois colisores do mesmo alvo contam como um acerto.
- CA22 (T). Cenário C16: sem os campos novos o projétil se comporta como o de hoje (um teste de equivalência com os casos do `Projectile` existente).
- CA23 (T). `DamageOwner`, `InitialInvulnerabilityDuration`, `Lifetime` e `IsHoming` continuam como hoje.
- CA24 (T). Um projétil que volta ao pool e sai de novo nasce sem o conjunto "já acertou" do uso anterior.

**Módulos, desempenho, editor e documentação**
- CA25 (T). O teste de assemblies da 0-M passa: o GAS não referencia Pooling, Teams, Cues nem Events; a condição e o filtro de time estão no `Bridges`.
- CA26 (E). Nenhuma ação ou componente novo usa `StartCoroutine`, `WaitForSeconds`, `Time.time` nem `PoolManager.Instance`, `Instantiate`, `Destroy` ou `PlayClipAtPoint` para objetos de gameplay (busca de código; ADR 0014, 0015, 0018).
- CA27 (T). Cenário C18: 1000 pulsos de aura com 20 alvos e 1000 impactos de projétil com perfuração alocam 0 bytes gerenciados depois do aquecimento.
- CA28 (T). Um ouvinte ou ação que cria ou destrói thinkers, ou aplica ou remove o efeito da aura dentro do pulso, não corrompe a iteração (N5).
- CA29 (S). Cenário C19: no host, o thinker e o projétil mostram abas por evento, resumo de uma linha e seletor de alvo inline (DS); a aura mostra raio, intervalo, efeito e filtros, e o Gizmo do raio aparece na cena.
- CA30 (T). O validador cobre os casos do R23, com o objeto clicável.
- CA31 (E). O CHANGELOG cita os itens do R25; as páginas **Thinkers e auras** e **Projétil** existem com as receitas; os tipos novos têm `[HelpURL]`.
- CA32 (T). A suíte do GAS existente passa sem edição, exceto os testes listados pelo B-012 (N6).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Vida do thinker.** *Respondida (usuário, 2026-10-31): aceita. `Duration` **opcional** (zero = até terminar por outra via), `EndWithExecution` **ligado por padrão** e `FollowOwner` **desligado por padrão**.*
- **P2. Thinker colocado na cena.** *Respondida (usuário, 2026-10-31): aceita. O thinker **sem dono** é permitido (zona de nível na cena), com contexto vazio; o validador avisa se uma ação precisa de dono.*
- **P3. Efeito da aura.** *Respondida (usuário, 2026-10-31): aceita. Modo **"enquanto dentro"**; a aura remove **só o que ela aplicou** (pela instância) e **reaplica no pulso seguinte** se o efeito foi retirado por outra via e o alvo continua dentro. A renovação por duração curta é a receita R13.*
- **P4. Perfuração.** *Respondida (usuário, 2026-10-31): aceita. `Pierce` = número de alvos atravessados depois do primeiro (0 = como hoje), com opção "sem limite"; "um acerto por alvo" ligado por padrão quando há perfuração.*
- **P5. Término do projétil.** *Respondida (usuário, 2026-10-31): aceita. "Ao terminar" existe para **todo término** (`Hit`, `Lifetime`, `Obstacle`, `Released`), roda **uma vez**, com o motivo no contexto.*
- **P6. Capacidade do "já acertou".** *Respondida (usuário, 2026-10-31): aceita. Limite fixo pré-alocado por projétil; ao estourar, o validador avisa e os acertos além do limite contam como acerto repetido. O número é do Arquiteto.*
- **P7. Colisor sem ASC.** *Respondida (usuário, 2026-10-31): aceita. O colisor sem ASC continua **terminando** o projétil (`Obstacle`), como hoje, sem máscara própria de obstáculos.*

### Com o Arquiteto (decisão técnica da Spec)

- **P8. Detecção da aura:** consulta de área por pulso (overlap, proposta, determinística em teste) ou gatilho de colisor (entra e sai por evento da física); como o conjunto "quem está dentro" é guardado sem alocar.
- **P9. Onde moram `GameplayThinker` e `AuraBehaviour`:** `SpellBook.GAS` (proposta, ADR 0012) e se `AuraBehaviour` é `[SerializeReference]` do thinker ou outra peça (a aura "presa a uma unidade" R12 sem tipo novo).
- **P10. Lista fechada de eventos do thinker e do projétil** (B-012, P10; ADR 0018): "Ao criar", "A cada pulso" e "Ao terminar" (e o motivo no contexto); a convivência com o `OnHitActions` do projétil na base nova.
- **P11. Contexto de execução do thinker:** como o thinker guarda o contexto do criador (dono, habilidade, nível, `EffectContext`) sem alocar por pulso, e como o B-012 (P7) o unifica.
- **P12. Gancho do filtro de impacto:** a assinatura do contexto da `Condition` por candidato (origem, alvo, projétil), compatível com o B-009 (P5) e o B-012 (P11 do PRD do B-011: o gancho de filtros do seletor), para o `TeamRelationCondition` encaixar.
- **P13. Efeito da aura e a instância:** guardar a `ActiveEffectInstance` que a aura aplicou (para remover só ela) sem alocar por alvo (B-003); onde fica o mapa "alvo → instância".
- **P14. Cancelamento no término:** o thinker cancelar ações em curso e pendentes (`Delayed`) com a mesma regra do B-012 (P13) e voltar ao pool uma vez só (como o `_released` do `Projectile`).
- **P15. Regra do B-030 nos pulsos:** o modelo de execução do B-012 garante o "Tick no mesmo quadro conta" também para o `PulseInterval`; confirmar e fixar com teste.
- **P16. Ordem entre tickets:** o projétil (perfuração, término, ASC no pai) não depende do B-011 nem do B-013 e pode ir primeiro, logo depois do B-012; o thinker e a aura vêm depois; o filtro de time é o último ticket (depende de B-011).

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-014, do plano (seção B "Thinkers e auras", item 14 do relatório e Fase 8) e das ADRs 0012, 0013, 0014, 0015 e 0018; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-012 (base de ação, eventos, seletor, modelo de execução), B-003, B-007, B-009, B-002; B-011 (Teams) só nos filtros em Bridges; B-013 (Cues) sem dependência obrigatória | Seção 6, R11, R17, R21 | Aplicado |
| 2026-10-31 | Gate do Lote D: cues só por ações (`ExecuteCueAction` toca, `AddCueAction` liga); sem lista de cues nem ponte; `SpawnVFXAction` sai do GAS (VFX só no Cues); sem roteador: avisa e não toca; filtro de alvo único (ADR 0035) | R21 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1 a P7: todas as propostas aceitas (padrões do thinker; thinker sem dono na cena; aura "enquanto dentro" com reaplicação no pulso seguinte; `Pierce` depois do primeiro; "Ao terminar" em todo término; limite fixo do "já acertou"; colisor sem ASC termina) | R2, R6, R7, R9, R10, R14 a R16, R20 (sem mudança de texto: já descreviam isso) | Aplicado |
