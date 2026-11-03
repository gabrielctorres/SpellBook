# PRD: Valores por nível e EffectSpec
Backlog: B-007 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção B ("O que falta": valores por nível e EffectSpec/EffectContext), seção D (lacunas 10, 11 e 12), seção E (Fase 3) e decisões I9 e I11. ADRs [0020](../adr/0020-valores-por-nivel-scalablefloat.md) (valores por nível) e [0021](../adr/0021-magnitude-configuravel-e-pilhas.md) (magnitude e pilhas), e a 0018 (contexto de execução).
Depende do Lote A (**B-002**: duração aplicada guardada na instância, `RemoveEffect` idempotente, recálculo sem alocação, reaplicação sem `CanStack` que renova; **B-006**: tags) e dos itens **B-003** (instância ativa com dados públicos e eventos) e **B-005** (ordem de composição dos modificadores e `startingEffects`). Este PRD usa o que eles entregam e não reescreve o escopo deles (ver seção 6).
Este PRD não tem foco em nenhum jogo. As menções a jogos no plano são histórico.

---

## 1. Problema (contexto)

O `AbilitySpec` tem um `Level`, e o `AbilityContext` o carrega, mas **nada o consome**. O efeito aplicado não sabe de qual habilidade veio, nem em qual nível, nem de onde partiu o impacto. Quem usa o pacote esbarra nisto:

- **Balancear por nível exige duplicar assets.** "Fireball nível 1 causa 50, nível 2 causa 75" vira dois efeitos, duas habilidades ou um script. Recarga, custo, duração e número de cargas também são constantes.
- **Não há tabela de valores da habilidade.** O designer não tem um lugar único onde "dano", "recarga" e "alcance" por nível vivem juntos e são lidos pela habilidade, pelo efeito e, depois, pela descrição.
- **O efeito não recebe dados de quem o aplica.** Não há como um chamador passar "dano = 120" ou "chance = 0,3" para um efeito genérico (o SetByCaller do Unreal). A solução de hoje é criar um efeito por valor ou ler `CustomData` por string.
- **Não há contexto do efeito.** Instigador, habilidade de origem, nível e ponto de impacto não chegam ao efeito aplicado. Um efeito que causa dano proporcional ao ataque de quem lançou não tem como saber quem lançou depois da aplicação.
- **Em efeitos com duração, o valor é calculado uma única vez (snapshot).** Não há opção de valor ao vivo ("ganha 10 % do meu ataque atual, enquanto durar").
- **As pilhas não mudam a magnitude.** Três pilhas de veneno causam o mesmo dano de uma. Quem precisa de "N vezes o valor" compensa com uma fórmula própria.
- **A habilidade não tem nível máximo nem aviso de mudança de nível.** O pacote não sabe até onde se evolui e a UI não sabe quando o nível mudou.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, pelo Inspector e sem script:

