# PRD: Correções do núcleo do GAS
Backlog: B-002 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (item 3, "correções que entram junto"; item 6, bug do valor base), seção D (lacunas 6, 8, 13 e 15) e decisão I44.
Achados A1 e A2 da Spec do `sample-sandbox` (Arquiteto), incluídos em 2026-10-31.
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

Quem usa o GAS do SpellBook hoje esbarra em comportamentos que parecem bugs e que obrigam a contornos no código do jogo:

- **O valor base de um atributo não tem limite.** O limite entre mínimo e máximo só vale para o valor atual. Um dano grande leva a base de Vida abaixo de zero, e as curas seguintes "somem", porque primeiro precisam pagar a dívida.
- **Reaplicar um efeito sem empilhamento cria uma instância paralela** (achado A1). Com `CanStack` desligado, cada nova aplicação cria outra instância do mesmo efeito, com tags e modificadores duplicados, em vez de renovar a existente.
- **Remover um efeito duas vezes refaz a limpeza.** A segunda chamada roda de novo a limpeza, as ações de remoção e a devolução das tags, e a contagem de tags fica errada.
- **Renovar um efeito usa a duração do asset, não a aplicada.** Uma recarga reduzida por um atributo (por exemplo, 6 s no lugar de 8 s) volta para 8 s quando o efeito é renovado.
- **A recarga normalizada (0 a 1) divide pela duração do asset.** Com a recarga reduzida, a barra radial de uma UI nunca começa cheia.
- **A consulta do tempo de recarga compara tags por igualdade exata.** O resto do ASC trata tags de forma hierárquica (uma tag filha conta como a tag pai). O resultado depende de qual método se chama.
- **O cálculo de atributos aloca memória a cada recálculo** (consultas LINQ), e roda a cada tick de um efeito periódico. A decisão I44 aceitou essa alocação só até este item.
- **Um contexto derivado de habilidade perde os dados customizados.** Um projétil que acerta algo recebe um contexto sem as chaves que a habilidade gravou.
- **Spawn sem pool na cena gerava NullReferenceException** (`PoolManager.Instance` nulo). A modularização (0-M, R24 a R26) já tirou o acesso direto ao singleton; este item só garante que o conserto tem teste.

O resultado é que o desenvolvedor escreve código de defesa e o designer vê números que não batem com o que configurou.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue confiar nas regras abaixo sem contorno:

1. o valor base de um atributo nunca sai do intervalo definido no `AttributeDefinition` (o que também permite regenerar um recurso, como mana, com teto; achado A2);
2. remover um efeito é seguro e acontece uma única vez, não importa quantas vezes a chamada se repita;
3. renovar um efeito devolve a duração com que ele foi aplicado;
4. a recarga normalizada vai de 1 (recém-aplicada) a 0 (pronta), também quando há redução de recarga;
5. o tempo de recarga respeita a hierarquia de tags;
6. recalcular um atributo e remover modificadores não aloca memória em regime;
7. o contexto derivado carrega os dados customizados da habilidade.

É um item de **correção**: nenhuma API pública nova é obrigatória (ver seção 6 para o que fica fora).

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | Números previsíveis, chamadas repetidas sem efeito colateral, sem alocação por tick, sem reflexão nem checagem extra para contornar |
| **Designer técnico** | O que ele configura (limites do atributo, redução de recarga, tags de recarga) é o que aparece em jogo e na UI |
| **Mantenedor do pacote** | Cada correção coberta por um teste que falha antes e passa depois |

**Cenários** (cada um vira teste automatizado):

- **C1, dano acima da vida:** Vida com mínimo 0 e máximo 100, base 100. Um efeito instantâneo tira 150. A base fica em 0. Uma cura de 30 leva a base a 30.
- **C2, remoção dupla:** dois efeitos diferentes concedem a mesma tag. O desenvolvedor remove o primeiro duas vezes seguidas. A tag continua presente (pelo segundo efeito), a ação de remoção rodou uma vez.
- **C3, renovação de recarga reduzida:** uma habilidade de recarga 8 s, com redução que a leva a 6 s. Passam 4 s e a habilidade é ativada de novo no limite que renova o efeito. O tempo restante volta a 6 s, não a 8 s.
- **C4, barra de recarga:** com a recarga reduzida a 6 s, logo após ativar o valor normalizado é 1; com 3 s restantes, é 0,5.
- **C5, recarga por tag pai:** o efeito de recarga concede a tag `Cooldown`. Outro efeito ativo concede `Cooldown.Fire`. O tempo restante e o estado "em recarga" consideram os dois, como `HasTag` considera.
- **C6, tick sem lixo:** um efeito infinito periódico altera um atributo a cada 0,1 s. Durante 300 quadros de teste, o ASC não aloca memória gerenciada.
- **C7, impacto de projétil:** a habilidade grava `"crit" = true` no contexto. O projétil acerta algo e o contexto derivado do impacto enxerga `"crit"`.

