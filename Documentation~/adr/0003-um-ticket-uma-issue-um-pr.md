# 0003 — Um ticket = uma issue = um PR

Status: Substituída por [0042](0042-tamanho-do-ticket-contagem-e-excecoes.md) (aprovada pelo usuário em 2026-11-03)
Data: 2026-10-29

> **Histórico.** Desde 2026-11-03, a regra que vale é a da 0042, que consolida numa página a Decisão e as emendas abaixo. Este texto não vale mais como regra e fica só como registro, inclusive as exceções únicas e a sintaxe do filtro de testes.

## Contexto

Diffs grandes são difíceis de revisar e escondem regressões. O Examinador dá veredito por ticket (`/revisao`), e o DevOps publica por ticket (`/commit`). O `CLAUDE.md` pede PRs pequenos, um commit por mudança lógica e plano aprovado antes de mudar mais de três arquivos.

## Decisão

1. Cada ticket da Spec:
   - entrega um comportamento testável sozinho;
   - depende só de tickets anteriores da mesma Spec ou de Specs já entregues;
   - tem até **~400 linhas de diff** e até **10 arquivos**. Arquivos `.meta` não contam nos dois limites.
2. Um ticket vira exatamente uma issue, uma branch (`feat/<feature>-<ticket>` ou `fix/<feature>-<ticket>`) e um PR contra a `main`. A descrição do PR traz motivação, abordagem, testes e o link da issue.
3. **WIP 1:** só um ticket em implementação. A Fábrica só pega o próximo depois do APROVADO do Examinador.
4. Se, na implementação, o ticket passar dos limites ou exigir arquivo fora do escopo, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto para dividir o ticket.
5. O ticket aprovado no gate (0002) vale como o plano exigido pelo `CLAUDE.md` para mudanças em mais de três arquivos.

## Motivo

- Revisão de verdade só acontece em diffs pequenos.
- Rastreabilidade simples: issue ↔ ticket ↔ PR ↔ entrada no CHANGELOG.
- Reverter um PR pequeno é barato.

## Consequências

- Specs grandes viram muitos tickets; o Arquiteto precisa ordenar dependências com cuidado.
- Mudanças mecânicas grandes (mover arquivos entre asmdefs, por exemplo) podem precisar de vários tickets em sequência.

## O que os agentes NÃO podem fazer

- Juntar dois tickets num mesmo PR ou commit.
- Incluir no diff arquivos fora do escopo do ticket, inclusive "aproveitando" para refatorar.
- Pegar um segundo ticket com outro em implementação ou em revisão.
- O Examinador aprovar um diff que passe dos limites ou toque arquivos fora do escopo.

## Decisões incorporadas (2026-10-29)

- **Páginas do manual e fixtures** (decisão I35): páginas em `Documentation~/manual/` e fixtures de teste (YAML de assets, arquivos de dados) **não contam** no limite de ~400 linhas, mas **contam** no limite de 10 arquivos.
- **Contagem** (decisão técnica do Arquiteto, para o Examinador aplicar sempre do mesmo jeito): "linhas" são as linhas adicionadas mais as removidas no diff de código (`.cs`, `.asmdef`, `.json`, `.yml` de CI, `package.json`), sem `.meta`, páginas do manual e fixtures. "Arquivos" são os caminhos distintos no diff (criados, alterados, movidos ou apagados), sem `.meta`. Um arquivo movido conta uma vez.

- **Remoção pura** (decisão I42): um arquivo apagado por inteiro, sem substituto no mesmo ticket, não conta no limite de 10 arquivos nem no de ~400 linhas. Ele continua listado no PR e é revisado pela lista de caminhos. Um arquivo apagado que tem substituto no mesmo ticket (por exemplo, movido ou reescrito com outro nome) conta normalmente.

## Emenda (2026-10-29): paralelo só para tickets sem Editor

Decisão do usuário: "só tickets sem Editor" podem rodar em paralelo.

- O WIP 1 (item 3 da Decisão) continua valendo para os tickets que usam a Unity. Eles pegam a trava (0005) e só um fica em implementação ou revisão por vez.
- Tickets de docs, processo e CI, que não abrem a Unity e não pegam a trava, podem rodar em paralelo a um ticket com Unity e entre si. Cada um tem a própria issue, branch e PR, e a regra 1 ticket = 1 PR não muda.
- Dois tickets em paralelo não podem tocar o mesmo arquivo. A Spec marca quais tickets podem andar em paralelo.
- O item "Pegar um segundo ticket com outro em implementação ou em revisão" da lista de proibições vale entre tickets com Unity e, para os sem Editor, só quando os dois tocam o mesmo arquivo.
- Como a 0006 (worktrees) está adiada, o ticket em paralelo não pode trocar a branch do checkout que o ticket com Unity está usando. Ele precisa de uma cópia de trabalho própria, ou espera.

## Emenda (2026-10-30): movimento puro conta como 1 linha

Decisão do usuário no gate da 0027: em vez de dar uma exceção ao ticket de migração, vale uma regra permanente. Ela é mais um item das "Decisões incorporadas":

- **Movimento puro:** um arquivo movido sem alteração de conteúdo conta como **1 linha** no limite de ~400.
  - Ele **não conta no limite de 10 arquivos**, como a remoção pura (I42). Leitura do Arquiteto, **confirmada pelo usuário em 2026-10-30** (0027, item 10 das decisões do usuário).
  - O arquivo continua listado no PR e é revisado pela lista de caminhos.
- **Como reconhecer:** o git mostra o movimento como `R100` (similaridade de 100%) em `git diff -M --name-status` contra a base do ticket. O Examinador confere que todo arquivo contado como movimento puro aparece com `R100`.
- **Movido com alteração** (`R` abaixo de 100, ou apagado e criado de novo com outro conteúdo): conta como antes, 1 arquivo e as linhas da diferença real.
- O `.meta` que acompanha o arquivo continua fora dos dois limites. Movê-lo junto, com o mesmo GUID, é obrigatório (0027).
- A frase "Um arquivo movido conta uma vez", em "Contagem", passa a valer só para o movimento com alteração.
- **Exceção única, não regra (decisão do usuário, 2026-10-30):** no 0M-54, os 21 arquivos de `ProjectSettings/` gerados pela Unity entram sem contar nos dois limites (0027, item 11 das decisões do usuário). Fora desse ticket, arquivo criado em `ProjectSettings/` conta normalmente.

O resto desta ADR não muda.

## Emenda (2026-10-30): teto de ~500 linhas por ticket

Decisão do usuário no gate de 2026-10-30. A regra é permanente e vale para todos os tickets daqui em diante.