1. escrever um valor que **depende do nível** em dois formatos: uma lista ("10 8 6 4") ou uma curva;
2. reunir os valores nomeados de uma habilidade numa tabela (`AbilityValues`) e usá-los em campos de habilidade e efeito por `%nome`, escolhido numa lista (sem digitar);
3. aplicar um efeito que **carrega o contexto** (instigador, habilidade, nível, ponto de impacto) e valores passados pelo chamador;
4. escolher, **por modificador**, se o valor de um efeito com duração é fixo na aplicação (snapshot) ou recalculado quando algo de que ele depende muda (ao vivo);
5. fazer as pilhas multiplicarem a magnitude por padrão, com opção de desligar;
6. definir o **nível máximo** de uma habilidade e reagir à mudança de nível por evento.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Uma tabela por habilidade com valores por nível, `%nome` num dropdown, magnitude snapshot ou ao vivo num campo, pilhas que escalam o valor |
| **Desenvolvedor de gameplay** | Passar valores ao aplicar um efeito, ler o nível e o instigador no efeito, API de nível com evento, sem alocação por avaliação |
| **Desenvolvedor de UI e descrição** (B-008, B-015 a B-018) | Ler os valores por nível de uma habilidade e do efeito para mostrar "dano: 75" no tooltip |
| **Mantenedor do pacote** | Um único ponto de resolução de valor (constante, atributo, fórmula, tabela, nível, chamador) que o resto do GAS reutiliza |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, array:** `ScalableFloat` em array `10 8 6 4`. Nível 1 dá 10, nível 3 dá 6, nível 4 dá 4.
- **C2, acima do tamanho:** nível 6 num array de 4 valores dá 4 (repete o último, proposta; ver P3).
- **C3, curva:** `ScalableFloat` em curva avaliada no nível; o nível 2 dá o valor da curva em x = 2.
- **C4, tabela e `%nome`:** a habilidade tem `damage = 50 75 100 125` e `cooldown = 8 7 6 5`. Em nível 3, a recarga aplicada é 6 s e o dano do efeito é 100, usando `%cooldown` e `%damage`.
- **C5, nome inexistente:** `%dano` sem entrada na tabela é erro do validador (não existe lista que o permita) e, em runtime, vale 0 com um único aviso.
- **C6, nível chega ao efeito:** o projétil da habilidade em nível 3 aplica um efeito que lê o nível 3 do contexto.
- **C7, SetByCaller:** `ApplyEffect` com um valor chamador `"Dano" = 120` faz o modificador do efeito valer 120. Sem o valor, vale o padrão configurado.
- **C8, contexto:** o efeito aplicado conhece o instigador, a habilidade de origem, o nível e o ponto de impacto. Um tick do efeito usa "ataque do instigador" como fonte do valor.
- **C9, snapshot x ao vivo:** um buff `+10 % do ataque`. Em snapshot, subir o ataque depois da aplicação não muda o buff. Em ao vivo, muda, sem polling.
- **C10, pilhas:** com `+5` de ataque por pilha e 3 pilhas, o modificador vale `+15`. Com a multiplicação desligada, vale `+5`.
- **C11, nível máximo:** uma habilidade com `MaxLevel = 5`; subir para o nível 6 limita em 5 e avisa; `OnLevelChanged(spec, antes, depois)` dispara uma vez por mudança real.
- **C12, ciclo ao vivo:** A depende de B e B depende de A no modo ao vivo: o validador acusa o ciclo e, em runtime, o ciclo é cortado e avisado uma vez.
- **C13, duração por nível:** a duração de um efeito vem de `%duracao` e vale 4 s no nível 1 e 6 s no nível 3; renovar volta à duração aplicada (B-002, R6).
- **C14, efeito sem habilidade:** um efeito aplicado por código sem habilidade (nível e tabela ausentes) usa o nível padrão (ver P4) e `%nome` cai no padrão configurado.
- **C15, sem lixo:** avaliar um valor por nível e um `%nome` já resolvido 10 000 vezes não aloca memória gerenciada.

## 4. Requisitos funcionais

### 4.1 `ScalableFloat`

R1. **Tipo.** `ScalableFloat` é um tipo serializável (serialização nativa da Unity) que devolve um `float` a partir de um nível (inteiro, mínimo 1). Cada campo escolhe **um** dos dois modos:
- **Array:** um valor por nível, na ordem dos níveis.
- **Curva:** um `AnimationCurve` avaliado no nível.
Ele também aceita ser **constante** (um único valor), para campos que hoje são `float` migrarem sem perda.

R2. **Regras de nível.** O primeiro item do array é o **nível 1** (P1). Nível acima do tamanho do array repete o último valor (P3). Nível menor que 1 é tratado como 1. Array vazio dá 0 e o validador acusa. A curva é avaliada **no nível inteiro** (P2).

R3. **Edição.** No Inspector, o array pode ser digitado como texto ("10 8 6 4", separado por espaço, vírgula ou ponto e vírgula, cultura invariante) ou editado como lista, com uma **prévia por nível** (uma linha "nível → valor" até o nível máximo da habilidade, quando existir). Texto inválido não destrói o dado salvo: mostra o erro e mantém o último valor válido.