## 4. Requisitos funcionais

### 4.1 Atributos

R1. **Limite do valor base** (origem: achado A2; já cobre o caso de regeneração com teto, sem requisito novo). O valor base fica sempre entre `MinValue` e `MaxValue` do `AttributeDefinition`. O limite vale em toda forma de alterar a base: definição direta, efeitos instantâneos e ticks de efeitos periódicos, e também na criação do atributo (um valor inicial fora do intervalo é limitado e o pacote avisa **uma vez** no Console, por atributo, com o nome do atributo e do objeto; decisão P2). Para atributos derivados (calculados por fórmula), o comportamento atual continua: o aviso de que a base será sobrescrita.

R2. **Os limites continuam constantes.** Limites dinâmicos por atributo ficam no B-005 (seção 6).

R3. **Recálculo sem alocação.** `Recalculate`, adicionar e remover modificadores (inclusive remover todos os de uma fonte) não alocam memória gerenciada em regime, ou seja, com a lista de modificadores já dimensionada. O resultado numérico é idêntico ao de hoje: `(base + soma dos Add) × (1 + soma dos Multiply)`, o último `Override` aplicado vence, e o valor atual é limitado entre mínimo e máximo.

R4. **Tick sem alocação.** `ASC.Tick` com efeitos periódicos ativos não aloca em regime. Isso cumpre o N2 da 0-M e encerra a exceção da decisão I44. A exceção vale só para atributos sem fórmula: o custo de fórmulas por expressão fica fora do escopo (seção 6).

### 4.2 Efeitos ativos

R5. **Remoção idempotente.** Chamar `RemoveEffect` com uma instância que já foi removida (ou que nunca esteve ativa neste ASC) não faz nada: não roda a limpeza, as ações de remoção nem a devolução das tags e dos modificadores uma segunda vez. Uma instância nula também é ignorada, sem exceção. Isso vale quando a remoção explícita acontece no mesmo quadro da expiração.

R6. **Renovação com a duração aplicada.** Quando um efeito com duração é renovado por empilhamento (`RefreshDurationOnStack`), o tempo restante volta à duração **com que aquela instância foi aplicada** (incluindo qualquer redução ou substituição na aplicação), e não à duração do asset. Os efeitos aplicados sem substituição não mudam de comportamento.

R6a. **Reaplicação sem empilhamento renova** (achado A1). Aplicar um efeito com duração cuja instância já está ativa neste ASC, com `CanStack` desligado, não cria uma segunda instância: renova a existente. O tempo restante volta à duração aplicada na nova aplicação (R6), as cargas continuam em 1 e tags e modificadores não são duplicados. Com `CanStack` ligado, o comportamento de empilhar não muda. Efeitos infinitos reaplicados sem `CanStack` também não geram segunda instância.

### 4.3 Recarga

R7. **Recarga normalizada pela duração aplicada.** O valor normalizado é o tempo restante dividido pela duração aplicada da instância que define o tempo restante. Fica entre 0 e 1: vale 1 logo após aplicar (inclusive com redução de recarga) e 0 quando não há recarga. Duração aplicada zero ou negativa dá 0, sem divisão por zero.

R8. **Casamento hierárquico de tags na recarga.** O tempo restante de recarga (e, por consequência, o estado "em recarga" e o valor normalizado) considera, além do próprio efeito de recarga, qualquer efeito ativo que conceda uma tag que case com alguma tag do efeito de recarga, pela mesma regra de hierarquia usada em `HasTag`. Quando vários efeitos casam, vale o de maior tempo restante.

### 4.4 Contexto e spawn

R9. **`CreateDerived` copia os dados customizados.** O contexto derivado recebe uma **cópia rasa** do `CustomData`: as mesmas chaves e os mesmos valores. Acrescentar ou trocar uma chave no derivado não altera o contexto original, e o contrário também. Os valores que são objetos continuam sendo as mesmas referências. Os demais campos herdados hoje (origem, habilidade, nível) não mudam.

