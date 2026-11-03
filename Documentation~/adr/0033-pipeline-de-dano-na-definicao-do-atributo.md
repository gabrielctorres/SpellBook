# 0033 — Pipeline de dano: processadores na definição do atributo, meta-atributo marcado e relatório da alteração; respostas aos "Em aberto" da 0024

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O B-010 (PRD `gas-dano-escudo-e-morte.md`, Spec `gas-dano-escudo-e-morte.md`) entrega o pipeline da 0024. Ela deixou oito itens em aberto; o usuário respondeu quatro no PRD (escudo sem duração, tag de estado opcional, sem cálculo de execução, resistência e imunidade prontas). Restam os técnicos. Restrições:

- Depois do AR3 (B-005), um instantâneo ou um tique compõe, por atributo, todos os modificadores daquela aplicação numa nova base (0029, item 4). A "operação" deixa de existir no ponto em que a base muda: só há a base antiga e a nova.
- O `EffectContext` (0030) é um `readonly struct` sem o efeito; o efeito vem no `EffectSpec` e na instância ativa.
- O `AttributeState` não conhece o contexto do efeito; o ASC conhece.

## Decisão

1. **Onde moram os processadores:** só na `AttributeDefinition` (lista `[SerializeReference]` ordenada). Não há lista no `AttributeSet`. A variação por entidade vem de **atributos** do alvo (resistência, escudo), que os processadores leem. Com uma fonte só, não há ordem entre fontes.
2. **Escopo:** só alterações do **valor base** por efeito instantâneo, por tique periódico e pela API pública `ApplyAttributeChange` passam pelos processadores. Modificadores persistentes de efeitos com duração não passam (não são dano). `AttributeState.SetBaseValue` continua sendo a escrita direta, sem pipeline (inicialização, depuração).
3. **A alteração pendente é uma variação.** `AttributeChange` (`struct`, passado por `ref`) leva o atributo, a variação pedida (`nova base - base atual`, já com a composição da 0029), a variação corrente e o efeito de origem (nulo por código). O contexto do efeito vai junto por `in`. Tags de dano são `effect.DamageTags`.
4. **Redirecionamento:** um processador gera alterações em outros atributos por uma fila do ASC (sem alocação). A alteração redirecionada passa pelos processadores do atributo de destino, com profundidade máxima 8 e sem repetir um atributo já na cadeia (repetição: aplicada direto, com um aviso por definição).
5. **Ordem de aplicação:** as alterações redirecionadas são aplicadas na ordem em que foram geradas e **antes** da alteração original. Com escudo e vida na mesma alteração, o escudo quebra (`OnDepleted` do escudo) antes de a vida chegar ao mínimo.
6. **Meta-atributo:** atributo comum marcado (`AttributeDefinition.IsMeta`). A variação final dele é descartada: a base continua 0 e nenhum evento de valor dispara. Meta-atributo em modificador persistente é erro do validador.
7. **Relatório da alteração:** cada alteração aplicada gera um `AttributeChangeReport` (pedido, final depois dos processadores, aplicado depois do limite, efeito, contexto, origem do redirecionamento), entregue pelo evento do ASC `OnAttributeChangeApplied`. É a base dos números flutuantes (B-015) e do `OnTakeDamage` (B-012). A morte (`OnAttributeDepleted`) leva o relatório da alteração que causou a transição; sem pipeline (limite mudou, modificador), o relatório vem vazio.
8. **Sem gancho de pós-alteração:** o `OnValueChanged` do atributo e o relatório cumprem.
9. **Instigador destruído:** referência nula (comparação da Unity), sem identificador sobrevivente, como na 0030 (P8 do B-007).

## Motivo

- Uma fonte de processadores é mais fácil de ler e validar; "resistência deste chefe" já é um atributo.
- Variação em vez de operação é o que sobra depois da composição da 0029, e é o que escudo e resistência precisam.
- Aplicar o redirecionado antes do original dá uma ordem de eventos fixa e testável.

## Consequências

- Duas entidades com o mesmo atributo têm os mesmos processadores; quem precisa de comportamento diferente usa atributos diferentes ou um processador próprio que lê um atributo.
- O ASC ganha uma fila de alterações reutilizada e um campo com a alteração em curso (lido pelo relay, no Bridges).
- A 0024 ganha, ao ser aceita esta ADR, uma emenda datada de uma linha apontando para cá.

## O que os agentes NÃO podem fazer

- Criar uma segunda lista de processadores (no `AttributeSet`, no efeito ou no ASC).
- Passar modificadores persistentes pelos processadores.
- Fazer o processador aplicar efeito, disparar evento de gameplay ou guardar estado na instância.
- Codificar tipo de dano fora de `GameplayTag`.

## Em aberto

Nada.

## Relacionadas

- 0013 (costura), 0024 (pipeline), 0029 (composição), 0030 (`EffectContext`), 0016 (relay no Bridges).