R4. **Sem alocação.** Avaliar um `ScalableFloat` não aloca memória gerenciada.

### 4.2 `AbilityValues` e `%nome`

R5. **Tabela por habilidade.** `GameplayAbility` tem uma tabela `AbilityValues`: uma lista de entradas **nome → `ScalableFloat`**. Os nomes são únicos na tabela (o validador acusa duplicados) e têm formato de identificador (letras, números e sublinhado; sem espaço). A tabela é a fonte de valores por nível lida por campos da própria habilidade e pelos efeitos aplicados por ela.

R6. **`%nome` nos campos numéricos.** Um campo numérico de habilidade ou de efeito (magnitude de modificador, custo, recarga, duração, número de cargas, alcance, entre os que a Spec listar) pode usar, além de constante, atributo e fórmula, **um valor da tabela por `%nome`** (ADR 0020, item 3). A escolha do nome é feita numa **lista no Inspector** com os nomes da tabela da habilidade; digitar um nome livre não é possível.

R7. **Resolução sem custo em regime.** O `%nome` é resolvido a um índice da tabela **na configuração (na criação do spec ou ao aplicar)**, não por comparação de texto a cada avaliação. Avaliar um `%nome` já resolvido não aloca nem compara texto (ADR 0020, "não podem fazer").

R8. **Validação do nome.** `%nome` inexistente na tabela da habilidade é **erro do validador** com o objeto clicável. Em runtime, um nome inexistente vale 0 e avisa **uma vez** no Console por nome. Um efeito usado por várias habilidades pode usar `%nome` se **todas** as habilidades que o referenciam tiverem o nome; o validador aponta as que faltam (P5).

### 4.3 Contexto do efeito (`EffectContext`) e `EffectSpec`

R9. **`EffectContext`.** É o conjunto de dados que acompanha um efeito aplicado: o instigador (ASC de origem), o alvo, a habilidade de origem (pode ser nula), o **nível**, o ponto de impacto (posição, quando existir) e os **valores do chamador** (R12). O contexto é criado na aplicação e **guardado na instância ativa**, de modo que ticks, remoção e valores ao vivo leem o mesmo contexto. Ele não é uma classe de estado da ação (ADR 0013, 0018): a ação lê, não guarda.

R10. **Nível chega ao efeito.** Quando a habilidade aplica um efeito (por `ApplyEffectAction`, impacto de projétil ou outra ação do GAS), o `EffectContext` leva o nível do `AbilityContext`. É o que a lacuna "o nível do contexto não chega ao efeito" pede.

R11. **`EffectSpec`.** A aplicação passa a poder receber um `EffectSpec` (um objeto leve que reúne o `GameplayEffect`, o `EffectContext` e os valores do chamador, como no Unreal), construído por código ou pelas ações. `ApplyEffect(efeito, origem)` existente continua funcionando e cria internamente um `EffectSpec` com contexto mínimo (sem habilidade, nível padrão; P4). A forma exata do `EffectSpec` (classe, struct, pool) é do Arquiteto (P6).

R12. **Valores do chamador (SetByCaller).** Quem aplica pode passar valores nomeados ao efeito. Um modificador do efeito escolhe, como origem do valor, "valor do chamador" com um **nome**; sem o valor passado, vale o **padrão** configurado no próprio modificador (e o validador avisa quando não há padrão). O identificador é por **nome** (P7). O valor do chamador é fixado na aplicação (não muda durante a vida do efeito, salvo reaplicação).

R13. **Efeito sem habilidade.** Um efeito aplicado fora de uma habilidade (por código, por `startingEffects` do B-005 ou pelo Debugger do B-003) tem contexto sem habilidade, **nível padrão 1** (configurável por campo do efeito, P4) e `%nome` sem tabela: vale o padrão configurado do modificador ou 0 com o aviso do R8.

R14. **Novas origens no `ModifierValueSource`** (ADR 0020, "Consequências"): além de Constante, Atributo e Fórmula, entram **valor da tabela (`%nome`)**, **nível** (o nível do contexto como número, para uso em fórmula ou multiplicador) e **valor do chamador**. Os valores novos entram **no fim** do enum (serializado como inteiro). O formato serializado muda e isso vai ao CHANGELOG (0009).