R10. **Spawn sem pool na cena.** Spawnar um projétil ou tocar um cue sem nenhum `PoolManager` na cena não lança exceção (0-M, R24 a R26). Este item só acrescenta o teste de regressão, caso a 0-M não o tenha; se já existir um teste equivalente, ele é citado na Spec e nada é duplicado.

### 4.5 Compatibilidade e documentação

R11. **CHANGELOG.** A entrada da pré-release descreve as mudanças de comportamento visíveis: base limitada (R1), renovação e normalização pela duração aplicada (R6, R7), tags hierárquicas na recarga (R8) e cópia do `CustomData` (R9). Pela decisão I1 não há fachada de compatibilidade.

R12. **Manual.** A página do manual do GAS que explica atributos, efeitos e recarga passa a descrever as regras R1, R5 a R8. Sem a atualização da página o ticket não passa na revisão (decisão I15).

## 5. Requisitos não funcionais

N1. **Alocação:** R3 e R4 medidos por teste (bytes alocados na thread, depois do aquecimento, iguais a zero).
N2. **Determinismo:** os testes usam `ManualTimeSource` e nunca o relógio real.
N3. **Sem regressão:** todos os testes da suíte do GAS existentes continuam passando sem alteração.
N4. **Sem API pública nova obrigatória.** Se a Spec precisar guardar a duração aplicada na instância, o acesso público a ela é do B-003.

## 6. Fora do escopo

- Lista pública de efeitos ativos, `Find`/`Remove` por tag, `Extend`/`Reduce`/`Restart`, eventos de aplicado/removido/pilhas, `ReduceCooldown`/`ResetCooldown`, `CooldownReductionPolicy` (piso de 0,1 s e teto da redução, hoje fixos) e a propriedade pública `Duration` da instância: **B-003**.
- `TryApplyEffect` e `EffectApplicationResult`: B-004.
- Limites mínimo e máximo dinâmicos por atributo, política ao mudar o máximo, `OnDepleted`/`OnReachedMax`: **B-005**.
- Magnitude ao vivo ou por snapshot e pilhas que multiplicam a magnitude: B-007.
- `CanAffordCost` ignorar custos com Multiply, Override ou fórmula; custo de `ExpressionFormula` (alocação por avaliação): sem item no backlog, entra na faxina se o usuário quiser.
- Correções do `Projectile` (filtro de time, perfuração, `GetComponentInParent`) e do `AnimationAction`/`WaitAction`: sem item no backlog.
- Qualquer mudança visual de Inspector.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Atributos**
- CA1 (T). Cenário C1: base 100 (mín 0, máx 100); dano instantâneo de 150 deixa `BaseValue` em 0 e `CurrentValue` em 0; cura de 30 leva os dois a 30.
- CA2 (T). Um valor base acima do máximo (por exemplo, 150 num atributo de máximo 100) fica em 100 depois de `SetBaseValue`.
- CA3 (T). Um atributo criado com valor inicial fora do intervalo nasce limitado.
- CA4 (T). Um tick de efeito periódico de dano não leva a base abaixo do mínimo.
- CA5 (T). Um atributo derivado continua avisando que a base será sobrescrita e continua calculando pela fórmula.
- CA6 (T). Casos numéricos de `Recalculate` (só Add; Add e Multiply; vários Multiply somados; dois Override, o último vence; resultado fora do intervalo é limitado) dão exatamente os mesmos valores de antes da correção.
- CA7 (T). Depois do aquecimento, 1000 chamadas de `Recalculate` e 1000 ciclos de adicionar e remover um modificador alocam 0 bytes gerenciados.
- CA8 (T). Cenário C6: 300 quadros de `ASC.Tick` com um efeito infinito periódico alocam 0 bytes gerenciados.

**Efeitos**
- CA9 (T). Cenário C2: remover a mesma instância duas vezes roda `OnRemoval` uma única vez e mantém a contagem das tags correta.
- CA10 (T). `RemoveEffect(null)` e remover uma instância de outro ASC não lançam exceção nem alteram nada.
- CA11 (T). Remover explicitamente um efeito no mesmo quadro em que ele expira roda a limpeza uma única vez.
- CA12 (T). Cenário C3: o efeito aplicado com duração reduzida (6 s, asset com 8 s) volta a 6 s ao ser renovado.
- CA13 (T). Um efeito aplicado sem substituição de duração volta, ao renovar, à duração do asset (comportamento mantido).

