# 0024 — Pipeline genérico de dano

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

O GAS só altera atributos: não há ganchos antes de uma alteração, nem absorção, nem sinal de "morte", nem tipo de dano. O valor base não é limitado pelo mínimo e pelo máximo (só o atual é), o que deixa a base de um atributo de vida abaixo de zero e faz curas seguintes "sumirem". A decisão I.7 do usuário: pipeline genérico próprio, com processadores de pré-alteração, meta-atributo de dano opcional, absorção, `OnDepleted` como morte e tipos de dano por tag.

## Decisão

1. **Processadores de pré-alteração** (proposta): uma lista ordenada, escolhida por `[SerializeReference]` (0013), que recebe uma alteração pendente de atributo com o contexto do efeito e pode alterá-la (reduzir, redirecionar, anular) antes de ela ser aplicada.
2. **Meta-atributo de dano, opcional**: um efeito pode aplicar dano num atributo intermediário; os processadores convertem esse valor em alterações nos atributos reais (escudo, vida). Quem não precisa aplica direto no atributo de vida.
3. **Absorção** é um processador pronto: um atributo de escudo consome a alteração antes do atributo protegido.
4. **Morte é um sinal, não um sistema:** o atributo emite um evento quando chega ao mínimo (`OnDepleted`) e quando chega ao máximo. O GAS não destrói objetos nem tem componente de vida; quem usa o pacote decide o que é morrer.
5. **Tipos de dano são tags** levadas no contexto do efeito; processadores (resistências, imunidades) decidem por elas.
6. O valor base também é limitado pelo mínimo e pelo máximo.
7. Nada no pipeline depende de engine, gênero ou componente de terceiros.

## Motivo

- Cobre dano, escudo, resistência e morte com peças pequenas e substituíveis.
- Quem já tem sistema de vida próprio pode ignorar o meta-atributo e escutar só o evento.

## Consequências

- Limitar a base muda comportamento (quebra, 0009).
- Processadores dependem do `EffectContext` (0018/0020) para saber origem, tipo e nível.

## O que os agentes NÃO podem fazer

- Colocar no GAS conceito de vida, morte ou destruição de objeto além do evento do atributo.
- Fazer processador com dependência de Teams, Cues ou outro módulo fora do GAS (isso vai em Bridges).
- Codificar tipo de dano como enum em vez de tag.

## Em aberto

- Os processadores ficam no `AttributeSet` (por entidade), na definição do atributo (por tipo de atributo) ou nos dois?
- O meta-atributo é um atributo comum marcado de algum jeito, ou um tipo especial que nunca guarda valor?
- Ordem e prioridade entre processadores de fontes diferentes.
- Há também ganchos de pós-alteração?
- Os processadores agem só em alterações do valor base (instantâneo e tick) ou também em modificadores de efeitos com duração?
- Ao chegar ao mínimo, o GAS concede automaticamente uma tag de estado (por exemplo, "morto")?
- O escudo é um atributo base (consumido) ou um modificador de um efeito com duração (que expira)?
- Existe um equivalente a cálculo de execução (dano calculado com atributos de origem e alvo ao mesmo tempo, crítico)?

## Relacionadas

- 0009 (quebra), 0013 (processadores), 0018 (contexto), 0020 (valores por nível), 0021 (magnitude).