R15. **Atributo da origem.** Um modificador pode ler um atributo do **instigador** (quem aplicou) além do alvo, usando o `EffectContext` (cenário C8). Se o instigador foi destruído, o valor cai no último conhecido (snapshot) ou 0, definido pela Spec (P8), sem exceção.

### 4.4 Magnitude: snapshot ou ao vivo; pilhas

R16. **Modo de magnitude por modificador** (ADR 0021, item 1). Cada modificador de efeito com duração ou infinito escolhe **Snapshot** (calculado na aplicação e fixo; é o comportamento de hoje e o **padrão**) ou **Ao vivo** (recalculado quando muda algo de que o valor depende: atributo do alvo, atributo do instigador, nível, pilhas).

R17. **Ao vivo sem polling.** O recálculo ao vivo é disparado por **evento** das fontes (o evento do atributo, a mudança de pilhas, a mudança de nível), nunca por verificação a cada quadro (ADR 0021, "não podem fazer"). O custo é zero para modificadores em Snapshot.

R18. **Ciclos ao vivo.** Dependência circular entre atributos no modo ao vivo (A depende de B que depende de A) é detectada: o **validador** acusa quando é possível saber pelo asset, e em runtime o ciclo é **cortado** (a segunda avaliação usa o último valor) e avisado uma vez, sem laço infinito nem exceção (ADR 0021, "não podem fazer").

R19. **Ticks periódicos.** `PeriodicModifiers` avaliam o valor **no tick** (a cada período) com o contexto da instância; não há snapshot do valor do período, a menos que o modificador esteja em Snapshot (P9).

R20. **Pilhas multiplicam a magnitude** (ADR 0021, itens 2 e 3). Com N pilhas, o modificador vale **N vezes** o valor de uma pilha **por padrão**. Há uma opção **no efeito** (`StacksMultiplyMagnitude`, padrão ligado) para desligar a multiplicação quando a pilha só conta (P10). A mudança de pilhas recalcula o atributo afetado e dispara os eventos já existentes do atributo.

R21. **Quebra de comportamento registrada.** Efeitos empilháveis existentes passam a ter a magnitude multiplicada ao atualizar para a 3.0 (ADR 0021, "Consequências"). O CHANGELOG traz o aviso e o jeito de manter o comportamento antigo (desligar a opção do R20 nos efeitos afetados). O validador **não** acusa isso como erro.

R22. **Pilhas e ordem de composição.** A multiplicação das pilhas acontece **sobre o valor do modificador**, antes da ordem de composição de operações definida no B-005 (R12 daquele PRD). O resultado é coerente com `Add`, `Multiply` e as operações novas.

### 4.5 Nível máximo e mudança de nível

R23. **`MaxLevel`** em `GameplayAbility` (inteiro, mínimo 1, padrão 1). Definir o nível do `AbilitySpec` acima do máximo **limita** no máximo e avisa uma vez; abaixo de 1 limita em 1.

R24. **`OnLevelChanged`.** O `AbilitySpec` (ou o ASC, por habilidade; P11) dispara `OnLevelChanged(spec, nívelAntes, nívelDepois)` quando o nível muda de fato. Mudar para o mesmo nível não dispara. Esse evento é C# por instância (ADR 0016).

R25. **Efeitos ativos e a mudança de nível.** Um efeito ativo que depende do nível em modo **Ao vivo** é recalculado quando o nível do contexto muda; em **Snapshot**, não muda. O nível de um efeito já aplicado é o que a habilidade tinha **na aplicação**, salvo no modo ao vivo (P12).

R26. **API de nível.** O ASC oferece definir e ler o nível de uma habilidade concedida (`SetAbilityLevel`, `GetAbilityLevel`), com os limites do R23. `GiveAbility` aceita o nível inicial.

### 4.6 Uso dos valores em habilidade