- **Teto de linhas:** o limite passa de **~400** para **~500 linhas de diff** por ticket. Onde esta ADR diz "~400" como regra (item 1 da Decisão, "Decisões incorporadas" e a emenda do movimento puro), leia "~500". O texto anterior fica como está, como histórico.
- **O que não muda:** o limite de **10 arquivos**; a "Contagem" (linhas adicionadas mais removidas, nos mesmos tipos de arquivo, sem `.meta`); páginas do manual e fixtures contam só nos arquivos (I35); a remoção pura (I42); o movimento puro (emenda acima).
- **Motivo:** os tickets com testes PlayMode e EditMode completos passam de 400 linhas de forma recorrente (0M-22 e 0M-27). Os testes são o que a 0004 pede, e o código de produção desses tickets continua pequeno.
- **Exceções anteriores:** a exceção do 0M-22 (teto de 500) passa a ser só um registro, porque o teto geral já a cobre. As exceções do 0M-07 (440), do 0M-47 (~500) e do 0M-48 (~440) também ficam só como registro.
- O item 4 da Decisão continua: acima de ~500 linhas ou de 10 arquivos, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto.

O resto desta ADR não muda.

## Emenda (2026-10-31): exceção única para o EV3 do B-012

Decisão do usuário no gate do Lote C (2026-10-31, Q1 da Spec `gas-evento-acoes-alvos.md`). É uma **exceção única, não regra**, no mesmo molde da do 0M-54.

- **O que:** o ticket **EV3** (troca atômica da base das ações da habilidade para `GameplayAction`, ADR 0034) é um ticket só, com **~1100 linhas e ~22 arquivos**, acima dos dois limites. As três remoções puras do ticket (`AbilityAction.cs`, `AbilityContext.cs`, `EffectAction.cs`) seguem a regra I42 e não contam.
- **Por que:** trocar a base obriga a mudar no mesmo commit todas as ações, o ASC, o projétil, a factory e os testes que as usam. A alternativa, uma ponte temporária entre as duas bases por quatro tickets, seria um mecanismo paralelo, e o usuário a recusou.
- **Como revisar:** o Examinador revisa pela lista de arquivos do escopo do EV3 na Spec e pelos testes red nomeados nele. Arquivo fora da lista faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** Os demais tickets do B-012 seguem ~500 linhas e 10 arquivos.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única para o EA7 do B-003

