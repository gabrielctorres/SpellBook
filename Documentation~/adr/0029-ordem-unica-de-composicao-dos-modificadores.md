# 0029 — Ordem única de composição dos modificadores

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

Hoje o valor de um atributo é calculado de dois jeitos (lido em `AttributeState.cs:55-94` e `AbilitySystemComponent.cs:563-586`):

- **efeito com duração ou infinito:** os modificadores ficam na lista do `AttributeState`, e o `Recalculate` faz `(base + ΣAdd) × (1 + ΣMultiply)`, depois `Override` (o último vence) e o limite;
- **efeito instantâneo e tique periódico:** cada modificador altera a base **em sequência, na ordem da lista** (`Multiply` compõe sobre o resultado do anterior, um `Add` depois de um `Multiply` soma depois, e um `Override` no meio da lista é sobrescrito pelo que vem depois).

Dois itens do Lote B dependem de uma regra só:

- o B-005 (PRD `gas-atributos-ricos.md`, R11 a R13) acrescenta `MultiplyCompound` e `AddFinal` e pede que instantâneo, tique e duração usem a mesma ordem (P2 do PRD, respondida: unificar, com aviso no CHANGELOG);
- o B-007 (PRD `gas-valores-por-nivel-e-effectspec.md`, R22) faz as pilhas multiplicarem a magnitude e pede que isso aconteça "antes da ordem de composição do B-005".

## Decisão

1. **Fórmula única** para um atributo, sobre um conjunto de modificadores:

   `((base + ΣAdd) × (1 + ΣMultiply)) × Π(1 + MultiplyCompound) + ΣAddFinal`

   Depois, se houver `Override`, o valor passa a ser o do último `Override` aplicado. Por último, o limite entre o mínimo e o máximo efetivos.
2. **Valor de cada modificador antes da fórmula:** `valor do modificador = valor de uma pilha × fator de pilhas`, com fator `CurrentStacks` quando o efeito multiplica pelas pilhas (B-007) e `1` nos outros casos. A fórmula só vê o valor já multiplicado.
3. **Efeitos com duração e infinitos:** o conjunto é a lista de modificadores ativos do atributo (todas as fontes), e a fórmula roda no `Recalculate`, partindo da base.
4. **Efeitos instantâneos e tiques periódicos:** o conjunto é o dos modificadores **daquela aplicação** (ou daquele tique) que miram o mesmo atributo. A fórmula roda **uma vez por atributo**, partindo da base atual, e o resultado vira a nova base (`SetBaseValue`, que limita, B-002). Aplicações sucessivas compõem entre si, porque cada uma parte da base deixada pela anterior.
5. **Um acumulador só.** Um tipo interno do GAS (`ModifierAccumulator`) soma os quatro grupos e aplica a fórmula. O `Recalculate` e a aplicação na base usam o mesmo acumulador, para a regra não divergir de novo.
6. **Ordem dentro de cada grupo:** a da lista (as somas de ponto flutuante seguem a ordem de inserção), para que, sem `MultiplyCompound` e `AddFinal`, o resultado de um efeito com duração seja idêntico, bit a bit, ao do B-002.
7. **Enum:** `MultiplyCompound` e `AddFinal` entram no fim de `ModifierOperation` (serializado como inteiro).

## Motivo

- Quem descreve um efeito para o jogador (B-008) precisa de uma regra só, que valha para instantâneo, tique e duração.
- Multiplicar pelas pilhas antes da fórmula mantém a semântica de cada operação: três pilhas de "+5" somam 15; três pilhas de "+10 %" somam 30 % no grupo `Multiply`.
- Um acumulador compartilhado impede a divergência que existe hoje.

## Consequências

- **Mudança de comportamento (CHANGELOG, 0009):** um efeito instantâneo ou um tique com **dois ou mais modificadores no mesmo atributo** pode dar outro resultado. Exemplos com base 100: `Multiply 0,5` e `Multiply 0,5` davam 225 e passam a dar 200; `Multiply 0,5` e depois `Add 10` davam 160 e passam a dar 165; `Override 50` e depois `Add 10` davam 60 e passam a dar 50. Efeito com um só modificador por atributo não muda.
- Nenhum teste da suíte atual usa `Multiply` ou `Override` em efeito instantâneo (busca em `Tests/` em 2026-10-31). A Spec do B-005 lista o que mudar se um teste do Lote A passar a usar.
- O `ModifierAccumulator` é o ponto onde o B-010 (processadores de pré-alteração) vai se encaixar.

## O que os agentes NÃO podem fazer

- Calcular modificadores fora do `ModifierAccumulator`, ou com outra ordem, em qualquer caminho do GAS.
- Multiplicar pelas pilhas depois da fórmula (por exemplo, multiplicar o valor final do atributo).
- Inserir operações novas no meio de `ModifierOperation`.

## Relacionadas

- 0009 (quebras na 3.0), 0021 (pilhas), 0020 (valores por nível), 0024 (pipeline de dano).