R27. **Recarga, custo, duração e cargas por nível.** Os campos de recarga e custo (efeitos `Cooldown` e `Cost`) e a duração dos efeitos aceitam `%nome` e usam o nível do spec. A recarga aplicada continua passando pela redução de recarga (B-003, `CooldownReductionPolicy`) e guarda a **duração aplicada** do B-002. Cargas (`MaxCharges`) por nível são do B-009; aqui só é garantido que o mecanismo de `%nome` serve a elas.

R28. **Leitura para descrição e UI.** A API pública permite ler o valor de um nome da tabela para um nível dado (`GetValue(nome, nível)`), para a descrição (B-008) e a UI. Ela não aloca (R7) e devolve se o nome existe.

### 4.7 Editor, validação e documentação

R29. **Inspector** (UI Toolkit, ADR 0026, herdando do `SpellBookEditor`): o `ScalableFloat` com seletor de modo, entrada em texto e prévia por nível; a tabela `AbilityValues` com lista de nomes e valores; o seletor de `%nome` como lista; o modo de magnitude e a opção de pilhas no modificador e no efeito, com explicação curta. Um **controle de "nível de prévia"** na habilidade alimenta as prévias.

R30. **Validação** (`IValidatable`, 0026): array vazio; texto de array inválido; curva nula; nomes duplicados ou inválidos na tabela; `%nome` inexistente; efeito que usa `%nome` e é usado por habilidade sem o nome; valor do chamador sem padrão; ciclo ao vivo; `MaxLevel` menor que 1; modo ao vivo em efeito instantâneo (aviso; o modo não tem efeito). Mensagens clicáveis no topo do Inspector e na janela do validador.

R31. **Documentação.** A página de Habilidades descreve `ScalableFloat`, `AbilityValues`, `%nome`, `MaxLevel` e `OnLevelChanged` com o exemplo C4 (Fireball com `damage` e `cooldown`). A página de Efeitos descreve `EffectContext`, SetByCaller, snapshot x ao vivo, pilhas que multiplicam (com o aviso de migração do R21) e o efeito sem habilidade. Os tipos novos têm `[HelpURL]` (decisão I15).

R32. **CHANGELOG.** Cita: `ScalableFloat`, `AbilityValues`, `EffectContext`/`EffectSpec`, SetByCaller, novas origens do `ModifierValueSource` (formato serializado), magnitude por modificador, **pilhas multiplicando a magnitude por padrão (quebra)**, `MaxLevel` e `OnLevelChanged`.

## 5. Requisitos não funcionais

N1. **Alocação:** avaliar `ScalableFloat`, `%nome` resolvido, valor do chamador e magnitude ao vivo não aloca em regime (C15). Criar o `EffectContext` na aplicação pode alocar uma vez por instância, nunca por tick (a Spec avalia pool, P6).
N2. **Sem polling** no modo ao vivo (R17).
N3. **Módulos (ADR 0012):** tudo em `SpellBook.GAS`, que referencia só Core, Tags e Time. Nenhum tipo novo referencia Events, Pooling, Cues, Teams ou UI. O tempo vem do `ITimeSource` (ADR 0014).
N4. **Serialização:** nativa da Unity; campos novos têm padrão que reproduz o comportamento de hoje (constante, snapshot); o `ModifierValueSource` ganha valores só no fim; assets existentes abrem sem perda de dados, e `float` constantes migram para `ScalableFloat` constante sem mudar o número.
N5. **Determinismo:** testes com `ManualTimeSource` e factories (`GasFactory`), sem assets fixos.
N6. **Sem regressão:** a suíte do GAS existente passa sem edição, **exceto** os testes de efeito empilhável cuja magnitude muda pelo R20; a Spec lista esses testes e o CHANGELOG os cita.
N7. **Testabilidade da curva:** os testes de curva usam `AnimationCurve` criada por código.

## 6. Fora do escopo