Decisão do usuário em 2026-11-01, depois da revisão do EA7 (issue #131, Spec `gas-efeitos-ativos-e-recarga.md`). É uma **exceção única, não regra**, no mesmo molde da do EV3.

- **O que:** o ticket **EA7** (modelo do GAS Debugger e log de eventos) entra como um ticket só, com **621 linhas e 3 arquivos**, acima do teto de ~500 linhas: 361 em `Runtime/GAS/Diagnostics/GasDebuggerModel.cs` e 260 em `Tests/GAS/EditMode/GasDebuggerModelTests.cs`. A Spec estimava ~380. O limite de arquivos não foi passado.
- **Por que:** o ticket já estava implementado e aprovado em toda a revisão quando o tamanho apareceu. O usuário preferiu aceitá-lo como está a dividir um modelo e seus testes que só fazem sentido juntos.
- **Como revisar:** a revisão já feita vale. O escopo continua o da Spec (três arquivos); arquivo fora dele faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O EA8 e os demais tickets do B-003 seguem ~500 linhas e 10 arquivos.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única de arquivos para o VN2b do B-007

Decisão do usuário em 2026-11-01, depois que a Fábrica contou o escopo do VN2b (issue #221, Spec `gas-valores-por-nivel-e-effectspec.md`). É uma **exceção única, não regra**, no mesmo molde da do EA7 e da do EV3.

- **O que:** o ticket **VN2b** (constante do modificador como `ScalableFloat`) entra como um ticket só, com **14 arquivos**, acima do limite de 10. Os 4 arquivos a mais são testes PlayMode criados depois da Spec, que usam `ConstantValue` e `SourceType.Constant`: `Tests/GAS/PlayMode/DynamicLimitTests.cs`, `EffectApplicationResultTests.cs`, `EffectRemoveByTagTests.cs` e `InstantCompositionTests.cs`. O ticket fica em ~150 linhas, dentro do teto; o limite de linhas não foi passado.
- **Por que:** a troca de `ConstantValue` por `Value` quebra a compilação de todo teste que usa o nome antigo, então os 14 arquivos precisam mudar no mesmo commit. A edição nos 4 testes é mecânica. O usuário preferiu a exceção a dividir o ticket.
- **Como revisar:** o Examinador revisa pela lista de 14 arquivos do escopo do VN2b na Spec e pelos testes red nomeados nele. Cada teste editado só troca `ConstantValue = x` por `Value = ScalableFloat.Constant(x)` e `Constant` por `Value`. Arquivo fora da lista faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O VN3 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única para o VN4 do B-007

Decisão do usuário em 2026-11-01, depois da revisão do VN4 (issue #150, Spec `gas-valores-por-nivel-e-effectspec.md`). É uma **exceção única, não regra**, no mesmo molde da do EA7 e da do VN2b.

- **O que:** o ticket **VN4** (`AbilityValues`, modo `Table`, `MaxLevel` e API de nível) entra como um ticket só, com **581 linhas** (161 alteradas e 420 novas), acima do teto de ~500. A Spec estimava ~480. O limite de arquivos não foi passado: são **10 arquivos**, um a mais que os 9 da Spec, porque `Tests/GAS/EditMode/GasDebuggerModelTests.cs` precisou mudar quando o `MaxLevel` passou a começar em 1.
- **Por que:** o ticket já estava implementado e aprovado em toda a revisão quando o tamanho apareceu. A mudança no teste do Debugger é consequência direta do comportamento novo do ticket e não se separa dele.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, já corrigido com os 10 arquivos; arquivo fora dele faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O VN5 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda abaixo.

O resto desta ADR não muda.

## Emenda (2026-11-01): margem de ~30% registrada pelo Arquiteto

Decisão do usuário em 2026-11-01. A regra é permanente e vale para todos os tickets daqui em diante.

- **Quando vale:** a revisão do Examinador tem como **único achado** passar do teto, e todo o resto está aprovado. A passagem fica em até **~30%** acima de algum dos limites: até **~650 linhas** ou até **13 arquivos**, contados como em "Contagem" e nas emendas acima.
- **O que acontece:** o Arquiteto registra a exceção nesta ADR, numa emenda datada no molde das do EA7 e do VN4 (ticket, issue, números reais, por que passou, escopo corrigido na Spec), e o ticket segue para o DevOps **sem uma nova decisão do usuário**. O veredito do Examinador passa a APROVADO com a exceção citada.
- **Acima da margem** (mais de ~650 linhas ou mais de 13 arquivos), ou com qualquer outro achado na revisão, o ticket volta ao usuário pelo Controlador, como antes.
- **O que não muda:** o item 4 da Decisão. Se a estimativa já passar de ~500 linhas ou de 10 arquivos **antes de começar**, a Fábrica para e devolve ao Controlador, que leva ao Arquiteto para dividir o ticket. A margem cobre só o que se descobre na implementação, nunca a Spec: o Arquiteto continua escrevendo tickets dentro de ~500 linhas e 10 arquivos.
- **Arquivo fora do escopo:** a exceção registrada corrige o escopo do ticket na Spec. Um arquivo novo no diff só entra pela margem se for consequência direta do ticket (por exemplo, um teste que quebra pelo comportamento novo), e o Examinador o confere na revisão.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única para o VN5 do B-007

Registrada pelo Arquiteto pela margem de ~30% da emenda acima, depois da revisão do VN5 (issue #151, Spec `gas-valores-por-nivel-e-effectspec.md`). É uma **exceção única, não regra**, no mesmo molde da do EA7 e da do VN4.

- **O que:** o ticket **VN5** (`EffectContext`, `EffectSpec` e nível na avaliação) entra como um ticket só, com **~600 linhas e 11 arquivos**, acima do teto de ~500 linhas e do limite de 10 arquivos, e dentro da margem (até ~650 linhas e 13 arquivos). A Spec estimava ~490 linhas e 10 arquivos; o ticket chegou à revisão com 590 linhas e 10 arquivos.
- **Por que:** a revisão achou dois pontos de código, ambos consequência direta do ticket. `GameplayAbility.GetCooldownDuration(asc)` (público, `GameplayAbility.cs:111`) ainda avaliava `Duration.Evaluate(1)`, e a Spec o lista entre os leitores da duração; e o `GetCooldownDuration(in EffectSpec)` privado do ASC (`AbilitySystemComponent.cs:700-712`) duplicava a conta de redução da recarga. A correção põe `Runtime/GAS/Abilities/GameplayAbility.cs` no escopo, o 11º arquivo.
- **Como revisar:** o Examinador confere só a correção dos dois achados sobre a revisão já feita: o caminho público e o do spec avaliam a duração com o nível e a tabela e chamam a **mesma** conta de redução da recarga (`CooldownReductionPolicy`, EA6), sem cópia. O escopo é o da Spec, já corrigido com os 11 arquivos; arquivo fora dele faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O VN6 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda acima.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única para o RL3 do B-004

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", depois da revisão do RL3 (issue #135, Spec `gas-resultado-de-aplicacao-e-relay.md`). É uma **exceção única, não regra**, no mesmo molde da do VN4 e da do VN5.

- **O que:** o ticket **RL3** (Relay: núcleo, efeitos e bloqueios) entra como um ticket só, com **578 linhas e 8 arquivos**, acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de arquivos não foi passado. A Spec estimava ~480 linhas e 8 arquivos.
- **Por que:** o único achado da revisão foi o tamanho; todo o resto foi aprovado. As linhas a mais estão nos mesmos 8 arquivos do escopo da Spec, sem arquivo fora dele, e o ticket entrega um comportamento só (o relay com os pares de efeito e o `blocked`, o asmdef de Editor e a cobertura), que não se divide sem deixar uma parte sem teste.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com o tamanho já corrigido para 578 linhas e 8 arquivos; arquivo fora dele faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O RL4 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda acima.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única para o RL5 do B-004

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", depois da revisão do RL5 (issue #137, Spec `gas-resultado-de-aplicacao-e-relay.md`). É uma **exceção única, não regra**, no mesmo molde da do VN5 e da do RL3.

- **O que:** o ticket **RL5** (Inspector e validação do relay) entra como um ticket só, com **512 linhas e 11 arquivos**, acima do teto de ~500 linhas e do limite de 10 arquivos, e dentro da margem (até ~650 linhas e 13 arquivos). A Spec estimava ~440 linhas e 9 arquivos.
- **Por que:** o único achado da revisão foi o tamanho. Dois arquivos ficaram fora do escopo listado e são consequência direta do ticket: `Runtime/GAS.Bridges/Relay/RelayPair.cs`, onde mora a validação de cada par que o `IValidatable` do relay chama, e `Tests/GAS.Bridges/PlayMode/EventRelayTests.cs`, ajustado pelo comportamento novo. A remoção de `Editor/GAS.Bridges/GasBridgesEditors.cs` conta nos dois limites, porque o `AbilitySystemEventRelayEditor.cs` do mesmo ticket o substitui (não é remoção pura).
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, já corrigido para os 11 arquivos e 512 linhas; o Examinador confere que os dois arquivos acrescentados seguem o critério de "arquivo fora do escopo" da emenda da margem.
- **Não vale para nenhum outro ticket.** O RL6 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda acima.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única de arquivos para o VN9 do B-007

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), no VN9 (issue #155, Spec `gas-valores-por-nivel-e-effectspec.md`). É uma **exceção única, não regra**, no mesmo molde da do VN5 e da do RL5.

- **O que:** o ticket **VN9** (validação dos valores por nível) entra como um ticket só, com **11 arquivos e ~400 linhas**, acima do limite de 10 arquivos e dentro da margem (até 13 arquivos). O limite de linhas não foi passado. A Spec estimava ~420 linhas e 9 arquivos.
- **Por que:** o aviso `MaxLevelAboveArray_Warning` (CA33) precisa dos nomes das entradas da tabela e do tamanho de cada array, que hoje são privados em `Runtime/GAS/Values/AbilityValues.cs` e `Runtime/GAS/Values/ScalableFloat.cs`. Os dois arquivos entram no escopo só para dar acesso `internal` mínimo a esses dados. O `IsIdentifier` da `AbilityValues` (`AbilityValues.cs:142`) passa de `private` a `internal`, para a checagem do nome do `SetByCaller` (CA15) usar a mesma regra, sem cópia. São consequência direta do ticket, e a API pública não muda.
- **Como revisar:** o Examinador revisa pela lista de 11 arquivos do escopo do VN9 na Spec, já corrigida, e pelos testes red nomeados nele. Nos dois arquivos de `Values/` confere que a mudança é só de acesso `internal` (sem membro `public` novo e sem mudança de comportamento). Arquivo fora da lista faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O VN10 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda acima.

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única de linhas para o VN9 do B-007

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do VN9 (issue #155, Spec `gas-valores-por-nivel-e-effectspec.md`). Completa a emenda anterior do VN9, que previa ~400 linhas. É uma **exceção única, não regra**, no mesmo molde da do RL3 e da do RL5.

- **O que:** o ticket **VN9** (validação dos valores por nível) entra como um ticket só, com **559 linhas e 11 arquivos**, acima do teto de ~500 linhas e do limite de 10 arquivos, e dentro da margem (até ~650 linhas e 13 arquivos). A Spec estimava ~420 linhas e 9 arquivos, corrigida depois para ~400 linhas e 11 arquivos.
- **Por que:** o único achado da revisão foi o tamanho; todo o resto foi aprovado. Os 11 arquivos são os do escopo já corrigido na Spec, sem arquivo fora dele. As linhas a mais vêm das regras de validação e dos testes red dos cinco critérios do ticket (CA8, CA10, CA15, CA22 e CA33), que não se dividem sem deixar uma regra sem teste.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com o tamanho já corrigido para 559 linhas e 11 arquivos; arquivo fora dele faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** O VN10 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única de linhas para o VN10 do B-007

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do VN10 (issue #156, Spec `gas-valores-por-nivel-e-effectspec.md`). É uma **exceção única, não regra**, no mesmo molde da do RL3 e da do VN9.

- **O que:** o ticket **VN10** (Inspector do `ScalableFloat`, da tabela e nível de prévia) entra como um ticket só, com **523 linhas e 7 arquivos**, 4,6% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de arquivos não foi passado. A Spec estimava ~460 linhas e 6 arquivos.
- **Por que:** o tamanho passou por pouco e o ticket entrega um comportamento só (o drawer do `ScalableFloat` com os modos, a lista fechada de tabelas e o nível de prévia do Inspector da habilidade), que não se divide sem deixar uma parte do Inspector sem teste.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com o tamanho corrigido para 523 linhas e 7 arquivos. O arquivo além dos 6 listados só entra se seguir o critério de "arquivo fora do escopo" da emenda da margem (consequência direta do ticket), e o Examinador o confere; outro arquivo fora do escopo faz a Fábrica parar e devolver, como no item 4 da Decisão.
- **Não vale para nenhum outro ticket.** Os demais tickets seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-01): exceção única de linhas para o AV6 do B-003

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do AV6 (issue #162, Spec `gas-ativacao-avancada.md`). É uma **exceção única, não regra**, no mesmo molde da do RL3 e da do VN10.

- **O que:** o ticket **AV6** (cargas: configuração, gasto, recarga sequencial e API) entra como um ticket só, com **579 linhas e 7 arquivos**, 15,8% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de arquivos não foi passado. A Spec estimava ~480 linhas e 7 arquivos.
- **Por que:** o ticket entrega um comportamento só (o ciclo de cargas: configuração, gasto no commit, recarga sequencial no `Tick` e a API com `OnChargesChanged`), que não se divide sem deixar o gasto sem a recarga que o testa.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com os mesmos 7 arquivos e o tamanho corrigido para 579 linhas. Arquivo além dos 7 listados só entra se seguir o critério de "arquivo fora do escopo" da emenda da margem (consequência direta do ticket), e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O AV7 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-01): entrada do `nav` conta com a página

**Status da emenda: Aceita** (Q2 do gate da Spec `manual-reorganizacao.md`, B-035, aprovada em 2026-11-01). Vem junto com a emenda de 2026-11-01 da 0007, também aceita, que torna o `nav` do `mkdocs.yml` explícito.

- **Regra:** quando o ticket cria ou apaga uma página do manual, a entrada dessa página no `nav` do `Documentation~/mkdocs.yml` conta junto com a página. Se a única mudança no `mkdocs.yml` for essa entrada (uma linha por página), o arquivo não soma no limite de 10 arquivos e não soma linhas.
- **Qualquer outra mudança** no `mkdocs.yml` (tema, extensões, validação, ordem dos grupos) conta como antes: 1 arquivo.
- **Como revisar:** o Examinador confere que o diff do `mkdocs.yml` só tem as linhas do `nav` das páginas criadas ou apagadas no mesmo ticket.
- **Motivo:** sem a regra, seis tickets aprovados que já estão em 10 arquivos (DM1, TM1, CU2, DE8, UI2 e TT1) passariam do limite por uma linha que é parte da página.

O resto desta ADR não muda.

## Emenda (2026-11-01): saída de gerador commitada

**Status da emenda: Aceita** (G4 do gate da Spec `referencia-catalogo-e-api.md`, B-019g, aprovada em 2026-11-01). A regra é permanente e vale para todos os tickets daqui em diante.

- **Regra:** um arquivo escrito por um gerador do próprio repositório e commitado não conta no limite de ~500 linhas nem no de 10 arquivos, quando valem as três condições:
  - a Spec do ticket lista o caminho como saída gerada;
  - um teste de guarda falha se a saída commitada for diferente da que o gerador produz;
  - o gerador e a guarda já estão na `main` ou entram no mesmo ticket.
- **Hoje:** pela 0040, são saída gerada `Documentation~/manual/referencia/catalogo.md`, `Documentation~/api/reference/**` e `Documentation~/api/reference.sha256`, cobertos pelas guardas do RC4. Uma saída nova entra pela Spec que cria o gerador dela.
- **O que conta normalmente:** o código do gerador, os testes, a configuração e as páginas escritas à mão. Arquivo que a Unity escreve (`.meta` fica como já está; `ProjectSettings/`, YAML de cena, prefab e asset) não é saída de gerador do repositório. A exceção do 0M-54 continua única.
- **Como revisar:** o Examinador revisa o gerador e a guarda, confere que a guarda passa e que a saída gerada no diff só tem caminhos listados na Spec. Ele não revisa a saída linha a linha. A descrição do PR separa a lista de caminhos gerados do resto.
- **Motivo:** o DocFX escreve um YAML por tipo. Contada como fixture, a saída faria quase todo ticket que muda a API pública passar de 10 arquivos.

O resto desta ADR não muda.

## Emenda (2026-11-01): testes filtrados na implementação

Decisão do usuário em 2026-11-01. A regra é permanente e vale para os tickets com Unity daqui em diante.

- **Na implementação** (`/implementar`), a Fábrica roda só os testes do ticket, no red e no green, com o filtro do Unity Test Framework. Os testes do ticket são os nomeados em "Testes red" e as guardas citadas no critério de pronto.
- **Na revisão** (`/revisao`), o Examinador roda a suíte completa de EditMode e de PlayMode uma vez, sem filtro. Ela continua sendo a prova de que nada quebrou, e o APROVADO exige essa suíte verde.
- **Sintaxe**, conferida no `com.unity.test-framework` 1.6.0 instalado (`Packages/manifest.json:3`; no pacote, `UnityEditor.TestRunner/CommandLineTest/SettingsBuilder.cs:45-70`, `UnityEditor.TestRunner/CommandLineParser/CommandLineOption.cs:34-41`, `UnityEngine.TestRunner/TestRunner/RuntimeTestRunnerFilter.cs:24-90` e `UnityEngine.TestRunner/NUnitExtensions/Filters/AssemblyNameFilter.cs:11-18`):
  - `-testFilter` recebe uma lista separada por `;`. Cada item é uma expressão regular procurada em qualquer parte do nome completo do teste (`Namespace.Classe.Metodo`, com os argumentos no caso de `[TestCase]`). Basta o teste casar com um dos itens. Um item que começa com `!` exclui. Um item `^Nome.Exato$` sem outro caractere especial vira comparação exata.
  - `-assemblyNames` recebe nomes de assembly separados por `;`, comparados por inteiro e sem diferenciar maiúsculas (por exemplo, `SpellBook.Core.Tests.EditMode`).
  - Com `-testFilter` e `-assemblyNames` juntos, o teste precisa passar nos dois.
  - Como a busca é por trecho, a Fábrica ancora o nome da classe entre pontos (`\.TypeCatalogTests\.`). Sem os pontos, `CatalogTests` casaria também `TypePickerCatalogTests`.
  - No shell, o valor vai entre aspas, porque `;` separa comandos.
- **Exemplo** (RC2 da Spec `referencia-catalogo-e-api.md`, EditMode, da raiz do projeto):

  ```
  "$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -assemblyNames "SpellBook.Core.Tests.EditMode" -testFilter "\.TypeCatalogPageTests\.;\.TypeCatalogTests\." -testResults "$PWD/Logs/TestResults-EditMode.xml" -logFile "$PWD/Logs/unity-tests.log"
  ```

- **Filtro que não casa nada:** o runner termina verde com zero testes. A Fábrica confere no XML de resultado que o atributo `total` do `<test-run>` é maior que zero e bate com o número de testes do ticket, e cola esse número no relatório. Um red com zero testes não vale como red.
- **O que não muda:** os tickets sem Unity, a trava (0005), o timeout de 600 s do PlayMode e a limpeza de `Assets/InitTestScene*`. As skills `/implementar` e `/revisao` foram ajustadas a esta emenda.

O resto desta ADR não muda.

## Emenda (2026-11-01): experimento de WIP 2 com Unity no lote da referência

Decisão do usuário em 2026-11-01. É um **experimento**, não regra: vale só no próximo lote, os tickets RC1 a RC8, XR1 e XR2 da Spec `referencia-catalogo-e-api.md`. Ao fim do lote, o usuário decide se a regra fica, e o Arquiteto registra a decisão numa emenda nova.

- **O que muda:** dois tickets com Unity podem estar em implementação ou em revisão ao mesmo tempo, quando valem as duas condições:
  - nenhum dos dois depende do outro, nem por um ticket no meio;
  - os escopos não têm arquivo em comum. Contam também a saída gerada (emenda "saída de gerador commitada"), o `CHANGELOG.md`, os `.asmdef`, os `.meta` de pasta nova e os testes.
- **Worktree por ticket:** cada ticket do par roda num worktree do Orca, com pasta e `Library/` próprias, fora do repositório (`C:/Users/gabri/orca/workspaces/SpellBook/wt-<ticket>`). A raiz do worktree é o projeto host dele (0027), e a trava `.claude/editor.lock` fica na raiz de cada worktree. Isso aplica o item 4 da 0005 ("uma trava por projeto host") a dois hosts; dentro de um worktree, a trava continua única. A 0006 continua Adiada, e o worktree vale só para este experimento.
- **Criação e remoção:** o Controlador cria o worktree com `orca worktree create`, a partir da branch do lote, numa branch local `gabrielctorres/wt-<ticket>`. Em seguida copia para o worktree os docs de planejamento ainda sem commit e as skills editadas, para que a Fábrica e o Examinador leiam a mesma Spec, as mesmas ADRs e as mesmas skills do checkout do lote. Depois do cherry-pick, o Controlador remove o worktree com `orca worktree rm`, que apaga também a branch local.
- **Commit:** a Fábrica e o Examinador trabalham dentro do worktree. Depois do APROVADO, o DevOps commita no worktree, sem push, e leva o commit para a branch do lote por `git cherry-pick`, um commit por ticket, com push na branch do lote.
- **Suíte do par:** cada ticket do par foi revisado sem o outro. Depois do cherry-pick do segundo, o Examinador roda a suíte completa uma vez no checkout do lote, sob a trava desse checkout. Se falhar, o par volta ao Controlador. O usuário aceitou essa rodada a mais em 2026-11-01.
- **Memória:** a máquina tem 16 GB, e duas Unity em batchmode com `Library/` separadas podem esgotá-la. Se uma execução cair por memória (processo morto pelo sistema, erro de alocação ou log cortado sem resultado), o experimento acaba e o lote volta para o WIP 1 na hora. O ticket que caiu roda de novo sozinho.
- **Onde ficam os pares:** a Spec do lote marca cada par na tabela de ordem dos tickets, ao lado das dependências, e diz por que os dois cabem nas condições acima. Ticket sem par marcado na Spec segue o WIP 1. Os tickets sem Unity seguem a emenda de 2026-10-29.
- **O que não muda:** os limites de tamanho e a contagem, o paralelo sem Editor (emenda de 2026-10-29) e, fora do par, a proibição de pegar um segundo ticket com outro em implementação ou em revisão.

O resto desta ADR não muda.

## Emenda (2026-11-02): exceção única de linhas para o XR1 do B-019g

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do XR1 (issue #294, Spec `referencia-catalogo-e-api.md`). É uma **exceção única, não regra**, no mesmo molde da do VN10 e da do AV6.

- **O que:** o ticket **XR1** (mapa de referências cruzadas da API para a documentação da Unity) entra como um ticket só, com **599 linhas e 2 arquivos**, 19,8% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de arquivos não foi passado. A saída gerada (`api/reference/**` e `api/reference.sha256`) fica fora da contagem, pela emenda "saída de gerador commitada". A Spec estimava ~445 linhas e 2 arquivos.
- **Por que:** o ticket entrega um comportamento só (ler as referências externas, resolver o `href` de cada uma, com a versão de pacote da emenda de 2026-11-02 do D8, e escrever o mapa ordenado), que não se divide sem deixar a resolução de URL sem o `Render` que a testa de ponta a ponta. A maior parte do excesso veio dos testes da emenda do X6.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com os mesmos 2 arquivos e o tamanho corrigido para 599 linhas. Arquivo além dos 2 listados e da saída gerada só entra se seguir o critério de "arquivo fora do escopo" da emenda da margem (consequência direta do ticket), e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O XR2 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-02): experimento de WIP 2 estendido ao lote 8

Decisão do usuário em 2026-11-02. O experimento da emenda de 2026-11-01 ("experimento de WIP 2 com Unity no lote da referência") se repete no lote 8, com as mesmas regras. Continua sendo experimento: o usuário decide depois do lote 8 se ele vira regra, e o Arquiteto registra a decisão numa emenda nova.

Resultado no lote 7 (RC1 a RC8, XR1 e XR2, PR #306):
- O RC2 e o RC3, o único par com Unity marcado na Spec, rodaram cada um no seu worktree. O cherry-pick dos dois na branch do lote não teve conflito, a suíte do par passou, e nenhuma execução caiu por falta de memória.
- O RC5 rodou junto com o RC6, mas foi devolvido. O RC5 não usa a Unity, então esse par seguiu a emenda de 2026-10-29 (paralelo sem Editor) e não conta como dado do WIP 2. O lote 7 deu um par de WIP 2.

No lote 8:
- Valem os tickets do lote 8 e os pares marcados nas emendas de 2026-11-02 das Specs `gas-dano-escudo-e-morte.md`, `gas-evento-acoes-alvos.md` e `referencia-catalogo-e-api.md`. A composição proposta é DM7, EX1, DM8, EX2, EV1, EX4, EV2, EX5, EV3 e EV4a, com os pares DM8∥EX2, EV1∥EX4 e EV2∥EX5. Ela entra com o gate parcial dessas emendas. Se o gate mudar a composição, o Arquiteto refaz os pares nas Specs antes do primeiro ticket.
- Cada par junta um ticket que muda `.cs` do pacote (e regenera a saída gerada) com um ticket de exemplos do manual, que só muda testes, páginas e a guarda dos exemplos e não regenera nada. Dois tickets que mudam `.cs` do pacote continuam sem formar par, porque os dois escrevem `api/reference.sha256`.
- O worktree de cada ticket do par sai da branch do lote depois do cherry-pick do ticket anterior na ordem da Spec.
- O resto da emenda de 2026-11-01 vale sem mudança: as duas condições do par, o worktree por ticket, a criação e a remoção pelo Controlador, o commit por cherry-pick, a suíte do par depois do segundo cherry-pick, o fim do experimento na primeira queda por memória e o WIP 1 para ticket sem par.

O resto desta ADR não muda.

## Emenda (2026-11-02): composição do lote 8 aprovada

Decisão do usuário no gate parcial de 2026-11-02, com as perguntas L1, L2 e L3 da Spec `referencia-catalogo-e-api.md` respondidas como propostas. Completa a emenda anterior, que deixava a composição do lote 8 para esse gate.

- **Lote 8:** DM7 → EX1 → DM8∥EX2 → EV1∥EX4 → EV2∥EX5 → EV3 → EV4a. São 10 tickets e três pares de WIP 2. A composição e os pares são os que a emenda anterior propôs, e os motivos de cada par estão na seção "Lote 8 e pares do WIP 2" da `referencia-catalogo-e-api.md`.
- **Lote 9:** começa pelo EV4b. O EX3 (exemplos do `gas.md`) entra depois do EV7 (L1), e o EX6 (exemplos da página de dano e da migração, ticket novo com issue própria) entra depois do EX3 e do DM8 e apaga a lista de pendências da guarda dos exemplos (L2). Os pares do lote 9 são marcados nas Specs quando o lote for montado e seguem as regras do experimento, se o usuário decidir mantê-lo depois do lote 8.
- **O que não muda:** os limites de tamanho, a contagem e as regras do experimento de WIP 2 da emenda de 2026-11-01 e da anterior.

O resto desta ADR não muda.

## Emenda (2026-11-02): lote 8 revisto com o EV1 dividido

**Status da emenda: Aceita** (gate parcial de 2026-11-02, perguntas E1 e E2 da Spec `gas-evento-acoes-alvos.md` respondidas como propostas). A divisão do EV1 em EV1a e EV1b é decisão do usuário de 2026-11-02, tomada porque a implementação passou do teto (~805 linhas em 8 arquivos). Revê a emenda anterior, "composição do lote 8 aprovada".

- **Lote 8:** DM7 → EX1 → DM8∥EX2 → EX4 → EV1a∥EX5 → EV1b → EV2 → EV3. São 10 tickets e dois pares de WIP 2 (DM8∥EX2 e EV1a∥EX5). O EX4 entra sem par. Os motivos do par novo estão na revisão de 2026-11-02 da seção "Lote 8 e pares do WIP 2" da `referencia-catalogo-e-api.md`.
- **Lote 9:** começa pelo EV4a, seguido do EV4b. O resto da emenda anterior sobre o lote 9 (EX3 depois do EV7, EX6 depois do EX3 e do DM8) não muda.
- **O que não muda:** os limites de tamanho, a contagem e as regras do experimento de WIP 2. O experimento continua valendo só para os pares marcados nas Specs.

O resto desta ADR não muda.

## Emenda (2026-11-02): o WIP 2 depende do XR3

Decisão do usuário em 2026-11-02, depois do achado do Examinador na revisão do EX5, no par EV1a∥EX5. Completa a emenda de 2026-11-01 ("experimento de WIP 2 com Unity no lote da referência") e as do lote 8.

- **Achado:** o `ReferenceGeneratorTests` (XR2) e o `UnityXrefMapTests` (XR1) escrevem numa pasta fixa no `%TEMP%` do usuário e a apagam no `SetUp` e no `TearDown`. As duas Unity do par rodam com o mesmo usuário, então uma apaga a pasta da outra no meio do teste, e o `Generate_Twice_SameBytes` e o `Generate_WritesUnityXrefMap` falham de vez em quando. O worktree com pasta e `Library/` próprias, da emenda de 2026-11-01, não cobre arquivo escrito fora do projeto.
- **Dependência:** o experimento de WIP 2 depende do ticket **XR3** da Spec `referencia-catalogo-e-api.md` (emenda de 2026-11-02, backlog B-037). O XR3 põe a pasta desses testes no `Temp/` do projeto host e acrescenta uma guarda que falha em todo teste do pacote que use a pasta temporária do sistema.
- **Até o XR3 estar na branch do lote:** nenhum par novo de WIP 2 começa, e os tickets com Unity seguem o WIP 1. O lugar do XR3 na fila e o fim da revisão do EX5 são as perguntas R1 e R2 da mesma emenda da Spec.
- **Depois do XR3:** as duas condições do par continuam as mesmas. A guarda do XR3 cobre o caso "teste que escreve fora do projeto host" para todos os tickets, sem conferência a mais na montagem dos pares.
- **O que não muda:** as outras regras do experimento, os limites de tamanho e a contagem. Essa falha não foi por memória e não encerra o experimento pela regra de memória da emenda de 2026-11-01.

O resto desta ADR não muda.

## Emenda (2026-11-02): exceção única de arquivos para o EV2 do B-012

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois que a Fábrica contou o escopo do EV2 antes de editar (Spec `gas-evento-acoes-alvos.md`). É uma **exceção única, não regra**, no mesmo molde da do VN9 e da do XR1.

- **O que:** o ticket **EV2** (lado do efeito na base nova) entra como um ticket só, com **~490 linhas e 12 arquivos**, 20% acima do limite de 10 arquivos e dentro da margem (até 13). O teto de linhas não é passado. A Spec estimava ~430 linhas e 9 arquivos.
- **Por que:** `EffectPeriodTests.cs`, `EffectRemovalTests.cs` e `EffectRenewalTests.cs` derivam classes de `EffectAction` e usam as listas antigas do `GameplayEffect`. A troca das listas no EV2 quebra a compilação deles, então os três mudam no mesmo commit. A migração é mecânica (base `GameplayAction`, execução pelo `ActionContext`, nomes `OnApplied`, `OnTick`, `OnRemoved`) e os asserts não mudam. São consequência direta do ticket, pelo critério de "arquivo fora do escopo" da emenda da margem.
- **Como revisar:** o escopo é o da Spec corrigida, com os 12 arquivos e o que cada teste migra. Arquivo além desses só entra pelo mesmo critério, e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O EV3 segue a exceção própria de 2026-10-31, e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem de ~30%.

O resto desta ADR não muda.

## Emenda (2026-11-02): exceção do EV3 ampliada, EV3c e EV3d no lote 9

Decisão do usuário em 2026-11-02 (opção (c) e proposta WAIT da Spec `gas-evento-acoes-alvos.md`), depois que o Arquiteto recontou o EV3 no código de depois do EV2. Amplia a emenda de 2026-10-31 ("exceção única para o EV3 do B-012"), que continua sendo uma exceção única, não regra.

- **O que:** o EV3 continua um ticket só e passa a ter **~1150 linhas e 35 arquivos**, no lugar de ~1100 linhas e ~22 arquivos. A estimativa levada ao usuário era de ~33 arquivos; a lista fechada da Spec ficou com 35, porque a recontagem final achou o `manual/ferramentas/validador.md`, que cita `AbilityAction.CollectEffects`, e o `AbilityContextTests.cs`, que o ticket substitui por um `ActionContextTests.cs`. Não contam as três remoções puras (`AbilityAction.cs`, `AbilityContext.cs`, `EffectAction.cs`, regra I42) nem a saída gerada (emenda "saída de gerador commitada").
- **Por que:** a lista de 2026-10-31 deixava de fora o `Editor/GAS/ScalableFloatDrawer.cs` e parte dos testes que usam a base antiga ou o campo `GameplayAbility.Actions`. Todos quebram a compilação quando a base muda, então mudam no mesmo commit. Para o ticket não crescer mais, o EV3 faz só a troca, com o mesmo comportamento: a `WaitAction` fica com `float`, e saem dele as listas `OnEnd` e `OnCancel` (EV3c) e a `WaitAction` com `ScalableFloat` e a migração dos assets (EV3d).
- **Como revisar:** como na emenda de 2026-10-31. O Examinador revisa pela lista numerada de 35 arquivos do EV3 na Spec e pelos testes red nomeados nele. Arquivo fora da lista, além dos `.meta` e da saída gerada, faz a Fábrica parar e devolver, como no item 4 da Decisão. A margem de ~30% não se aplica por cima desta exceção.
- **EV3c e EV3d:** seguem os limites normais (~380 linhas e 8 arquivos; ~260 linhas e 7 arquivos).
- **Lote 8:** não muda. Fecha com o EV3 como 10º ticket, na ordem da emenda "lote 8 revisto com o EV1 dividido".
- **Lote 9:** começa por XR3 → EV3c → EV3d → EV4a → EV4b. O EV4a passa a depender do EV3d. O resto da emenda "lote 8 revisto com o EV1 dividido" sobre o lote 9 não muda.
- **Não vale para nenhum outro ticket.**

O resto desta ADR não muda.

## Emenda (2026-11-02): `ActionContext.cs` no EV3, lista com 36 arquivos

Decisão do usuário em 2026-11-02 (opção (a)), depois que a Fábrica devolveu o EV3 antes de editar. Ajusta a emenda anterior ("exceção do EV3 ampliada"), que continua sendo uma exceção única, não regra.

- **O que:** o EV3 passa a ter **~1200 linhas e 36 arquivos**. Entra o `Runtime/GAS/Actions/ActionContext.cs` (item 36 da lista da Spec), com a escolha do comparador do `CustomData` e a cópia dele no `CreateDerived`.
- **Por que:** o item 19 migra os seis casos do `AbilityContextTests` com os mesmos asserts, inclusive o do comparador mantido. O `ActionContext` de hoje não deixa escolher o comparador, e o `CreateDerived` o perde. Tirar ou afrouxar o caso fere a regra de testes do `CLAUDE.md`, então o comportamento do `AbilityContext` antigo vai para o `ActionContext` no mesmo ticket.
- **Como revisar:** como na emenda anterior, agora pela lista de 36 arquivos. A margem de ~30% continua sem valer por cima desta exceção.
- **Não vale para nenhum outro ticket.**

O resto desta ADR não muda.

## Emenda (2026-11-02): ponto de extensão `protected` no EV3, lista com 39 arquivos

**Status da emenda: Aceita** (gate parcial de 2026-11-02, aprovada pelo usuário). Proposta do Arquiteto, depois que a Fábrica devolveu o EV3 com a geração da referência da API falhando. Ajusta a emenda anterior ("`ActionContext.cs` no EV3"), que continua sendo uma exceção única, não regra.

- **O que:** o EV3 passa a ter **~1240 linhas e 39 arquivos**. Entram `Runtime/GAS/Actions/GameplayAction.cs`, `Runtime/GAS/Actions/ActionRunner.cs` e `Runtime/GAS/Effects/Actions/CommonEffectActions.cs` (itens 37 a 39 da lista da Spec), com o `Execute` e o `OnCancel` em `protected` e os dois métodos `internal` que o executor chama (emenda de 2026-11-02 da ADR 0034).
- **Por que:** com o `Execute` `protected internal`, o override do Bridges compila na Unity e quebra o docfx, e a forma que o docfx aceita quebra a Unity. A troca não pode esperar outro ticket, porque o EV3 tem de deixar a saída gerada atualizada e a geração precisa rodar. Os três arquivos mudam poucas linhas cada.
- **Como revisar:** como na emenda anterior, agora pela lista de 39 arquivos e pelo teste red `NoProtectedInternal_InActions`. A margem de ~30% continua sem valer por cima desta exceção.
- **Não vale para nenhum outro ticket.**

O resto desta ADR não muda.

## Emenda (2026-11-02): `ChargesTests.cs` no EV3, lista com 40 arquivos

**Status da emenda: Aceita** (gate parcial de 2026-11-02, aprovada pelo usuário). Proposta do Controlador, conferida pelo Arquiteto. Ajusta a emenda anterior ("ponto de extensão `protected` no EV3"), que continua sendo uma exceção única, não regra.

- **O que:** o EV3 passa a ter **~1245 linhas e 40 arquivos**. Entra o `Tests/GAS/PlayMode/ChargesTests.cs` (item 40 da lista da Spec). No `BornFull_DisabledSameAsToday`, a habilidade sem cargas ganha uma espera que não vence no teste. Só o preparo muda, e os asserts ficam como estão.
- **Por que:** pela regra de tempo do EV3 (R8), a habilidade sem ações termina na mesma chamada, e o teste, que esperava `AlreadyActive`, recebe `OnCooldown`. O preparo dependia do quadro que a corrotina dava à habilidade. Trocar ou tirar o assert fere a regra de testes do `CLAUDE.md`. A espera explícita devolve ao teste a pré-condição que ele já supunha, a habilidade ativa.
- **Como revisar:** como na emenda anterior, agora pela lista de 40 arquivos. O Examinador confere que as linhas 171 a 183 do teste não mudam e que o `CHANGELOG.md` (item 35) descreve a quebra da habilidade sem ações. A margem de ~30% continua sem valer por cima desta exceção.
- **Não vale para nenhum outro ticket.**

O resto desta ADR não muda.

## Emenda (2026-11-03): exceção única de linhas para o EV4b do B-012

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do EV4b (issue #178, Spec `gas-evento-acoes-alvos.md`). É uma **exceção única, não regra**, no mesmo molde da do AV6 e da do XR1.

- **O que:** o ticket **EV4b** (`AreaSelector` e fim da `TargetingStrategy`) entra como um ticket só, com **579 linhas e 10 arquivos**, 15,8% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de 10 arquivos não foi passado. Os três tipos apagados têm substituto no mesmo ticket e contam. A saída gerada (`api/reference/**` e `api/reference.sha256`) fica fora da contagem, pela emenda "saída de gerador commitada". A Spec estimava ~420 linhas e 9 arquivos.
- **Por que:** o ticket troca a estratégia pelo seletor de área de uma vez, com os testes de 2D, 3D, sorteio com semente e alocação, e não se divide sem deixar a `TargetingAction` com as duas formas de alvo. O 10º arquivo é o `manual/referencia/catalogo.md`, que ganha a linha do `AreaSelector` porque o catálogo lista todo tipo escolhível no Inspector. É consequência direta do ticket, pelo critério de "arquivo fora do escopo" da emenda da margem.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, corrigido para os 10 arquivos e 579 linhas. Arquivo além desses e da saída gerada só entra pelo mesmo critério, e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O EV5 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-03): detalhe na entrada da fila e documentação fora do ticket

**Status da emenda: Aceita** (decisão do usuário, aprovada pelo usuário em 2026-11-03). Vem com as emendas de 2026-11-03 da 0002 (Spec "just-in-time"), da 0007 e da 0040 (manual e referência só no passe de documentação).

- **Item 1 e item 4 da Decisão.** O escopo, os testes red e o tamanho que valem são os do "Detalhe (AAAA-MM-DD)" que o Arquiteto escreve na entrada do ticket na fila (emenda de 2026-11-03 da 0002). Uma estimativa antiga da Spec é rascunho. Passar dela não é motivo de devolução, e os limites e a margem de ~30% se aplicam ao Detalhe.
- **Páginas do manual.** Saem do escopo dos tickets de código. A regra de contagem delas (I35) e a da entrada do `nav` continuam valendo para os tickets do passe de documentação.
- **Saída gerada** (emenda "saída de gerador commitada"). Sai do diff dos tickets de código e aparece só no passe de documentação, com a mesma regra de contagem. Entre os testes do ticket (emenda "testes filtrados na implementação"), as guardas da categoria `Documentacao` não entram.
- **CHANGELOG.** Entra no escopo só do ticket que quebra algo, com a entrada de quebra e o passo de migração (emenda de 2026-11-03 da 0007, item 4). Na regra de "arquivo em comum" do WIP 2, o `CHANGELOG.md` conta só quando os dois tickets do par quebram algo.
- **WIP 2.** A frase "dois tickets que mudam `.cs` do pacote não formam par, porque os dois escrevem `api/reference.sha256`" deixa de valer. As outras condições do experimento continuam.

O resto desta ADR não muda.

## Emenda (2026-11-03): exceção única de linhas para o EV10 do B-012

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do EV10 (issue #184, Spec `gas-evento-acoes-alvos.md`). É uma **exceção única, não regra**, no mesmo molde da do AV6 e da do EV4b.

- **O que:** o ticket **EV10** (`SendGameplayEvent` e gatilhos) entra como um ticket só, com **583 linhas e 9 arquivos**, 16,6% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de 10 arquivos não foi passado. A saída gerada (`api/reference/**` e `api/reference.sha256`) fica fora da contagem, pela emenda "saída de gerador commitada". A Spec estimava ~480 linhas e 8 arquivos.
- **Por que:** o envio do evento, o índice de gatilhos na concessão e as travas contra laço (a habilidade que já está na tentativa e o limite de 8 disparos aninhados) só se testam juntos, e o ticket não se divide sem deixar o envio sem quem o receba. O 9º arquivo é o `AbilityTarget.cs`, que ganha a sobrecarga `AtUnit(unit, point)` porque o gatilho passa a unidade do evento como unidade-alvo com o ponto do evento (teste `Trigger_PassesEventTargetAsUnit`). É consequência direta do ticket, pelo critério de "arquivo fora do escopo" da emenda da margem.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, corrigido para os 9 arquivos e 583 linhas. Arquivo além desses e da saída gerada só entra pelo mesmo critério, e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O EV11 e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Emenda (2026-11-03): exceção única de linhas para o EV11a do B-012

Registrada pelo Arquiteto pela margem de ~30% da emenda "margem de ~30% registrada pelo Arquiteto", a pedido do Controlador (regra de exceção automática do usuário de 2026-11-01), depois da revisão do EV11a (issue #185, Spec `gas-evento-acoes-alvos.md`). É uma **exceção única, não regra**, no mesmo molde da do EV4b e da do EV10.

- **O que:** o ticket **EV11a** (reações a dano e morte) entra como um ticket só, com **606 linhas e 7 arquivos**, 21,2% acima do teto de ~500 linhas e dentro da margem (até ~650 linhas). O limite de 10 arquivos não foi passado. A Spec estimava ~490 linhas e 7 arquivos.
- **Por que:** o ticket já é a primeira metade da divisão do EV11 (2026-11-03). A base das reações (listas no efeito, `EffectReactions`, alvo padrão de verdade, `Change` no contexto) e os dois ganchos, com a trava de reentrada e a ordem fixa da P12, só se testam juntos: separar `OnTakeDamage` de `OnDeath` deixaria a ordem do golpe que mata sem teste. Nenhum arquivo entrou além dos 7 da Spec.
- **Como revisar:** a revisão já feita vale. O escopo é o da Spec, com os mesmos 7 arquivos e o tamanho corrigido para 606 linhas. Arquivo além dos 7 listados só entra se seguir o critério de "arquivo fora do escopo" da emenda da margem (consequência direta do ticket), e o Examinador o confere.
- **Não vale para nenhum outro ticket.** O EV11b e os demais tickets da Spec seguem ~500 linhas e 10 arquivos, com a margem da emenda "margem de ~30% registrada pelo Arquiteto".

O resto desta ADR não muda.

## Em aberto

Nada.

## Relacionadas

- 0002 (fluxo), 0004 (testes primeiro), 0006 (worktrees), 0008 (commits e versão), 0027 (estrutura do repositório).