- CA25 (T). Achado A1: aplicar duas vezes um efeito com duração e `CanStack` desligado deixa uma única instância ativa, com o tempo restante renovado para a duração aplicada.
- CA26 (T). Na reaplicação do CA25, a tag concedida tem contagem 1 e o modificador do atributo foi aplicado uma vez; remover a instância devolve tudo ao estado inicial.
- CA27 (T). Um efeito infinito com `CanStack` desligado, aplicado duas vezes, deixa uma única instância.
- CA28 (T). Com `CanStack` ligado, a segunda aplicação continua empilhando (cargas 2, até `MaxStacks`).
- CA29 (T). Achado A2: um atributo de mana com máximo 100, regenerado por efeito periódico, para em 100 na base e no valor atual (cobre o R1).
- CA30 (T). Um atributo criado com valor inicial fora do intervalo registra exatamente um aviso no Console (nome do atributo no texto); criar o mesmo atributo de novo no mesmo ASC não repete o aviso (decisão P2).

**Recarga**
- CA14 (T). Cenário C4: normalizado 1 logo após a ativação com recarga reduzida, 0,5 com metade do tempo, 0 com a habilidade pronta.
- CA15 (T). Duração aplicada zero devolve 0 sem exceção.
- CA16 (T). Cenário C5: um efeito ativo com a tag filha `Cooldown.Fire` conta como recarga de um efeito cuja tag é `Cooldown`; um efeito com tag sem parentesco não conta.
- CA17 (T). Com dois efeitos casando, o tempo restante é o maior dos dois.

**Contexto e spawn**
- CA18 (T). Cenário C7: o contexto derivado enxerga as chaves e os valores do original.
- CA19 (T). Acrescentar uma chave no derivado não aparece no original, e o contrário também.
- CA20 (T). O derivado mantém origem, habilidade e nível (comportamento mantido).
- CA21 (T). Sem `PoolManager` na cena, ativar uma habilidade com `SpawnProjectileAction` e outra com `PlayCueAction` termina sem exceção.

**Compatibilidade e documentação**
- CA22 (E). O CHANGELOG cita as mudanças do R11.
- CA23 (E). A página do manual do GAS descreve R1, R5 a R8 (R12).
- CA24 (T). A suíte do GAS existente passa sem edição de nenhum teste antigo (N3).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Cópia do `CustomData`. RESPONDIDA (2026-10-31): cópia rasa.** Dicionário novo, mesmas referências: o derivado não polui o original, e objetos compartilhados, como um gerador de números aleatórios, continuam únicos. Já refletido no R9.
- **P2. Valor base fora do intervalo em assets existentes. RESPONDIDA (2026-10-31): limitar e avisar uma vez no Console.** O atributo nasce limitado e o pacote registra um aviso no Console, uma única vez por atributo. O validador do projeto não é exigido neste item. Refletido no R1 e no CA30.

### Com o Arquiteto (decisão técnica da Spec)

- **P3. Onde guardar a duração aplicada** na instância (R6, R7), sem criar API pública, já que a propriedade pública é do B-003.
- **P4. Direção do casamento de tags na recarga** (R8): a tag concedida deve ser filha da tag do efeito de recarga (como em `GameplayTag.Matches`) e não o contrário. Confirmar que é a mesma regra de `HasTag`.
- **P6. Reaplicação sem `CanStack` (R6a).** Renova sempre, ou respeita `RefreshDurationOnStack`? Este PRD assume que renova sempre, porque o flag trata de empilhar. Dizer também se `OnApplication` roda de novo na renovação (a proposta é que não rode).
- **P5. O R10 já está coberto?** O código atual não acessa `PoolManager.Instance` fora do módulo Pooling. Verificar se algum teste da 0-M já cobre o spawn sem pool.

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote A do backlog, a partir do texto de B-002 e do plano | Todo o documento | Rascunho |
| 2026-10-31 | Controlador (achados A1/A2 da Spec sample-sandbox) | A1: reaplicar sem `CanStack` deve renovar, não criar instância paralela. A2: base sem limite, já coberto pelo R1 | R6a, CA25 a CA29, P6; A2 só citado como origem do R1 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P1 = cópia rasa do `CustomData`; P2 = limitar e avisar uma vez no Console | R9 (confirmado); R1 e CA30 (aviso único); P1 e P2 marcadas como respondidas | Aplicado |
| 2026-10-29 | usuário | I44: a alocação do `ASC.Tick` periódico é aceita até o B-002 tirar o LINQ do `Recalculate` | R3, R4, CA7, CA8 | Aplicado |
| 2026-10-29 | usuário | I1: 3.0 com quebras livres, sem fachada de compatibilidade | R11 | Aplicado |