- **Já entregue pelo Lote A:** duração aplicada na instância, `RemoveEffect` idempotente, recálculo sem alocação e reaplicação sem `CanStack` que renova (**B-002**); os tipos de tag (**B-006**). Este PRD só os usa; o R27 depende da duração aplicada.
- **Lista pública de efeitos, eventos de efeito, `CooldownReductionPolicy`, GAS Debugger:** **B-003**. O Debugger pode mostrar o nível do contexto depois, de forma aditiva.
- **Resultado de aplicação (`TryApplyEffect`) e relay:** **B-004**. `EffectSpec` e `TryApplyEffect` devem se combinar (a Spec decide a forma, P6), mas `TryApplyEffect` não é escopo deste item.
- **Ordem de composição (`MultiplyCompound`, `AddFinal`), limites dinâmicos, `OnDepleted`, `startingEffects` e metadados:** **B-005**. O R22 usa a ordem definida lá.
- **Descrição estruturada** (`EffectDescriber`, `DescriptionContext`, templates `%nome` na descrição, `ITextProvider`): **B-008**. Aqui só se entrega a API de leitura (R28).
- **Cargas de habilidade** (`MaxCharges` por nível, recarga sequencial) e `ActivationConditions`: **B-009**.
- **Dano, escudo e morte:** **B-010**; a ADR 0024 (pipeline de dano) usa o `EffectContext` daqui.
- **Hierarquia única de ações, alvo por ação e contexto único de execução:** **B-012** (ADR 0018). O `EffectContext` é desenhado para ser compatível com esse contexto, mas a unificação **não** entra aqui.
- **Políticas de pilha além da multiplicação** (agregação por origem, expiração por pilha, `StackingPolicy`): ADR 0021, "em aberto"; ficam para outro item.
- **Tabela de balanceamento** (habilidades × valores × nível) e simulador: **B-019**.
- **Progressão, XP e pontos de nível** (quem dá o nível, curva de XP): sem item no backlog. Aqui o nível é só um número definido por código.
- Salvar e carregar níveis e efeitos; rede.

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

**`ScalableFloat`**
- CA1 (T). Cenário C1: array `10 8 6 4` dá 10, 6 e 4 nos níveis 1, 3 e 4.
- CA2 (T). Cenário C2: nível acima do tamanho repete o último valor; nível 0 ou negativo vale como 1.
- CA3 (T). Cenário C3: curva avaliada no nível inteiro.
- CA4 (T). Modo constante devolve o mesmo valor em qualquer nível; um `float` antigo migra para constante sem mudar o número.
- CA5 (T). O texto do array aceita espaço, vírgula e ponto e vírgula com cultura invariante; texto inválido mantém o último valor válido e mostra o erro.
- CA6 (T). Array vazio dá 0 e o validador acusa.

**Tabela e `%nome`**
- CA7 (T). Cenário C4: recarga 6 s e dano 100 em nível 3 pelos nomes `cooldown` e `damage`.
- CA8 (T). Cenário C5: `%nome` inexistente é erro do validador e vale 0 com um único aviso por nome em runtime.
- CA9 (T). Nomes duplicados ou inválidos na tabela são acusados pelo validador.
- CA10 (T). Um efeito com `%nome` usado por uma habilidade sem esse nome é apontado pelo validador com o objeto clicável.
- CA11 (T). `GetValue(nome, nível)` devolve o valor e se o nome existe; nome inexistente devolve falso, sem aviso.
- CA12 (S). No host, o Inspector oferece os nomes da tabela numa lista; não é possível digitar um nome livre.

**Contexto e SetByCaller**
- CA13 (T). Cenário C6: o efeito aplicado pelo projétil da habilidade em nível 3 lê o nível 3.
- CA14 (T). Cenário C8: o efeito lê instigador, habilidade, nível e ponto de impacto; um tick usa o atributo do instigador.
- CA15 (T). Cenário C7: o valor do chamador define o modificador; sem ele, vale o padrão; sem padrão, o validador avisa.
- CA16 (T). Cenário C14: efeito aplicado sem habilidade usa o nível padrão e o padrão do modificador; `%nome` sem tabela vale o padrão ou 0 com aviso.
- CA17 (T). `ApplyEffect(efeito, origem)` continua funcionando e com o mesmo resultado de antes para efeitos sem os recursos novos.
- CA18 (T). Instigador destruído: o valor lido do instigador segue a regra da Spec (P8), sem exceção.
- CA19 (T). Os valores novos de `ModifierValueSource` estão no fim do enum (teste de ordem); um asset serializado com o valor antigo abre com o mesmo valor.

