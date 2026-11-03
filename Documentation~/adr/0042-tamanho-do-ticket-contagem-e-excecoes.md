# 0042 — Tamanho do ticket, contagem e exceções (substitui a 0003)

Status: Aceita (aprovada pelo usuário em 2026-11-03)
Data: 2026-11-03

## Contexto

A 0003 juntou mais de 30 emendas em cinco dias. Mais da metade eram exceções únicas de tamanho (EV3, EA7, VN2b, VN4, VN5, RL3, RL5, VN9, VN10, AV6, XR1, EV2, EV4b, EV10, EV11a), cada uma com emenda, pergunta e registro. A regra que vale ficou espalhada. Na decisão de 2026-11-03, o usuário pediu uma página só. O texto da 0003 fica como histórico e não vale mais como regra.

## Decisão

1. **Unidade.** Um ticket = uma issue = um commit em Conventional Commits. Ele entrega um comportamento testável sozinho e depende só de tickets anteriores. Os commits de um lote vão na branch do lote, e o PR é dessa branch contra a `main`. O merge é do usuário.
2. **Teto:** ~500 linhas e 10 arquivos, medidos no "Detalhe (AAAA-MM-DD)" do ticket (0002, emenda de 2026-11-03). Uma estimativa antiga é rascunho.
3. **Contagem.**
   - Linhas são as adicionadas mais as removidas em `.cs`, `.asmdef`, `.json`, `.yml` de CI e `package.json`. Os outros arquivos (páginas do manual, fixtures, skills, agentes, docs) contam só nos arquivos.
   - Arquivos são os caminhos distintos do diff.
   - Não contam em nenhum dos dois limites: `.meta`; saída de gerador do repositório com guarda (lista na 0040); remoção pura, que é um arquivo apagado sem substituto no mesmo ticket; a entrada do `nav` que acompanha a página criada ou apagada; o Detalhe do próprio ticket no design doc e a linha dele no `Documentation~/decisoes.md`.
   - Movimento puro (`R100` em `git diff -M --name-status`) conta 1 linha e nenhum arquivo. Movido com alteração conta 1 arquivo e as linhas da diferença.
4. **Exceção até +30%** (até ~650 linhas ou 13 arquivos). Quem detalha registra o motivo numa linha de `Documentation~/decisoes.md`. A exceção vale só para aquele ticket e termina no commit, sem emenda e sem pergunta. Isso vale também quando o excesso aparece na implementação ou na revisão.
5. **Acima de +30%**, divide-se o ticket (`Xa`, `Xb`), cada parte dentro do teto. O Controlador cria a issue da parte nova.
6. **Arquivo fora do Detalhe.** Só entra se for consequência direta do ticket, por exemplo um teste que quebra pelo comportamento novo. Ele conta nos limites, e o Examinador o confere. Outro arquivo faz a Fábrica parar e rever o Detalhe.
7. **Testes.** Na implementação, rodam só os testes do ticket, com o filtro do Unity Test Framework (sintaxe na skill `/implementar`), e um filtro com zero testes não vale como red. Na revisão, roda a suíte completa com `-testCategory "!Documentacao"`.
8. **Fora do ticket de código:** manual, catálogo e referência da API ficam para o passe de documentação (0007 e 0040, emendas de 2026-11-03). O `CHANGELOG.md` entra só no ticket que quebra algo, com a migração.
9. **WIP e paralelo:** seguem a 0005 (emenda de 2026-11-03).
10. O Detalhe vale como o plano que o `CLAUDE.md` pede para mudanças em mais de três arquivos.

## Consequências

- Exceção de tamanho não gera emenda: o registro é a linha do `decisoes.md`.
- As exceções antigas da 0003 (EV3, EA7, VN9 e as outras) ficam só como histórico.
- O `CLAUDE.md`, seção "Time de desenvolvimento", resume esta ADR. As skills `/implementar` e `/revisao` apontam para ela (ticket PS1 da `spec/processo-enxuto.md`).

## O que os agentes NÃO podem fazer

- Juntar dois tickets num commit, ou incluir arquivo fora do Detalhe que não seja consequência direta do ticket.
- Passar de +30% sem dividir, ou passar do teto sem a linha no `decisoes.md`.
- Escrever emenda de ADR para uma exceção de tamanho.
- O Examinador aprovar um diff acima do teto sem a linha do ticket no `decisoes.md`.

## Relacionadas

- 0002 (fluxo e Detalhe), 0003 (substituída, histórico), 0004 (testes primeiro), 0005 (trava e WIP), 0007 e 0040 (passe de documentação), 0008 (commits).
