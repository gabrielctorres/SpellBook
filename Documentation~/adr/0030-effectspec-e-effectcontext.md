# 0030 — `EffectSpec` e `EffectContext` por valor; respostas aos "Em aberto" da 0020 e da 0021

Status: Aceita (gate 2026-10-31), com a emenda de 2026-10-31 abaixo
Data: 2026-10-31

## Contexto

O B-007 (PRD `gas-valores-por-nivel-e-effectspec.md`) leva o nível, a habilidade, o instigador, o ponto de impacto e os valores do chamador até o efeito aplicado. Restrições do código e dos outros itens:

- O B-004 exige que `TryApplyEffect` de um efeito instantâneo não aloque (CA8 daquele PRD). Um contexto em classe, criado a cada aplicação, alocaria.
- Um mesmo efeito é aplicado a vários alvos pela mesma ação (`ApplyEffectAction.cs:25-30`): o que vem da habilidade é igual para todos, só o alvo muda.
- O efeito ativo precisa guardar o contexto para os tiques e para a magnitude ao vivo (R9 do PRD).
- A 0018 deixou em aberto se um contexto único substitui o `AbilityContext` e o futuro `EffectContext`. A 0020 e a 0021 deixaram itens em aberto que o B-007 precisa fechar.

## Decisão

1. **`EffectContext` é um `readonly struct`** com: instigador (ASC), alvo (ASC), habilidade de origem (pode ser nula), nível (inteiro, mínimo 1), ponto de impacto (com um indicador de presença) e os valores do chamador. Ele é copiado, nunca compartilhado por referência mutável.
2. **`EffectSpec` é um `struct`** com o `GameplayEffect`, um `EffectContext` sem alvo e, opcionalmente, a duração imposta. Ele é passado por `in`. O ASC preenche o alvo na aplicação. Um spec pode ser aplicado a vários alvos.
3. **Valores do chamador (SetByCaller):** uma classe reutilizável, `SetByCallerValues`, de quem chama (pode ser guardada e limpa entre usos). O efeito instantâneo lê os valores direto dela, sem cópia. O efeito com duração copia os valores para a própria instância na aplicação (uma alocação por instância, e só quando há valores), para que o valor fique fixo durante a vida do efeito (R12).
4. **A instância ativa guarda o seu `EffectContext`** (cópia). Tiques, remoção e magnitude ao vivo leem dele. Só a magnitude ao vivo dependente de nível atualiza o nível guardado (0021).
5. **Contexto da 0018: dois contextos, por recomendação.** O `AbilityContext` continua sendo o contexto **mutável da execução** (alvos, cancelamento, espera). O `EffectContext` é o **dado imutável do efeito**. O `EffectSpec` é montado a partir do `AbilityContext` (`EffectSpec.FromAbility`). A Spec do B-012 confirma ou troca, por ADR, ao fechar o "Tipo do contexto" da 0018.

**Respostas aos "Em aberto" da 0020** (os três primeiros vêm das respostas do usuário ao PRD do B-007):

- O primeiro item do array é o nível 1.
- Nível acima do tamanho do array repete o último valor.
- A curva é avaliada no nível inteiro.
- **Efeito aplicado sem habilidade** (código, `startingEffects` do B-005, Debugger do B-003): o nível é o `DefaultLevel` do próprio efeito (padrão 1). Não há tabela. Um `%nome` vale o valor reserva configurado no modificador (padrão 0), sem aviso.
- **SetByCaller por nome**, não por tag. O nome é um identificador (letras, números, sublinhado). A busca é por comparação ordinal numa lista curta, feita na aplicação (instantâneo) ou uma vez por instância (duração). Nunca por tique.

**Respostas aos "Em aberto" da 0021:**