**Magnitude**
- CA20 (T). Cenário C9: em Snapshot, mudar o atributo-fonte não altera o buff; em Ao vivo, altera o buff sem nenhuma chamada de `Tick`.
- CA21 (T). O padrão de um modificador sem a marcação é Snapshot (comportamento de hoje); todos os testes antigos dão os mesmos valores.
- CA22 (T). Cenário C12: ciclo ao vivo é acusado pelo validador e cortado em runtime, com um único aviso e sem laço infinito.
- CA23 (T). Ticks periódicos avaliam o valor no tick com o contexto da instância (R19).
- CA24 (T). Cenário C10: com 3 pilhas o modificador vale 3 vezes; com a multiplicação desligada vale 1 vez; remover pilhas reduz o valor e dispara os eventos do atributo.
- CA25 (T). A multiplicação das pilhas é aplicada antes da ordem de composição (R22): casos com `Add` e `Multiply` dão o resultado esperado.
- CA26 (T). Depois do aquecimento, 10 000 avaliações de `ScalableFloat`, `%nome` resolvido e valor do chamador, e 1000 recálculos ao vivo, alocam 0 bytes gerenciados.
- CA27 (T). Modificador em Snapshot não assina nenhum evento (custo zero), verificado pela contagem de assinaturas.

**Nível**
- CA28 (T). Cenário C11: `MaxLevel` limita e avisa uma vez; `OnLevelChanged` dispara com antes e depois, só em mudança real.
- CA29 (T). `SetAbilityLevel` e `GetAbilityLevel` respeitam os limites; `GiveAbility` com nível inicial.
- CA30 (T). Um efeito Ao vivo que depende do nível é recalculado ao subir o nível; em Snapshot, não.
- CA31 (T). Cenário C13: duração por nível vale 4 s no nível 1 e 6 s no 3; renovar volta à duração aplicada (continuidade do B-002).

**Editor, validação, módulos e documentação**
- CA32 (S). No host, o Inspector mostra o `ScalableFloat` com seletor de modo, texto e prévia por nível; o modo de magnitude e a opção de pilhas aparecem no modificador e no efeito; o controle de "nível de prévia" atualiza as prévias.
- CA33 (T). O validador cobre todos os casos do R30, com o objeto clicável.
- CA34 (T). O teste de assemblies da 0-M continua passando: o GAS não referencia Events, Pooling, Cues, Teams nem UI.
- CA35 (E). O CHANGELOG cita os itens do R32, incluindo o aviso de migração das pilhas; as páginas de Habilidades e de Efeitos têm as seções do R31; os tipos novos têm `[HelpURL]`.
- CA36 (T). A suíte do GAS existente passa sem edição, exceto os testes de efeito empilhável listados na Spec (N6).

## 8. Perguntas em aberto

### Com o usuário

- **P1. O array começa no nível 1 ou no 0?** *Respondida (usuário, 2026-10-31): nível 1.*
- **P2. Curva em nível inteiro ou fracionário?** *Respondida (usuário, 2026-10-31): a curva é avaliada com nível inteiro.*
- **P3. Nível acima do array: repete o último ou é erro?** *Respondida (usuário, 2026-10-31): nível acima do array repete o último valor, sem erro (o validador continua avisando quando `MaxLevel` for maior que o array).*
- **P10. Opção de desligar a multiplicação das pilhas.** *Respondida (usuário, 2026-10-31): fica no efeito (`StacksMultiplyMagnitude`), não por modificador.*
- **P13. Migração das pilhas (R21).** *Respondida (usuário, 2026-10-31): na 3.0 as pilhas multiplicam, só com aviso no CHANGELOG, sem ferramenta de migração.*
- **P14. Tamanho do item.** *Respondida (usuário, 2026-10-31): o PRD fica inteiro; a Spec divide em tickets (sugestão: `ScalableFloat` e tabela → contexto, `EffectSpec` e SetByCaller → magnitude ao vivo e pilhas → `MaxLevel` e `OnLevelChanged`, com a quebra das pilhas em ticket isolado).*

