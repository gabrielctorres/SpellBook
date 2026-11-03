# PRD: Atributos ricos
Backlog: B-005 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 1, 6, 8 e 13), seção E (Fase 2) e seção H.9 (metadados `IsBuff` etc. e `TextRoleTheme`).
Depende do Lote A: **B-002** (limitar o valor base e recalcular sem alocação; este PRD **não** refaz isso) e **B-006** (tags; usadas só se a Spec precisar). Depende também de **B-003** para os eventos e a lista de efeitos que `startingEffects` e os metadados usam (ver seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

Depois que o B-002 limita o valor base entre o mínimo e o máximo, o atributo ainda é pobre para quem monta um jogo de verdade:

- **Os limites são constantes.** Vida máxima que muda com equipamento, mana máxima que cresce com o nível: o `MaxValue` do `AttributeDefinition` é um número fixo, e quem precisa de limite dinâmico sincroniza à mão (um script copia "MaxVida" para o máximo da "Vida" a cada mudança). O B-002 (R2) deixou os limites constantes de propósito e aponta para este item.
- **Mudar o máximo não tem regra.** Se a vida máxima sobe de 100 para 150, a vida atual deve ficar em 100, ir a 150 ou manter a proporção? Hoje nem existe a pergunta.
- **Não há aviso de "chegou a zero" nem de "chegou ao máximo".** A UI, a morte, o "mana cheia" e o fim de uma regeneração são feitos por polling sobre `OnValueChanged`.
- **A composição dos modificadores é inconsistente.** Em efeitos com duração, `Multiply` soma `(1 + Σv)`. Em efeitos instantâneos e em ticks, ele compõe sobre a base. Não há um multiplicador que componha (produto) nem um bônus aplicado depois de tudo. Quem descreve o efeito para o jogador precisa modelar a inconsistência.
- **O atributo não sabe se mostrar.** O `AttributeDefinition` só tem ícone e nome. Formatar "+20 %", "1,5 s", "x2" ou "12,5" é código do jogo, repetido a cada projeto, com cultura fixa.
- **Não há cor semântica.** Bom, ruim, neutro e "valor" não têm um tema compartilhado; cada jogo espalha cores por atributo.
- **Não há efeitos de partida.** Para um personagem nascer com uma passiva ou com regeneração de vida, é preciso um script que aplique o efeito em `Start`.
- **Efeitos não dizem o que são.** Não há como marcar um efeito como benéfico, nocivo, escondido ou purgável, e a UI de buffs e o "purge" não têm de onde ler isso.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem código de sincronização:

1. definir o mínimo e o máximo de um atributo como **constante ou outro atributo**, com uma política para o que acontece com o valor atual quando o limite muda;
2. receber eventos quando um atributo chega ao mínimo (`OnDepleted`) e ao máximo (`OnReachedMax`);
3. escolher, por modificador, uma operação que **componha** (`MultiplyCompound`) ou que some **depois de tudo** (`AddFinal`), com ordem de cálculo documentada;
4. configurar no `AttributeDefinition` como o valor se mostra (tipo, casas decimais, prefixo, sufixo, "mais é melhor", categoria, visibilidade) e **formatar um valor** com uma chamada, com cultura injetada;
5. usar um tema de cores semânticas compartilhado (`TextRoleTheme`) em vez de cores por atributo;
6. dar ao ASC uma lista de **efeitos de partida** (`startingEffects`), aplicados ao ser criado;
7. marcar um efeito como benéfico, nocivo, escondido e purgável, e ler isso por código.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Limites que seguem outro atributo, comportamento claro ao mudar o máximo, formato e categoria do atributo no próprio asset, passiva de partida só arrastando o efeito |
| **Desenvolvedor de gameplay** | Eventos de zero e de máximo sem polling; cálculo previsível e documentado; `Format` pronto; sem script de sincronização de máximo |
| **Desenvolvedor de UI** (B-015 a B-018) | Formato, categoria, visibilidade e papel de cor lidos do asset; `IsBuff`/`IsHidden` para a lista de buffs |
| **Mantenedor do pacote** | Base de dados que a descrição (B-008) e a UI leem, sem decidir formato em cada módulo |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, máximo dinâmico:** `Vida` com máximo = atributo `VidaMax` (base 100). `VidaMax` sobe para 150 por um modificador; o máximo de `Vida` passa a 150. Cura de 200 leva `Vida` a 150, não a 100.
- **C2, política ao mudar o máximo:** `Vida` em 80 de 100, máximo sobe para 150. Com `KeepValue`, vale 80. Com `KeepRatio`, vale 120 (80 % de 150). Com `AddDelta`, vale 130 (80 mais a diferença de 50).
- **C3, máximo cai abaixo do valor:** `Vida` em 120 de 150, o máximo cai para 100: o valor atual (e a base) é limitado a 100, qualquer que seja a política, e dispara a mudança de valor uma única vez.
- **C4, mínimo dinâmico:** atributo com mínimo = outro atributo; o valor nunca fica abaixo dele.
- **C5, `OnDepleted`:** dano que leva `Vida` ao mínimo dispara `OnDepleted` uma vez; mais dano no mínimo não dispara de novo; curar e depletar de novo dispara outra vez.
- **C6, `OnReachedMax`:** mana regenerando até o máximo dispara `OnReachedMax` uma vez; ficar no máximo não repete.
- **C7, composição:** base 100, `Add` +20, `Multiply` +0,5, `MultiplyCompound` ×1,1 e ×1,2, `AddFinal` +5 dão `((100 + 20) × (1 + 0,5)) × 1,1 × 1,2 + 5 = 242,6`.
- **C8, `Override` por último:** com um `Override` de 50 presente, o resultado é 50 (depois do `AddFinal`), como hoje.
- **C9, formatar:** `Format` de 0,25 num atributo `Percent` com 0 casa dá "25%"; de 1,5 em `Seconds` com 1 casa dá "1,5 s" em pt-BR e "1.5 s" em en-US; `Multiplier` 2 dá "x2".
- **C10, beneficio:** `IsBeneficial(delta, op)` de -10 num atributo com "mais é melhor" desligado (por exemplo, "tempo de recarga") é verdadeiro.
- **C11, efeito de partida:** um ASC com `startingEffects = {Regeneracao}` aplica a regeneração ao iniciar e a vida passa a subir sem script.
- **C12, metadados:** um efeito marcado `IsDebuff` e `IsPurgable` é lido assim pela API; um efeito sem marcação é neutro, visível e não purgável.
- **C13, tema (S):** um `TextRoleTheme` define cores para `Beneficial`, `Harmful`, `Neutral` e `Value`; no host, mudar a cor no asset muda a cor lida pelo código.

## 4. Requisitos funcionais

### 4.1 Limites dinâmicos

R1. **Fonte do limite.** No `AttributeDefinition`, o mínimo e o máximo passam a ser, cada um, **constante ou outro atributo** (`MinSource` e `MaxSource`, no estilo do `ModifierValueSource` já existente). O padrão é constante, com os valores atuais. Um asset existente abre sem perda e se comporta como antes.

R2. **Cálculo.** O limite efetivo é lido do valor atual do atributo escolhido (com os modificadores dele). Quando o atributo do limite muda, o limite do dependente é recalculado e o valor do dependente é reajustado conforme R3 e R4, **sem polling** (por evento do atributo-fonte). O B-002, R1 (base limitada) continua valendo, agora contra o limite efetivo.

R3. **Política ao mudar o máximo.** Um campo `MaxChangePolicy` no `AttributeDefinition` com: `KeepValue` (o valor fica, limitado ao novo máximo), `KeepRatio` (mantém a proporção entre o valor e o máximo) e `AddDelta` (soma ao valor a diferença do máximo). O padrão é `KeepValue`, que reproduz o comportamento de hoje. A política vale **só quando o máximo muda depois da criação**; ela atua sobre o valor base (o que o B-002 já limita), e o valor atual segue pelo recálculo normal.

R4. **Máximo abaixo do valor.** Se o novo máximo (ou o novo mínimo) deixa o valor fora do intervalo, o valor é limitado ao limite, qualquer que seja a política. Se mínimo e máximo se cruzam (mínimo maior que máximo), vale o mínimo e o validador acusa a configuração.

R5. **Ciclos.** Um atributo não pode ter, direta ou indiretamente, a si mesmo como fonte de limite. O validador aponta o ciclo e o código **não** entra em laço infinito em runtime: o ciclo é cortado e avisado uma vez no Console. A fonte de limite não pode ser um atributo derivado que depende do próprio (a regra geral de ciclos de fórmula continua como está).

R6. **Atributo-fonte ausente.** Se o ASC não tem o atributo escolhido como fonte, o limite cai no valor constante e o pacote avisa uma vez no Console (sem exceção).

### 4.2 Eventos de limite

R7. **`OnDepleted`** (o valor chega ao mínimo) e **`OnReachedMax`** (o valor chega ao máximo) são eventos C# do `AttributeState`, por instância (ADR 0016). Disparam **na transição**: entrar no mínimo (ou no máximo) vindo de fora dele. Ficar no limite não repete; sair e voltar dispara de novo. Se o atributo já nasce no limite, o evento **não** dispara na criação.

R8. **Mudança de limite também dispara.** Se o mínimo ou o máximo muda e, por isso, o valor passa a estar no limite (por exemplo, o máximo cai e o valor é limitado), o evento da transição dispara. Se o valor já estava no limite e continua nele, não dispara.

R9. **Ordem.** O evento dispara depois de `OnValueChanged` (o valor já é o final). Um ouvinte que altera o próprio atributo dentro do evento não corrompe a iteração.

R10. O significado de morte para `OnDepleted` (e a absorção) é do B-010; aqui o evento é só "chegou ao mínimo".

### 4.3 Composição dos modificadores

R11. **Duas operações novas**, acrescentadas **no fim** de `ModifierOperation` (serializado como inteiro): `MultiplyCompound` e `AddFinal`.

R12. **Ordem de cálculo documentada e única:**
`((base + ΣAdd) × (1 + ΣMultiply)) × Π(1 + MultiplyCompound) + ΣAddFinal`, depois `Override` (o último aplicado vence), depois o limite entre mínimo e máximo. Sem `MultiplyCompound` nem `AddFinal` o resultado é **idêntico** ao do B-002, R3.

R13. **Efeitos instantâneos e ticks.** O que um efeito instantâneo ou um tick de efeito periódico altera na base segue a mesma ordem do R12 aplicada à base, de modo que o cálculo de uma operação seja o mesmo em efeito com duração, instantâneo e periódico. Isso resolve a inconsistência apontada no plano (item 8) e é uma **mudança de comportamento visível** (CHANGELOG, R27): o usuário aceitou a unificação (P2, respondida) e a Spec define a regra exata para `Multiply` instantâneo (P5).

R14. **Sem alocação.** O cálculo com as operações novas mantém a garantia do B-002 R3 (0 bytes em regime).

### 4.4 Exibição do atributo

R15. **`AttributeDisplay`**, uma struct serializável dentro do `AttributeDefinition`, com:
- `ValueKind` em `Flat`, `Percent` (0,25 = 25 %), `PercentPoints` (25 = 25 %), `Multiplier` (2 = x2) e `Seconds`;
- número de casas decimais, prefixo e sufixo;
- `HigherIsBetter` (padrão verdadeiro), ordem de exibição, `Hidden`;
- `Category` (um asset `AttributeCategory`, opcional);
- um **papel de texto** opcional para a cor (R19), em vez de uma cor por atributo.
Os campos atuais `Icon` e `DisplayName` continuam onde estão.

R16. **`Format`.** `AttributeDisplay.Format(valor, ModifierOperation, IFormatProvider cultura)` devolve o texto do valor segundo o `ValueKind`, as casas, o prefixo e o sufixo. Ele respeita o sinal por operação (por exemplo, `Add` positivo com sinal "+"; `Multiply` em porcentagem). **A cultura é injetada** (parâmetro; nulo cai na cultura invariante configurável), e **não** fica por atributo (decisão do item 13 do plano). Ele não aloca além do texto devolvido.

R17. **`IsBeneficial(delta, operação)`** diz se uma variação é boa ou ruim para o jogador, usando `HigherIsBetter` (por exemplo, um delta negativo em "tempo de recarga" é benéfico). Delta zero é neutro. Devolve também o papel (benéfico, nocivo, neutro) para a cor (R19).

R18. **Leitura pelo asset.** Quem constrói UI ou descrições lê `AttributeDisplay`, a categoria e `Hidden` por API pública somente leitura do `AttributeDefinition`. O consumo (barra, tooltip, descrição) é dos itens B-008 e B-015 a B-018.

### 4.5 Tema de cores (`TextRoleTheme`)

R19. **`TextRoleTheme`** é um `ScriptableObject` (no `SpellBook.Core`, ADR 0012) que mapeia **papéis** de texto para cores: `Beneficial`, `Harmful`, `Neutral` e `Value`. Há um tema padrão do pacote e o jogo pode criar o seu. Ele não conhece o GAS. A cor de um papel é lida por uma função pública sem alocação.

R20. **Sem cor por atributo.** `AttributeDisplay` guarda só o papel opcional; a cor sai do tema. Mudar o tema muda todos os atributos de uma vez.

### 4.6 Efeitos de partida

R21. **`startingEffects` no ASC:** uma lista de `GameplayEffect` aplicados quando o ASC inicia, **depois** do `AttributeSet` (para os modificadores terem onde atuar), com origem o próprio ASC. Efeito instantâneo, de duração e infinito são aceitos. Um efeito que o ASC não consegue aplicar (requisito, imunidade) registra um aviso de desenvolvimento e não interrompe os demais. A ordem da lista é a ordem de aplicação.

R22. **Nível e tabela de valores dos efeitos de partida** (o que o B-007 define) ficam fora deste item: aqui o efeito de partida é aplicado como um efeito qualquer, sem nível (ver seção 6 e P4).

### 4.7 Metadados de efeito

R23. **Campos no `GameplayEffect`:** `IsBuff`, `IsDebuff`, `IsHidden`, `IsPurgable`, todos booleanos, padrão falso, lidos por propriedade pública somente leitura (e também expostos pela instância ativa). `IsBuff` e `IsDebuff` não são exclusivos entre si; o validador avisa se os dois estiverem ligados. O padrão (todos falsos) significa "neutro, visível, não purgável".

R23a. **Nome e ícone do efeito** (decisão do usuário, 2026-10-31, vinda do B-018 P5; ticket AR7). O `GameplayEffect` ganha um **nome exibido** (texto padrão e chave opcional de texto, ADR 0023, a forma é do Arquiteto, B-008 P2) e um **`Sprite` de ícone**, ambos opcionais, lidos por propriedade pública somente leitura e também expostos pela instância ativa. Padrão vazio: o asset antigo abre sem perda e se comporta como antes. É o **único** lugar onde esses campos nascem; B-008, B-017 e B-018 só os leem. O Inspector do efeito os mostra junto dos metadados (R25); o manual de Efeitos os descreve (R27); o CHANGELOG os cita. Teste: leitura pela API pública no asset e na instância, padrão vazio, asset antigo abre sem perda.

R24. **Quem consome.** Os consumidores (lista de buffs com filtro por categoria, esconder efeitos `IsHidden`, ação de purge) ficam em B-018 e B-003. Este item **só entrega os dados** e a leitura (ver seção 6).

### 4.8 Editor, validação e documentação

R25. **Inspector** (UI Toolkit, ADR 0026, herdando do `SpellBookEditor`): o `AttributeDefinition` mostra a fonte do limite (constante ou atributo), a política, o bloco `AttributeDisplay` com uma **prévia** do `Format` para um valor de exemplo editável, a categoria e o papel de cor. O `GameplayEffect` mostra os quatro metadados agrupados. O ASC mostra `startingEffects`.

R26. **Validação** (`IValidatable`, 0026): mínimo maior que máximo (constantes), ciclo de fontes de limite (R5), fonte de limite apontando para o próprio atributo, `Decimals` negativo, `Hidden` em atributo sem categoria (aviso), `IsBuff` e `IsDebuff` juntos (aviso), `startingEffects` com entrada vazia. Mensagens clicáveis no topo do Inspector e na janela do validador.

R27. **CHANGELOG e manual.** O CHANGELOG cita: limites por atributo e política, `OnDepleted`/`OnReachedMax`, as duas operações novas e a nova ordem de cálculo (inclusive a mudança do R13), `AttributeDisplay.Format`, `TextRoleTheme`, `startingEffects` e os metadados. A página de Atributos do manual descreve limites, política, eventos, a fórmula do R12 com o exemplo C7 e o formato; a página de Efeitos descreve os metadados e `startingEffects`. Os tipos novos têm `[HelpURL]` (decisão I15).

## 5. Requisitos não funcionais

N1. **Alocação:** recalcular com limites dinâmicos e operações novas, e disparar `OnDepleted`/`OnReachedMax`, não aloca em regime (continuidade do B-002). `IsBeneficial` e a leitura do tema não alocam. `Format` só aloca o texto devolvido.
N2. **Módulos (ADR 0012):** limites, eventos, operações, `AttributeDisplay`, `startingEffects` e metadados em `SpellBook.GAS`; `TextRoleTheme` em `SpellBook.Core`. O GAS não referencia Events, Pooling ou UI.
N3. **Sem polling:** a atualização de limites dinâmicos é por evento do atributo-fonte, nunca por verificação a cada quadro (alinhado à ADR 0021 para o modo "ao vivo").
N4. **Serialização:** nativa da Unity; campos novos têm padrão que reproduz o comportamento de hoje; `ModifierOperation` ganha valores só no fim; assets existentes abrem sem perda de dados.
N5. **Determinismo:** testes com `ManualTimeSource` e factories, sem assets fixos.
N6. **Sem regressão:** a suíte do GAS (com o que o B-002 entrega) passa sem edição de teste antigo, salvo os casos de `Multiply` instantâneo cuja mudança está descrita no R13 e no CHANGELOG, **que o Arquiteto lista explicitamente na Spec**.

## 6. Fora do escopo

- **Já entregue pelo Lote A:** limitar o valor base entre mínimo e máximo constantes e recálculo sem alocação (**B-002**, R1 a R4); os tipos de tag (**B-006**).
- **Lista pública de efeitos ativos, eventos de efeito aplicado/removido, purge por categoria, `ReduceCooldown`:** **B-003**. `startingEffects` aplica efeitos pelo caminho normal; `IsPurgable` só é dado.
- **Resultado de aplicação (`TryApplyEffect`) e relay:** **B-004**. O relay pode repassar `OnDepleted`/`OnReachedMax` depois (aditivo).
- **Magnitude ao vivo, pilhas que multiplicam, nível do efeito, `ScalableFloat`, `AbilityValues` e SetByCaller:** **B-007**. Os efeitos de partida não têm nível aqui (P4).
- **Descrição estruturada** (`EffectDescriber`, `IModifierTextFormatter`, `ITextProvider`) e elementos de custo e recarga: **B-008**. Este item só entrega o `AttributeDisplay.Format` e o tema, que o B-008 consome.
- **Widgets de UI** (barra, texto, lista de buffs) e o uso de `Hidden`, categoria e `IsBuff`: **B-015 a B-018**.
- **Dano, escudo, morte** e tratamento de `OnDepleted` como morte: **B-010**. Pré e pós-alteração de atributo (ganchos de processador): B-010.
- Cultura **por atributo** (descartada no plano; a cultura é injetada).
- Metadados de **atributo** além dos listados (por exemplo, ícones por estado).
- Atributos criados em runtime (lacuna 24 do plano), serialização de estado e rede.
- Limites dinâmicos por **fórmula** (expressão): só constante ou atributo.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**Limites dinâmicos**
- CA1 (T). Cenário C1: o máximo segue o atributo-fonte; a cura de 200 para em 150.
- CA2 (T). Cenário C2: `KeepValue`, `KeepRatio` e `AddDelta` dão 80, 120 e 130.
- CA3 (T). Cenário C3: com o máximo caindo abaixo do valor, o valor é limitado em todas as políticas, com uma única notificação de mudança.
- CA4 (T). Cenário C4: mínimo por atributo; o valor nunca fica abaixo dele.
- CA5 (T). Atributo com limite constante (assets existentes) se comporta exatamente como antes do item (mesmos testes do B-002 passam).
- CA6 (T). Atributo-fonte ausente: o limite cai no valor constante e há um único aviso no Console, sem exceção.
- CA7 (T). Ciclo A→B→A: sem laço infinito, um único aviso, e o validador aponta o ciclo.
- CA8 (T). Mínimo maior que máximo: vale o mínimo e o validador acusa.
- CA9 (T). A mudança do atributo-fonte atualiza o dependente sem nenhuma chamada de `Tick` (sem polling).

**Eventos de limite**
- CA10 (T). Cenário C5: `OnDepleted` dispara na transição, não repete no mínimo e dispara de novo após curar e depletar.
- CA11 (T). Cenário C6: `OnReachedMax` na transição e sem repetição.
- CA12 (T). Atributo que nasce no mínimo ou no máximo não dispara o evento na criação.
- CA13 (T). Máximo que cai e deixa o valor no limite dispara `OnReachedMax` (R8); valor que já estava no limite e continua não dispara.
- CA14 (T). `OnDepleted` dispara depois de `OnValueChanged`; um ouvinte que altera o atributo dentro do evento não lança exceção.

**Composição**
- CA15 (T). Cenário C7: resultado 242,6 (tolerância de ponto flutuante definida pela Spec).
- CA16 (T). Cenário C8: `Override` vence depois do `AddFinal`; o limite entre mínimo e máximo vem por último.
- CA17 (T). Sem `MultiplyCompound` e `AddFinal`, todos os casos numéricos do B-002 (CA6) dão os mesmos valores.
- CA18 (T). Os valores novos de `ModifierOperation` estão no fim do enum (teste de ordem); um asset serializado com a operação antiga abre com o mesmo valor.
- CA19 (T). Efeito instantâneo e tick seguem a ordem do R12 aplicada à base, nos casos definidos pela Spec (R13, P5).
- CA20 (T). Depois do aquecimento, 1000 recálculos com as quatro operações alocam 0 bytes gerenciados.

**Exibição e tema**
- CA21 (T). Cenário C9: `Format` para `Flat`, `Percent`, `PercentPoints`, `Multiplier` e `Seconds`, com casas, prefixo e sufixo, em pt-BR e en-US (a cultura é parâmetro).
- CA22 (T). `Format` com cultura nula usa a cultura invariante configurada, sem exceção.
- CA23 (T). Cenário C10: `IsBeneficial` com `HigherIsBetter` ligado e desligado, delta positivo, negativo e zero.
- CA24 (T). `AttributeDisplay` e a categoria são legíveis por API pública somente leitura; um `AttributeDefinition` antigo abre com `Flat`, 0 casas decimais e visível.
- CA25 (S). Cenário C13: `TextRoleTheme` com as quatro cores; mudar o asset muda o valor lido; existe um tema padrão no pacote.
- CA26 (T). A leitura de cor do tema e `IsBeneficial` alocam 0 bytes depois do aquecimento.

**Efeitos de partida e metadados**
- CA27 (T). Cenário C11: o efeito de partida é aplicado ao iniciar, depois do `AttributeSet`, e a regeneração sobe a vida sem script.
- CA28 (T). Um efeito de partida bloqueado por imunidade avisa e os demais são aplicados; a lista vazia não faz nada.
- CA29 (T). Cenário C12: os quatro metadados são lidos pela API pública no asset e pela instância ativa; o padrão é neutro, visível e não purgável.
- CA30 (T). O validador aponta `IsBuff` e `IsDebuff` juntos (aviso).
- CA30a (T). R23a: `DisplayName` (texto e chave) e `Icon` do efeito são lidos pela API pública no asset e na instância ativa; padrão vazio; um asset antigo abre sem perda de dados. O Inspector do efeito, o manual de Efeitos e o CHANGELOG os citam (R25, R27).

**Editor, validação, módulos e documentação**
- CA31 (S). No host, o Inspector do `AttributeDefinition` mostra fonte do limite, política e a prévia do `Format`; o do `GameplayEffect` mostra os metadados; o do ASC mostra `startingEffects`.
- CA32 (T). O validador aponta: mínimo maior que máximo, ciclo de fonte, `Decimals` negativo, `startingEffects` com entrada vazia, com o objeto clicável.
- CA33 (T). O teste de assemblies da 0-M continua passando: `TextRoleTheme` está em `SpellBook.Core`, e o GAS não referencia Events, Pooling nem UI.
- CA34 (E). O CHANGELOG cita os itens do R27 (incluindo a mudança do R13); o manual de Atributos e o de Efeitos têm as seções do R27; os tipos novos têm `[HelpURL]`.
- CA35 (T). A suíte do GAS existente passa sem edição; as exceções (R13) estão listadas na Spec e no CHANGELOG (N6).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Tamanho do item.** *Respondida (usuário, 2026-10-31): o PRD fica inteiro; a Spec divide em tickets (sugestão: limites e eventos primeiro, por causa do B-010; composição depois; exibição e tema por último, por causa do B-008).*
- **P2. Mudança do `Multiply` instantâneo (R13).** *Respondida (usuário, 2026-10-31): unificar o `Multiply` instantâneo na nova ordem do R12, com aviso no CHANGELOG.*
- **P3. Política padrão ao mudar o máximo.** *Respondida (usuário, 2026-10-31): `KeepValue` é o padrão; `AddDelta` (e `KeepRatio`) ficam como opção do designer.*

### Com o Arquiteto (decisão técnica da Spec)

- **P4. Nível e tabela dos efeitos de partida** (R22): o B-007 ainda não existe. Confirmar que, aqui, o efeito de partida é aplicado sem nível e que o B-007 acrescenta o nível do dono depois (ADR 0020, "em aberto", item 4).
- **P5. Regra exata do R13** para `Multiply`, `MultiplyCompound` e `AddFinal` em efeito instantâneo e em tick: qual fórmula aplicar sobre a base para ficar coerente com R12 e não perder o resultado de efeitos instantâneos sucessivos.
- **P6. Onde moram os limites dinâmicos:** o `AttributeState` assina o atributo-fonte (e quem desassina ao destruir), e como evitar alocação na assinatura (R2, N1, N3).
- **P7. `OnDepleted` e o piso do B-002:** a transição "chegou ao mínimo" usa o valor atual ou a base? (Decidir para não duplicar disparo quando o modificador de efeito com duração e o dano instantâneo agem no mesmo quadro.)
- **P8. Cultura padrão** quando nula (R16): invariante, a cultura atual da thread ou um campo de configuração global do pacote (e onde ele mora no módulo Core).
- **P9. Categoria** (`AttributeCategory`): asset no GAS ou no Core, e se `TextRoleTheme` fica no Core como no plano (ADR 0012).

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | usuário (via Controlador), B-018 P5 | Nome e ícone do `GameplayEffect` entram no B-005 (AR7), não no B-018. **PRD já aprovado no gate**: emenda aditiva R23a, a Spec (AR7) precisa incluí-la e o Arquiteto confirma | R23a e CA30a (novos); R25 e R27 passam a cobrir os dois campos (Inspector, manual) | Aplicado no PRD; Spec AR7 a emendar |

Linhas anteriores:

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote B do backlog, a partir do texto de B-005 e do plano (itens 1, 6, 8, 13) | Todo o documento | Rascunho |
| 2026-10-31 | Controlador | Lote A vem antes: B-002 limita a base com mínimo e máximo constantes e deixa os dinâmicos para este item (R2 do B-002). Este PRD não refaz o clamp da base | R1 a R4, seção 6 | Aplicado |
| 2026-10-31 | Controlador | Dependências registradas, sem puxar escopo: B-003 (lista de efeitos e purge), B-007 (nível dos efeitos de partida), B-008 (descrição), B-010 (dano e morte), B-015 a B-018 (UI) | Seção 6 | Aplicado |
| 2026-10-29 | usuário | Plano, item 13: formato por atributo e cultura injetada, não por atributo | R16 | Aplicado |
| 2026-10-29 | usuário | I1: 3.0 com quebras livres, sem fachada de compatibilidade | R13, R27, N6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P1: PRD inteiro; a Spec divide em tickets | Seção 8 (P1) | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P2: unificar o `Multiply` instantâneo na nova ordem, com aviso no CHANGELOG | R13, R27, CA19, seção 8 (P2) | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P3: política padrão `KeepValue`; `AddDelta` como opção | R3, seção 8 (P3) | Aplicado |