- A opção de desligar a multiplicação pelas pilhas fica **no efeito** (`StacksMultiplyMagnitude`, padrão ligado). É resposta do usuário ao PRD (P10).
- **Tiques periódicos sempre avaliam o valor no tique**, como hoje, com o contexto da instância. O modo de magnitude (snapshot ou ao vivo) vale só para os modificadores persistentes (`Modifiers`) de efeitos com duração ou infinitos. Marcar "ao vivo" num modificador periódico ou de efeito instantâneo é aviso do validador, sem efeito.
- **Ciclos ao vivo:** o validador acusa o que dá para ver num asset só (modificador ao vivo que lê o próprio atributo que altera, e dois modificadores ao vivo do mesmo efeito que se leem em cruz). Em runtime, cada ligação ao vivo tem uma trava de reentrada: a segunda avaliação no mesmo ciclo usa o último valor e avisa uma vez por ligação.
- Outras políticas de pilha (agregação por origem, expiração por pilha) **ficam fora da 3.0** deste item e pedem ADR própria.

## Motivo

- Struct com `in` cumpre "instantâneo sem alocação" (B-004) e "uma alocação por instância, nunca por tique" (B-007, N1).
- Separar o contexto mutável da execução do dado imutável do efeito evita que uma ação altere, sem querer, o nível ou o instigador de um efeito já aplicado.
- Avaliar o tique no tique preserva o comportamento atual dos `PeriodicModifiers`, que hoje já leem o valor a cada tique (`AbilitySystemComponent.cs:572`).

## Consequências

- `ApplyEffect(efeito, origem)` continua e monta um spec mínimo (sem habilidade, `DefaultLevel`).
- Struct grande copiado em excesso custa CPU. Por isso o spec e o contexto circulam por `in` e por propriedade somente leitura.
- Ao ser aceita, o Arquiteto acrescenta uma emenda datada de uma linha na 0020 e na 0021, apontando para esta ADR, e a 0018 ganha a recomendação do item 5 como nota, sem fechar o "Tipo do contexto".

## Emenda (2026-10-31): `%nome` como modo do `ScalableFloat`

Decisão do usuário no gate (Q4 da Spec do B-007): "modular, sem remendos de código". Esta emenda ajusta a leitura do item "Efeito aplicado sem habilidade" e da busca por nome; o resto da ADR não muda.

- **Caminho único:** todo valor numérico do escopo do B-007 que pode variar com o nível é um `ScalableFloat`, com os modos `Constant`, `Array`, `Curve` e `Table` (`%nome`). `GameplayEffect.Duration` e a constante do `ModifierValueSource` passam de `float` a `ScalableFloat`. Não há campo `%nome` paralelo a um `float`, nem origem `AbilityValue` ou `Level` no `ModifierValueSource`.
- **Efeito sem habilidade:** o `ScalableFloat` em modo `Table` vale o próprio `Fallback` (padrão 0), sem aviso. Antes: "o valor reserva configurado no modificador".
- **Resolução do `%nome` sem texto por avaliação:** o `ScalableFloat` guarda em cache (`[NonSerialized]`) a última tabela, a versão dela e o índice. Com a mesma tabela e a mesma versão, a avaliação não compara texto. Isso substitui o vetor de índices por instância.
- **Migração (0009):** campo legado privado com `[FormerlySerializedAs]` do nome antigo e conversão no `OnAfterDeserialize`; menu de Editor que regrava os assets; os campos legados saem antes da 3.0 final, em ticket próprio.

## O que os agentes NÃO podem fazer

- Criar um segundo mecanismo de valor por nível ao lado do `ScalableFloat`.
- Transformar `EffectContext` em classe criada a cada aplicação.
- Ler o nível de um efeito de outro lugar que não seja o `EffectContext` (0020).
- Procurar SetByCaller ou `%nome` por texto a cada tique.

## Relacionadas

- 0018 (contexto de execução), 0020 (valores por nível), 0021 (magnitude e pilhas), 0024 (dano usa o `EffectContext`), 0029 (ordem de composição).
