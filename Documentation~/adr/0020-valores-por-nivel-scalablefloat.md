# 0020 — Valores por nível (`ScalableFloat` em array ou curva)

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

O `AbilitySpec` tem `Level`, e o `AbilityContext` o carrega, mas nada o consome: o nível não chega ao efeito, e magnitudes, recargas, custos e durações são constantes. Os dois modelos de referência resolvem isso com valores por nível (tabela nomeada referenciada por nome, e valor escalável por curva). A decisão I.9 do usuário: `ScalableFloat` em array ("10 8 6 4") ou curva, numa tabela `AbilityValues` referenciada por `%nome`.

## Decisão

1. **`ScalableFloat`** (proposta): valor numérico que depende do nível, em um de dois modos escolhidos por campo:
   - **array**: um valor por nível, editável como texto "10 8 6 4" ou lista;
   - **curva**: `AnimationCurve` avaliada no nível.
2. **`AbilityValues`**: tabela de valores nomeados na habilidade, cada um um `ScalableFloat`.
3. Campos numéricos de habilidade e efeito (magnitude, custo, recarga, duração, cargas, alcance) podem usar uma constante, um atributo, uma fórmula ou um valor da tabela por `%nome`.
4. O nível vem do contexto de execução (0018) e é levado da habilidade até o efeito aplicado.
5. A habilidade tem nível máximo e evento de mudança de nível no spec.
6. Nomes de `%nome` são escolhidos numa lista no Inspector e validados (nome inexistente é erro do validador, 0007).

## Motivo

- Balanceamento por nível sem duplicar assets.
- A tabela nomeada é o ponto único que a descrição (0023) e uma futura tabela de balanceamento leem.

## Consequências

- Efeitos aplicados fora de uma habilidade precisam de uma regra para resolver `%nome` e nível.
- O `ModifierValueSource` ganha novas origens (valor da tabela, nível, valor passado pelo chamador), o que muda o formato serializado (0009).

## O que os agentes NÃO podem fazer

- Ler nível de outro lugar que não seja o contexto de execução.
- Resolver `%nome` por string em tempo de execução a cada avaliação, com alocação.
- Aceitar `%nome` digitado livre sem validação.

## Em aberto

- O primeiro item do array é o nível 1 ou o nível 0?
- Nível acima do tamanho do array: repete o último valor (proposta) ou é erro?
- Curva avaliada em nível inteiro ou aceita nível fracionário?
- Efeito aplicado sem habilidade (por exemplo, `startingEffects`): de onde vêm o nível e a tabela?
- O valor passado pelo chamador (SetByCaller) é identificado por tag ou por nome?

## Relacionadas

- 0018 (contexto), 0021 (magnitude), 0023 (descrição), 0024 (dano).