### Com o Arquiteto (decisão técnica da Spec)

- **P4. Nível e tabela de efeito sem habilidade** (R13): confirmar o nível padrão (1) e onde se configura, e a resposta à "em aberto" da ADR 0020 sobre `startingEffects` do B-005.
- **P5. Efeito compartilhado por várias habilidades** com `%nome` (R8): como o validador sabe quais habilidades usam o efeito (varredura de assets) e o custo disso.
- **P6. Forma do `EffectSpec`** (R11): classe, struct ou objeto em pool; e como ele convive com `TryApplyEffect` do B-004 e com o contexto único de execução da ADR 0018 (decisão "em aberto" lá: um contexto único ou dois).
- **P7. SetByCaller por nome ou por tag** (R12; ADR 0020, "em aberto"): a proposta do Criativo é **nome** (mais simples, sem criar definição de tag por valor), com o mesmo mecanismo de lista no Inspector. A Spec confirma e define como o nome vira índice sem alocação.
- **P8. Instigador destruído** (R15): último valor conhecido ou 0, e quem guarda o último valor.
- **P9. Ticks periódicos** (R19; ADR 0021, "em aberto"): modo do modificador ou sempre no tick.
- **P11. Onde mora o evento `OnLevelChanged`:** no `AbilitySpec` ou no ASC (por habilidade).
- **P12. Nível de um efeito ativo:** o nível da aplicação ou o do spec ao vivo (R25), e o efeito sobre a duração aplicada do B-002.
- **P15. Como o modo ao vivo assina as fontes** sem alocação por assinatura e como cancela ao remover o efeito (ADR 0021, "consequências"), inclusive atributo do instigador de outro ASC.
- **P16. Ciclos ao vivo** (R18): detecção estática no validador (grafo de dependências entre atributos) e o corte em runtime (ADR 0021, "em aberto").

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-31 | Controlador | PRD escrito para o Lote B do backlog, a partir do texto de B-007, do plano (Fase 3) e das ADRs 0018, 0020 e 0021 | Todo o documento | Rascunho |
| 2026-10-31 | Controlador | Lote A vem antes: B-002 guarda a duração aplicada, remove com segurança e recalcula sem alocação; B-006 entrega as tags. Este PRD só os usa | R19, R27, seção 6 | Aplicado |
| 2026-10-31 | Controlador | Dependências registradas, sem puxar escopo: B-003 (instância e eventos), B-005 (ordem de composição, `startingEffects`), B-004 (`TryApplyEffect`), B-008 (descrição), B-009 (cargas), B-010 (dano), B-012 (contexto único de ações) | Seção 6 | Aplicado |
| 2026-10-29 | usuário | I9: `ScalableFloat` em array ou curva, tabela `AbilityValues` referenciada por `%nome` | R1 a R8 | Aplicado |
| 2026-10-29 | usuário | I11: magnitude configurável por modificador, pilhas multiplicando por padrão, com opção de desligar | R16 a R22 | Aplicado |
| 2026-10-29 | usuário | I1: 3.0 com quebras livres, sem fachada de compatibilidade | R14, R21, R32 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P1, P2, P3: array começa no nível 1; curva com nível inteiro; nível acima do array repete o último | R1, R2, seção 8 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P10: desligar a multiplicação por pilhas fica no efeito | R20, seção 8 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P13: pilhas multiplicam na 3.0, só com aviso no CHANGELOG | R21, R32, seção 8 | Aplicado |
| 2026-10-31 | usuário (via Controlador) | P14: PRD inteiro; a Spec divide em tickets | Seção 8 | Aplicado |
