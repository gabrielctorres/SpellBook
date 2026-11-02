# GAS {#gas}

O GAS (Gameplay Ability System) é o sistema modular e orientado a dados do SpellBook para
habilidades, efeitos e atributos. Esta página é o guia de uso.

O GAS fica no assembly `SpellBook.GAS`. Um asmdef seu que usa o GAS precisa referenciar o
`SpellBook.GAS` (e o `SpellBook.Core`, se usar tipos do Core). Veja [Conceitos](../inicio/conceitos.md#modulos).

## 1. Visão geral {#visao-geral}

### 1.1 Tags (GameplayTags) {#tags}
- **O que são:** identificadores hierárquicos (ex: `State.Stunned`, `Ability.Fireball.Cooldown`).
- **Uso:** controle de fluxo. Uma habilidade pode ser bloqueada se o personagem tiver a tag `State.Stunned`.

### 1.2 Atributos {#attributeset}
- **O que são:** valores como Vida, Mana e Velocidade, agrupados em um `AttributeSet`.
- **Leitura sem aviso:** `TryGetValue`, `GetValueOrDefault(def, fallback)`, `HasAttribute` e `GetBaseValue` leem o atributo sem escrever no Console quando ele não está em `startingAttributes` (falso, o `fallback` ou 0). `GetValue` continua avisando uma vez por atributo e devolvendo 0. O ASC tem os mesmos atalhos: `TryGetAttributeValue`, `GetAttributeValueOrDefault`, `HasAttribute` e `GetAttributeBaseValue`. `TryGetValue` não aloca.
- **Listagem:** `AttributeSet.States` lista os estados na ordem de `startingAttributes`, e `AttributeState.Modifiers` lista os modificadores ativos de cada um, com o `Source` de quem os aplicou. As leituras já funcionam antes do `Awake` do `AttributeSet`, então um componente pode lê-lo no próprio `Awake` ou `OnEnable` sem depender da ordem dos componentes.
- **Contagem de tags:** `asc.GetTagCount(tag)` devolve quantas fontes concedem a tag exata (sem hierarquia; 0 se ausente ou inválida), e `asc.TagCounts` expõe todas as tags com a contagem de cada uma.
- **Desempenho:** recalcular um atributo, adicionar ou remover um modificador e remover os modificadores de uma fonte não alocam memória gerenciada depois do primeiro uso. Um efeito periódico com valor constante ou lido de atributo tiqueta sem gerar lixo. Um modificador com `ValueSource` do tipo `Formula` pode alocar, conforme a fórmula.

#### AttributeDefinition {#attributedefinition}
Cada valor tem uma `AttributeDefinition`.

- **Limites:** `MinValue` e `MaxValue` valem para o valor base e para o valor atual. O valor base nunca sai do intervalo: um dano maior que a vida deixa a base em `MinValue`, e a cura seguinte conta inteira. Isso vale para efeitos instantâneos, tiques periódicos e `SetBaseValue`.
- **Limite vindo de outro atributo:** com `MinSource` ou `MaxSource` em `Attribute`, o limite é o valor atual (com modificadores) do atributo escolhido em `MinAttribute` ou `MaxAttribute`, no mesmo ASC. Exemplo: a `Vida` com máximo = `VidaMax`; com `VidaMax` em 150, uma cura de 200 para em 150. `AttributeState.MinValue` e `MaxValue` dão o limite efetivo. O dependente se atualiza quando a fonte muda, sem esperar o `Tick`. O padrão é `Constant`, e um asset antigo se comporta como antes.
- **Política ao mudar o máximo (`OnMaxChange`):** vale quando o máximo vindo de outro atributo muda. Com a `Vida` em 80 de 100 e o máximo subindo para 150: `KeepValue` (padrão) deixa 80, `KeepRatio` mantém a proporção (120) e `AddDelta` soma a diferença (130). Se o novo máximo fica abaixo do valor, o valor vai ao máximo em qualquer política, com uma única notificação de `OnValueChanged`. Mudar o mínimo só limita.
- **Eventos de limite:** `AttributeState.OnDepleted` dispara quando o valor atual entra no mínimo e `OnReachedMax`, quando entra no máximo; os dois recebem o próprio `AttributeState`. Disparam só na transição: um dano que leva a `Vida` a 0 dispara `OnDepleted` uma vez, mais dano em 0 não repete, e curar e voltar a 0 dispara de novo. Um limite que muda e deixa o valor no limite também dispara (o máximo cai de 100 para 80 com a `Mana` em 80); quem já estava no limite e continua nele, não. O atributo que nasce no limite não dispara. A ordem é `OnValueChanged`, `OnDepleted` e `OnReachedMax`, e um ouvinte pode mudar o próprio atributo dentro do evento. Disparar os eventos não aloca.
- **Configuração inválida:** com o mínimo acima do máximo, vale o mínimo. Se o atributo-fonte não está no ASC, ou se as fontes formam um ciclo (A limitado por B e B por A), o limite cortado usa a constante (`MinValue` ou `MaxValue`, que funcionam como reserva) e o `AttributeSet` registra um aviso no Console, uma vez por atributo.
- **Valor inicial fora do intervalo:** o `AttributeSet` limita o valor inicial ao intervalo e registra um aviso no Console, uma vez por atributo, com o nome do atributo e do objeto. Revise o asset ou o `startingAttributes` apontado pelo aviso.
- **Validação e Inspector:** o [validador](../ferramentas/validador.md#regras) acusa erro no mínimo constante acima do máximo constante, na fonte `Attribute` sem atributo, na fonte que é o próprio atributo, no ciclo de fontes (A limitado por B e B por A), na fonte derivada cuja fórmula lê este atributo e no `Display.Decimals` negativo, e avisa do atributo `Hidden` sem categoria. Depois dos campos, o Inspector mostra um **Valor de exemplo** editável e o texto dele pelo `Display.Format`, na cultura do Editor, refeito ao mudar o exemplo ou o `Display`.
- **Dano, escudo e morte:** a lista `Processors`, o `Is Meta` e o `State Tag While Depleted`, na seção `Damage`, mudam o dano antes de chegar à base e marcam o atributo no mínimo. Depois deles, a seção **Dano** do Inspector mostra a prévia do dano. Veja [Dano, escudo e morte](dano-escudo-e-morte.md).

#### AttributeDisplay {#attributedisplay}
O campo `Display` do `AttributeDefinition` diz como o valor aparece na interface. Um asset antigo abre como `Flat`, 0 casas, visível e "mais é melhor".

- **Tipo (`Kind`, `ValueKind`):** `Flat` (número puro), `Percent` (fração: 0,25 vira "25%"), `PercentPoints` (25 vira "25%"), `Multiplier` (2 vira "x2") e `Seconds` (1,5 vira "1,5 s").
- **Casas, prefixo e sufixo:** `Decimals` (de 0 a 6; acima de 6 vale 6), `Prefix` e `Suffix` envolvem o texto: `Flat`, 2 casas, prefixo "PV " e sufixo " pts" dão "PV 1234,50 pts" em pt-BR.
- **`Format(valor, cultura)`:** devolve o texto do valor. A cultura é parâmetro; nula usa a cultura invariante ("1.5 s"), sem configuração global. O jogo passa a cultura dele, por exemplo `CultureInfo.GetCultureInfo("pt-BR")`.
- **`Format(valor, operação, cultura)`:** texto de um modificador. `Add` e `AddFinal` levam sinal ("+10", "-5"); `Multiply` e `MultiplyCompound` saem em porcentagem com sinal (0,5 vira "+50%"), sem prefixo nem sufixo; `Override` sai sem sinal, como o `Format` simples.
- **Direção boa:** `LowerIsBetter` ligado marca atributos em que menos é melhor, como tempo de recarga; `HigherIsBetter` é o contrário. `IsBeneficial(delta, operação)` diz se a variação é boa para o jogador (-10 numa recarga é benéfico) e `GetDeltaRole(delta)` devolve o `TextRole` (`Beneficial`, `Harmful` ou `Neutral` no zero) para a cor do [tema](#textroletheme). Os dois não alocam.
- **Interface:** `Order` ordena os atributos, `Hidden` os esconde da interface, `Category` aponta a [categoria](#attributecategory) e `Role` é o papel de cor do valor. O `AttributeDefinition` também expõe a categoria em `Category`, somente leitura.

#### AttributeCategory {#attributecategory}
Asset que agrupa atributos na interface, como "Ofensivo" e "Defensivo". Crie em **Create → GAS → Attribute Category**, preencha `DisplayName` e `Order` (menor vem primeiro) e escolha a categoria no `Display.Category` do atributo.

#### TextRoleTheme {#textroletheme}
Tema de cores por papel de texto, compartilhado pela interface em vez de uma cor por atributo. Fica no `SpellBook.Core` e não conhece o GAS.

- **Papéis (`TextRole`):** `Beneficial`, `Harmful`, `Neutral` e `Value`. `None` usa a cor de `Neutral`.
- **Leitura:** `theme.GetColor(TextRole.Harmful)` devolve a cor do papel, sem alocar.
- **Tema próprio:** crie um asset em **Create → SpellBook → Text Role Theme** e ajuste as quatro cores; o código passa a ler as cores novas.
- **Tema padrão:** `TextRoleTheme.Default` tem as cores do pacote (as mesmas de um tema novo). É criado sob demanda, sem asset em disco.

#### Fórmulas de modificadores {#formulas}
Os atributos mantêm um histórico de modificadores, permitindo cálculos precisos de bônus e penalidades. Há cinco operações (`ModifierOperation`):

| Operação | Efeito |
|---|---|
| `Add` | Soma na base. |
| `Multiply` | Percentual somado aos outros `Multiply` (0,5 = +50 %). Dois `Multiply` de 0,5 dão ×2, não ×2,25. |
| `MultiplyCompound` | Percentual que compõe com os outros (0,1 e 0,2 = ×1,1 × 1,2). |
| `AddFinal` | Soma depois de todas as multiplicações. |
| `Override` | O último aplicado vence, depois da fórmula. |

O valor sai sempre da mesma ordem:

`((base + ΣAdd) × (1 + ΣMultiply)) × Π(1 + MultiplyCompound) + ΣAddFinal`

Depois, se houver `Override`, vale o do último. Por último vem o limite entre o mínimo e o máximo. Exemplo: base 100, `Add` +20, `Multiply` +0,5, `MultiplyCompound` 0,1 e 0,2 e `AddFinal` +5 dão `((100 + 20) × 1,5) × 1,1 × 1,2 + 5 = 242,6`. Com um `Override` de 50 presente, o resultado é 50.

A ordem é a mesma em todos os tipos de efeito:

- **Com duração e infinito:** a fórmula roda sobre todos os modificadores ativos do atributo, partindo da base, a cada recálculo.
- **Instantâneo e tique periódico:** os modificadores **daquela aplicação** que miram o mesmo atributo passam pela fórmula uma vez, partindo da base atual, e o resultado vira a nova base (limitada). Aplicações sucessivas compõem: duas aplicações de um instantâneo com `Multiply` 0,5 levam 100 a 150 e depois a 225; um só instantâneo com dois `Multiply` 0,5 leva 100 a 200.

Uma fórmula calcula um valor a partir dos atributos de quem aplica (Source) e de quem recebe (Target). Ela serve no `Formula` de um atributo `Derived` e na fonte `Formula` de um modificador.

#### SimpleAttributeFormula {#simpleattributeformula}
Um valor base mais a soma de atributos multiplicados (`Base Value` + `Attribute` × `Multiplier`). Crie em **Create → GAS → Formulas → Simple Attribute Formula**.

#### ExpressionFormula {#expressionformula}
Uma expressão em texto, como `([ATK] * 2) / [DEF]`. Cada token entre colchetes é ligado a um atributo da Source ou da Target em `Mappings`. Crie em **Create → GAS → Formulas → Expression Formula**.

#### Valores por nível (ScalableFloat) {#scalablefloat}
`ScalableFloat` (`SpellBook.GAS.Values`) é um número que pode variar com o nível. Ele tem quatro modos:

- **Constant:** o mesmo valor em qualquer nível. Crie com `ScalableFloat.Constant(4f)`.
- **Array:** um valor por nível, e o primeiro é o nível 1. Com `ScalableFloat.FromArray(10, 8, 6, 4)`, o nível 1 vale 10, o 3 vale 6 e o 4 vale 4. Acima do tamanho do array, repete o último (nível 6 vale 4). Array vazio vale 0.
- **Curve:** uma `AnimationCurve` avaliada no nível inteiro (x = nível). Crie com `ScalableFloat.FromCurve(curva)`. Curva nula vale 0.
- **Table:** um valor nomeado da [tabela de valores](#abilityvalues) da habilidade de origem. Crie com `ScalableFloat.FromTable("Damage", fallback)`. Sem tabela (um efeito aplicado sem habilidade), vale o `Fallback` (padrão 0), sem aviso. Com tabela mas sem o nome nela, vale o `Fallback` e mostra um aviso no Console, uma vez por campo.

`Evaluate(nivel, tabela)` devolve o valor; a tabela é opcional e só o modo `Table` a lê. Nível 0 ou negativo vale como 1. O modo `Table` guarda o índice do nome e só o procura de novo quando a tabela muda (outra tabela ou `Version` nova). A avaliação não aloca memória gerenciada. Não há conversão implícita de `float`: o modo fica sempre explícito no código.

`ScalableFloat.TryParse("10 8 6 4", out valores, out erro)` lê o texto de um array. Os valores podem ser separados por espaço, vírgula ou ponto e vírgula, e o separador decimal é sempre o ponto (`1.5`), em qualquer idioma do sistema. Texto inválido devolve `false` e uma mensagem que cita o trecho errado.

`Validate(resultado, caminho)` acusa como erro um array vazio, uma curva não definida e o modo `Table` sem nome. Com `allowTable: false` (dentro da própria tabela), o modo `Table` também é erro. O asset dono do campo repassa a chamada com o nome do campo.

### 1.3 Gameplay Effects (GE) {#gameplayeffect}
- **Instant:** modifica o valor base (ex: Dano, Cura).
- **Duration/Infinite:** adiciona um modificador temporário ou permanente e pode conceder Tags enquanto ativo.
- **Duração:** `Duration` é um [`ScalableFloat`](#scalablefloat) (constante, array, curva ou tabela por nível), e não mais um `float`. O ASC avalia no nível e na tabela do [contexto](#effectcontext): `efeito.Duration = ScalableFloat.Constant(4f)` dura 4 s em qualquer nível. Assets 2.x com `Duration: 4` abrem como a constante 4, sem passo manual.
- **Valor do modificador:** na fonte `Value` (`SourceType.Value`, a antiga `Constant`), o valor é um [`ScalableFloat`](#scalablefloat) em `ValueSource.Value`, avaliado no nível e na tabela do [contexto](#effectcontext): `ValueSource.Value = ScalableFloat.Constant(-20f)`. As outras fontes estão em [Origens do valor](#origens-do-valor). Assets 2.x com `ConstantValue: -20` abrem como a constante -20. Para regravar os assets no formato novo, rode **SpellBook > Migrar assets do GAS (3.0)** (veja [Migração](../regras/migracao-2-para-3.md#valores-por-nivel)).
- **Tipos de dano:** `Damage Tags` marca o tipo do dano do efeito, como `Dano.Fogo`, e os processadores do atributo o usam para reduzir, anular ou absorver. Veja [Tipos de dano](dano-escudo-e-morte.md#tipos-de-dano).
- **Modificadores:** `AttributeEffectModifier` é uma classe. Uma entrada nula em `Modifiers` ou `PeriodicModifiers` é ignorada na aplicação, no tique, na remoção e no custo, e os outros modificadores valem.
- **Nome e ícone:** em **Identidade**, `DisplayName` (texto, vazio por padrão e nunca nulo) e `Icon` (`Sprite`, nulo por padrão) identificam o efeito na UI. A instância ativa não repete os campos: leia `instancia.Effect.DisplayName` e `instancia.Effect.Icon`. Assets antigos abrem com nome vazio e sem ícone.
- **Metadados:** em **Metadados**, `IsBuff`, `IsDebuff`, `IsHidden` e `IsPurgable` (somente leitura, também na instância ativa: `instancia.IsBuff`) são só dados para a UI e para quem purga efeitos; o ASC não os usa. O padrão, todos desligados, é "neutro, visível e não purgável". `IsBuff` e `IsDebuff` podem coexistir, mas o validador avisa.
- **Stacking:** suporta acúmulo de instâncias (ex: Veneno que acumula até 5 vezes).
- **Pilhas multiplicam a magnitude:** com `StacksMultiplyMagnitude` ligado (padrão), cada modificador do efeito, persistente ou do tique, vale o valor de uma pilha vezes `CurrentStacks`: `+5` de ataque com 3 pilhas vale `+15`. A multiplicação vem antes da [ordem de composição](#formulas), então três pilhas de `Multiply` 0,1 somam 0,3 no grupo `Multiply`: base 100 com `Add` 5 e `Multiply` 0,1 em 3 pilhas dá `(100 + 15) × 1,3 = 149,5`. Mudar as pilhas (reaplicação, `SetEffectStacks`, `RemoveEffectStacks`) recalcula o atributo e dispara o `OnValueChanged` dele antes do `OnEffectStacksChanged`. Desligue a opção quando a pilha só conta (para renovar a duração ou alimentar outra regra): o modificador vale uma pilha só.
  > **Migração (3.0):** efeitos empilháveis existentes abrem com a opção ligada e passam a multiplicar. Para manter o valor da 2.x, desligue **Stacks Multiply Magnitude** nesses efeitos.
- **Reaplicação e renovação:** aplicar de novo um efeito com duração ou infinito que já está ativo no ASC nunca cria uma segunda instância; `ApplyEffect` devolve a instância existente. Com `CanStack` ligado, ganha uma pilha (até `MaxStacks`) e, se `RefreshDurationOnStack`, renova a duração. Com `CanStack` desligado, só renova a duração, sempre. A renovação não reaplica modificadores nem tags e não roda `OnApplication` de novo, e o período continua contando de onde estava. A duração da renovação segue a regra **quem passa duração redefine; quem não passa mantém**: `ApplyEffect(efeito, duracao)` renova para `duracao` e a guarda como duração aplicada; `ApplyEffect(efeito)` volta à duração com que a instância foi aplicada (a do asset, se ninguém passou outra). Uma recarga aplicada com redução (ex: 6 s num asset de 8 s) volta a 6 s ao ser reaplicada.
- **Efeitos periódicos:** com `Period` maior que zero, o efeito dá um tique a cada `Period` segundos: aplica os `PeriodicModifiers` no valor base e roda as `OnTickActions`, sempre o mesmo número de vezes. Um efeito com duração dá `Duration / Period` tiques, arredondado para baixo (4 s com período de 0,5 s: 8 tiques; 4 s com período de 1,5 s: 2 tiques, em 1,5 s e 3 s). O tique que cai no fim da duração acontece antes da expiração, e nenhum tique roda depois da remoção, nem quando uma ação do tique remove a própria instância. Cada `Tick` do ASC dá no máximo um tique por efeito; o tempo de um `Tick` que passa do fim da duração não gera tique a mais.
- **Remoção:** `RemoveEffect(instancia)` roda as ações de limpeza e de `OnRemoval` e devolve as tags e os modificadores do efeito, uma vez só. Chamar de novo com a mesma instância, com `null` ou com uma instância de outro ASC não faz nada. Vale também quando a remoção explícita cai no mesmo quadro da expiração, ou quando uma ação de `OnRemoval` remove a própria instância.
- **Remoção em grupo:** `RemoveEffectsWithTag(tag)` remove os efeitos que concedem a tag ou uma filha dela, e `RemoveEffects(asset)` remove as instâncias de um `GameplayEffect`. As duas devolvem a soma das pilhas removidas (3 pilhas de veneno: 3) e 0 quando não há nada a remover; tag inválida ou asset `null` também dão 0.

#### Contexto e spec do efeito {#effectcontext}
Toda aplicação carrega um `EffectContext` (`SpellBook.GAS.Effects`), guardado em `ActiveEffectInstance.Context`:

| Campo | O que é |
|---|---|
| `Instigator` | Quem aplicou. Sem origem, o próprio alvo. |
| `Target` | Quem recebe o efeito. |
| `Ability` | Habilidade de origem; nula num efeito aplicado sem habilidade. |
| `Level` | Nível da aplicação: o da habilidade ou, sem ela, o `DefaultLevel` do efeito (padrão 1). |
| `HasImpactPoint`, `ImpactPoint` | Ponto de impacto, quando existe (o ponto de alvo da habilidade). |
| `CallerValues` | Valores por nome de quem aplicou (SetByCaller); nulo quando não há. |
| `Table` | A [tabela de valores](#abilityvalues) da habilidade; nula sem habilidade. |

Duração, modificadores, tiques e custo são avaliados com o nível e a tabela do contexto. Os tiques usam o contexto guardado na instância, e a duração aplicada não muda se o nível da habilidade mudar depois.

Para aplicar com contexto, monte um `EffectSpec` e passe por `in`:

```csharp
var valores = new SetByCallerValues();   // guarde e reutilize; Clear() não libera a memória
valores.Set("Dano", 120f);
EffectSpec spec = EffectSpec.Create(efeito, caster).WithCallerValues(valores);
target.TryApplyEffect(in spec, out ActiveEffectInstance instancia);
```

`EffectSpec.Create(efeito, instigador)` monta o spec sem habilidade; `EffectSpec.FromAbility(efeito, contextoDaAcao)` usa o instigador, a habilidade, o nível e o ponto do `ActionContext` (é o que a `ApplyEffectAction` faz, inclusive no impacto de projétil). `WithCallerValues`, `WithImpactPoint` e `WithDurationOverride` devolvem uma cópia alterada. `ApplyEffect(efeito, origem)` e as outras formas antigas continuam valendo, com o mesmo resultado. Aplicar um spec instantâneo não aloca.

##### Origens do valor {#origens-do-valor}
O `ValueSource` de um modificador diz de onde vem o valor (`ModifierValueSource.SourceType`):

| Fonte | Valor |
|---|---|
| `Value` | Um [`ScalableFloat`](#scalablefloat) no nível e na tabela do contexto. |
| `Attribute` | Um atributo do **instigador** (quem aplicou). |
| `Formula` | Uma [fórmula](#formulas) com o instigador como Source e o alvo como Target. |
| `SetByCaller` | O valor do chamador com o nome `CallerName`. |
| `TargetAttribute` | O atributo `TargetAttributeDefinition` do **alvo**. |

`SetByCaller` e `TargetAttribute` entraram no fim da lista, então assets antigos abrem com a mesma fonte.

- **SetByCaller:** o nome é texto e vem do código que aplica (letras, números e sublinhado). Sem o valor no spec, vale o `CallerDefault` quando `CallerHasDefault` está ligado; desligado, vale 0 e o Console avisa uma vez por modificador. Num efeito instantâneo, o valor é lido na aplicação. Num efeito com duração, a instância copia na aplicação os valores que os modificadores dela usam: mudar ou limpar o `SetByCallerValues` depois não muda o efeito, nem nos tiques. A busca é feita na aplicação, nunca por tique, e não aloca.
- **Atributo do instigador nos tiques:** um modificador periódico com fonte `Attribute` ou `Formula` lê o instigador a cada tique ("o tique causa o ataque atual de quem lançou"). Se o instigador foi destruído, o tique usa o último valor lido, ou o da aplicação se ainda não houve tique, sem exceção. Nos efeitos com duração, os `Modifiers` em [Snapshot](#magnitude-ao-vivo) já guardam o valor da aplicação.
- **Fora do ASC:** `ValueSource.GetValue(in contexto)` avalia como o ASC avalia, por exemplo com `instancia.Context`. `GetValue(origem, alvo)` avalia no nível 1, sem tabela e sem valores do chamador.

##### Snapshot ou ao vivo {#magnitude-ao-vivo}
Cada modificador de `Modifiers` num efeito com duração ou infinito escolhe em `Mode` (`MagnitudeMode`) quando o valor é calculado:

| Modo | Valor |
|---|---|
| `Snapshot` (padrão) | Calculado uma vez, na aplicação. Assets antigos abrem assim, com os mesmos valores. |
| `Live` | Recalculado quando muda algo de que ele depende, na hora e sem esperar o `Tick`. |

Um buff "+100 % do ataque de quem lançou" (fonte `Attribute`) em `Snapshot` fica com o ataque da aplicação; em `Live`, acompanha o ataque do instigador enquanto durar. O modo ao vivo assina eventos, sem verificar nada por quadro, e um modificador em `Snapshot` não assina nada (`instancia.LiveBindingCount` é 0). O que dispara o recálculo depende da fonte:

- `Attribute`: o `OnValueChanged` do atributo no instigador, mesmo em outro ASC;
- `TargetAttribute`: o `OnValueChanged` do atributo no alvo;
- `Formula`: os atributos do instigador que a fórmula declara em `GetSourceDependencies()`;
- `Value` com `ScalableFloat` em `Array`, `Curve` ou `Table`, num efeito aplicado por habilidade: o `OnAbilityLevelChanged` do instigador para essa habilidade. Ao subir o nível, o modificador passa ao valor do nível novo e o `Context.Level` da instância é atualizado; em `Snapshot`, nada muda. A duração aplicada não muda em nenhum dos modos.

`Value` constante e `SetByCaller` não dependem de nada que muda e não criam ligação. O recálculo não aloca, multiplica pelas pilhas como o valor da aplicação e dispara o `OnValueChanged` do atributo alterado. Com o instigador destruído, o modificador fica com o último valor. Remover o efeito desfaz as ligações.

- **Ciclos:** se dois modificadores ao vivo se leem (A depende de B e B de A) ou um lê o atributo que ele mesmo altera, a segunda reavaliação no mesmo ciclo é cortada: o modificador fica com o último valor e o Console avisa uma vez por modificador, sem laço infinito.
- **Onde não vale:** `Mode` não tem efeito em efeito instantâneo nem em `PeriodicModifiers`. Os tiques sempre avaliam o valor no tique, com o contexto da instância.

#### Resultado da aplicação {#resultado-da-aplicacao}
`ApplyEffect` devolve só a instância (ou `null`). Para saber o que aconteceu, use `TryApplyEffect(efeito, origem, out instancia)` ou `TryApplyEffect(efeito, duracao, origem, out instancia)`, que aplicam igual e devolvem um `EffectApplicationResult`. As checagens seguem esta ordem:

| Resultado | Quando | Instância |
|---|---|---|
| `Invalid` | Efeito `null`. Não lança exceção. | `null` |
| `MissingRequirements` | Falta ao alvo alguma das `ApplicationRequirements`. | `null` |
| `BlockedByImmunity` | O alvo tem alguma das `ApplicationImmunityTags`. | `null` |
| `AppliedInstant` | Efeito instantâneo aplicado. | `null` |
| `Stacked` | Reaplicação com `CanStack`, mesmo em `MaxStacks`. | a existente |
| `Refreshed` | Reaplicação sem `CanStack` (renovação). | a existente |
| `Applied` | Instância nova. | a nova |

`ApplyEffect` continua com a mesma assinatura e o mesmo retorno. O resultado e o `out` de `TryApplyEffect` não alocam; o caminho instantâneo e os bloqueios também não. Só `Applied` aloca, porque cria a instância nova, como `ApplyEffect`.

Nos dois bloqueios, o ASC dispara `OnEffectBlocked(efeito, origem, resultado)`, uma vez, com a origem já resolvida (origem `null` vira o próprio ASC). Efeito `null` não dispara. Para bloqueios de regras suas (silêncio, alvo fora de alcance), chame `asc.ReportBlocked(tagDoMotivo, origem)`: dispara `OnBlocked(tag, origem)`, ignora tag inválida e troca origem `null` pelo próprio ASC.

```csharp
if (target.TryApplyEffect(stun, caster, out _) == EffectApplicationResult.BlockedByImmunity)
    Debug.Log($"{target.name} é imune a {stun.name}");
```

#### Efeitos ativos {#efeitos-ativos}
`asc.ActiveEffects` lista as instâncias ativas na ordem de aplicação, sem cópia. Cada `ActiveEffectInstance` expõe `Effect` (o asset), `Source` e `Target` (os ASCs), `TimeRemaining`, `AppliedDuration` (a duração com que foi aplicada ou renovada, já com a redução de recarga), `CurrentStacks` e `IsActive` (falso depois da remoção). O tempo avança só pelo `Tick` do ASC, e as pilhas mudam pela reaplicação.

Para mudar a duração ou as pilhas de uma instância, chame o ASC alvo ou o atalho da própria instância (`instancia.ExtendDuration(2f)` chama `instancia.Target.ExtendEffectDuration(instancia, 2f)`). Todos devolvem se algo mudou e devolvem `false` para `null`, para instância removida ou de outro ASC.

- `ExtendEffectDuration(instancia, segundos)` soma ao tempo restante (5 s + 2 s = 7 s). `ReduceEffectDuration(instancia, segundos)` subtrai; se o restante chega a 0, o efeito expira na hora, com o motivo `Expired`. Segundos negativos ou `NaN` não mudam nada e geram um aviso no Console (só em build de desenvolvimento).
- `RestartEffectDuration(instancia)` volta o tempo restante à `AppliedDuration`, e não à `Duration` do asset (uma recarga aplicada com 3 s volta a 3 s). Não mexe no período.
- As três só valem para efeitos `HasDuration`; nos infinitos devolvem `false`. Nenhuma muda a `AppliedDuration`.
- `SetEffectStacks(instancia, quantidade)` grava as pilhas, limitadas a `[0, MaxStacks]` (9 num efeito de máximo 5 vira 5), e dispara `OnEffectStacksChanged`. A mesma contagem devolve `false` e não dispara. `RemoveEffectStacks(instancia, quantidade)` tira pilhas; quantidade 0 ou negativa devolve `false`.
- Pilhas em 0 removem a instância com o motivo `StacksDepleted`: primeiro devolve tags e modificadores, depois dispara `OnEffectStacksChanged(instancia, antes, 0)` e por último `OnEffectRemoved(instancia, StacksDepleted)`.
- As tags e os modificadores continuam um por instância. Com `StacksMultiplyMagnitude`, o valor de cada modificador acompanha a contagem, e o atributo é recalculado antes do `OnEffectStacksChanged`.

Para buscar, passe uma lista sua: o ASC limpa a lista, preenche com as instâncias na ordem de aplicação e devolve a quantidade. As buscas não alocam e lançam `ArgumentNullException` com a lista nula.

- `FindEffectsWithTag(tag, lista)`: efeitos que concedem a tag ou uma filha dela, como `HasTag` (buscar `Status` acha `Status.Burn`). Tag inválida não acha nada.
- `FindEffectsWithAnyTag(tags, lista)`: efeitos que casam com alguma tag de um `GameplayTagContainer`, lista ou array.
- `FindEffectsMatching(consulta, lista)`: efeitos cujas tags concedidas satisfazem uma [`GameplayTagQuery`](tags.md#gameplaytagquery).
- `FindEffects(asset, lista)`: as instâncias de um `GameplayEffect`.
- `HasEffectWithTag(tag)`: se algum efeito ativo concede a tag ou uma filha dela.

```csharp
private readonly List<ActiveEffectInstance> _found = new List<ActiveEffectInstance>();

int burns = asc.FindEffectsWithTag(statusBurn, _found);
for (int i = 0; i < burns; i++) Debug.Log($"{_found[i].Effect.name}: {_found[i].TimeRemaining:0.0} s");
```

### 1.4 Gameplay Abilities (GA) {#gameplayability}
As habilidades são compostas **dentro do próprio arquivo da habilidade**, como uma lista de ações.
Não é preciso criar ScriptableObjects separados para cada ação.

#### Níveis e tabela de valores {#abilityvalues}
`MaxLevel` (padrão 1) é o nível máximo da habilidade. O ASC limita o nível a `[1, MaxLevel]` em `GiveAbility(habilidade, nivel)` e em `SetAbilityLevel(habilidade, nivel)`; acima do máximo, mostra um aviso no Console, uma vez por habilidade. `GetAbilityLevel(habilidade)` devolve o nível, ou 0 se a habilidade não foi concedida. `AbilitySpec.Level` é só de leitura fora do pacote. `OnAbilityLevelChanged(spec, antes, depois)` dispara só quando o nível muda de fato.

`Values` (`AbilityValues`) é a tabela de valores nomeados da habilidade: cada entrada tem um nome e um [`ScalableFloat`](#scalablefloat). Um `ScalableFloat` em modo `Table` lê a entrada pelo nome, e várias partes da habilidade podem usar o mesmo número (o dano de 50/75/100, por exemplo).

```csharp
habilidade.Values.Add("Damage", ScalableFloat.FromArray(50, 75, 100));
habilidade.TryGetValue("Damage", 3, out float dano); // true, 100
```

- `Add` acrescenta, `Set` troca ou acrescenta e `Remove` tira uma entrada. Toda mudança sobe a `Version` da tabela; editar no Inspector também.
- O nome diferencia maiúsculas. `TryGetValue` com um nome que não existe devolve `false` e 0, sem aviso.
- Uma entrada não lê outra: ela é avaliada sem tabela.
- `Values.Validate` acusa nome vazio, nome com caractere fora de letras, dígitos e `_`, nome repetido e entrada em modo `Table`.

##### Exemplo: Fireball com `damage` e `cooldown` {#exemplo-fireball}
A Fireball tem `MaxLevel = 4` e duas entradas na tabela: `damage = 50 75 100 125` e `cooldown = 8 7 6 5`. A duração do efeito de recarga está em modo `Table` com o nome `cooldown`, e o valor do modificador de dano, em modo `Table` com o nome `damage`. Concedida em nível 3, a recarga aplicada é de 6 s e o dano é 100. Ao subir para o nível 4, a próxima recarga é de 5 s e o próximo dano, 125.

```csharp
fireball.Values.Add("damage", ScalableFloat.FromArray(50, 75, 100, 125));
fireball.Values.Add("cooldown", ScalableFloat.FromArray(8, 7, 6, 5));
fireball.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
asc.GiveAbility(fireball, 3); // recarga de 6 s, dano 100
```

##### No Inspector {#scalablefloat-inspector}
Todo campo `ScalableFloat` aparece como um grupo com o seletor **Modo** e, abaixo, só os campos do modo escolhido:

- **Constant:** o **Valor**.
- **Array:** o texto **Valores por nível** ("10 8 6 4", com espaço, vírgula ou ponto e vírgula, e ponto como separador decimal) e a **Lista** dos mesmos valores, para editar um a um. O texto é aplicado ao sair do campo ou ao apertar Enter. Texto inválido mostra o erro abaixo do campo e não apaga os valores salvos, que continuam os últimos válidos.
- **Curve:** a **Curva**.
- **Table:** o **Nome**, escolhido numa lista, e o **Fallback**. Não há campo de texto para o nome. A lista traz os nomes das tabelas das habilidades do projeto que usam o efeito (recarga, custo ou efeito aplicado por uma ação). Se nenhuma habilidade usa o efeito, a lista fica vazia e um aviso explica o motivo. A lista é refeita quando os assets do projeto mudam.

No fim do grupo, a **Prévia** mostra uma linha "nível → valor" do nível 1 até o maior `MaxLevel` das habilidades que usam o efeito. Sem habilidade, ela vai até o tamanho do array. No modo `Table`, a prévia lê a tabela da primeira dessas habilidades.

No Inspector da habilidade, depois dos campos, o **Nível de prévia** (limitado a `[1, MaxLevel]`) mostra cada valor da tabela e a recarga nesse nível, sem a redução do atributo. Na Fireball, em nível 3, a prévia mostra `damage = 100`, `cooldown = 6` e `Recarga = 6 s`.

#### Condições {#condicoes}
As condições do GAS herdam `Condition<ConditionContext>` (veja [Condições](../inicio/conceitos.md#condicoes) no Core) e ficam no namespace `SpellBook.GAS.Conditions`. São perguntas de sim ou não: não mudam o ASC, não guardam motivo de falha e não alocam ao avaliar.

O `ConditionContext` é o mesmo para toda condição do GAS e é somente leitura:

| Campo | O que é |
|---|---|
| `Owner` | ASC que avalia a condição. |
| `Ability` e `Level` | Habilidade em avaliação e o nível dela. A habilidade pode ser nula. |
| `Spec` | `AbilitySpec` da habilidade concedida, ou nulo. |
| `Target` | `AbilityTarget`: o ponto do alvo (`HasPoint` e `Point`), ou `AbilityTarget.None`. |
| `Targets` | Unidades-alvo. Nunca é nulo: sem alvos, é uma lista vazia. |

Condições prontas:

| Condição | Verdadeira quando | Chave padrão do motivo |
|---|---|---|
| `TagQueryCondition` | As tags ativas do `Owner` satisfazem a **Query** (todas, alguma e nenhuma, como a [`GameplayTagQuery`](tags.md#gameplaytagquery)). Sem `Owner`, é falsa. | `gas.activation.tag_query` |
| `HasTargetsCondition` | Há ao menos um alvo em `Targets`. Alvos destruídos não contam. | `gas.activation.has_targets` |
| `AttributeThresholdCondition` | O valor atual do **Attribute** no `Owner` compara com o **Value** pela **Comparison** (`Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual` ou `Equal`, com tolerância de 0,0001). Com **Use Ratio**, compara o valor dividido pelo máximo efetivo do atributo; máximo 0 ou menor dá falso. Atributo ausente no `Owner`, ou sem `Owner`, dá falso, sem aviso. | `gas.activation.attribute_threshold` |
| `HasTargetPointCondition` | A ativação tem um ponto-alvo (`Target.HasPoint`). | `gas.activation.no_target` |
| `CastRangeCondition` | O ponto-alvo está a até `CastRange` da habilidade, medido da posição do `Owner`. Com `CastRange` 0 ou menor, ou sem habilidade, é verdadeira, com ou sem ponto. Com alcance maior que 0 e sem ponto, é falsa. | `gas.activation.cast_range` |
| `AndCondition` | Todas as filhas são verdadeiras. Lista vazia é verdadeira. | — |
| `OrCondition` | Alguma filha é verdadeira. Lista vazia é falsa. | — |
| `NotCondition` | A filha é falsa. Sem filha, é falsa. | — |

O **Value** da `AttributeThresholdCondition` é uma fonte de valor, como a de um modificador: um valor por nível (inclusive `%nome` da tabela da habilidade), um atributo ou uma fórmula. Ele é avaliado com o `Owner` como instigador e alvo, na habilidade e no nível do contexto. Com a Mana em 30, "Mana `GreaterOrEqual` 20" passa e, com a Mana em 10, falha.

O alcance mora na habilidade: o campo **Cast Range** da `GameplayAbility` (`CastRange`, um `ScalableFloat` avaliado no nível e na tabela da habilidade) nasce 0, "sem limite". A `CastRangeCondition` mede a distância em 3D até o ponto: com alcance 5, um ponto a 3 m passa e um a 8 m falha. Para que a falta de ponto tenha motivo próprio (`no_target`) em vez de `cast_range`, ponha a `HasTargetPointCondition` antes da `CastRangeCondition`.

Filhas nulas nas composições são ignoradas. No Inspector, um campo `[SerializeReference] Condition<ConditionContext>` só oferece essas condições e as suas, e cada uma aparece numa linha, como "Alguma (2)" ou "Não Tem alvos".

Para consultar as tags do ASC no código, sem alocar, use `asc.HasMatching(query)`, `asc.HasAll(tags)` e `asc.HasAny(tags)`. Os dois últimos aceitam um `GameplayTagContainer`, uma lista ou um array. Valem as regras de `HasTag`: a tag ativa filha satisfaz o pai e uma tag inválida não casa. A consulta vazia casa com qualquer ASC e a consulta nula não casa.

##### Condição própria {#condicao-propria}
Para criar uma condição, herde `Condition<ConditionContext>`, marque a classe com `[Serializable]` e sobrescreva `Evaluate`. A classe precisa ser concreta, não genérica e ter um construtor sem parâmetros: assim ela aparece no seletor ao lado das condições prontas. O `[TypeCategory]` dá a pasta no seletor, a dica (`Summary`) e o resumo de uma linha (`Line`), em que `{campo}` vira o valor do campo serializado.

```csharp
using System;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Conditions;
using UnityEngine;

[Serializable]
[TypeCategory("Nível", Summary = "A habilidade está no nível mínimo.", Line = "Nível ≥ {minLevel}")]
public sealed class MinLevelCondition : Condition<ConditionContext>
{
    [SerializeField] private int minLevel = 2;

    public override string DefaultFailureKey => "game.activation.min_level";

    public override bool Evaluate(in ConditionContext context) => context.Level >= minLevel;
}
```

Em `Evaluate`, só leia o contexto: não mude o ASC, não guarde estado e não aloque, porque a consulta da UI chama a mesma condição a cada quadro. `DefaultFailureKey` é opcional e vale quando a regra deixa o **Failure Key** vazio. Para acusar configuração errada no validador, sobrescreva `Validate(result, propertyPath)` e passe o `propertyPath` nas mensagens.

### 1.5 AbilitySystemComponent (ASC) {#abilitysystemcomponent}
O `AbilitySystemComponent` é o componente que guarda as habilidades concedidas, os atributos,
os efeitos ativos e as tags de um personagem. Veja a seção [Ciclo de vida](#ciclo-de-vida).

#### Efeitos de partida {#starting-effects}
A lista **Starting Effects** do ASC aplica cada efeito no `Awake`, na ordem da lista, depois do `AttributeSet` e das `startingAbilities`, com o próprio ASC como origem e sem nível (por enquanto, nível 1). Vale para efeitos instantâneos, com duração e infinitos: uma regeneração infinita periódica sobe a vida sem script. Uma entrada vazia é pulada (e o validador acusa erro); um efeito bloqueado por imunidade ou requisito, ou inválido, gera um aviso no Console em build de desenvolvimento e não impede os seguintes. Esses efeitos são aplicados antes de qualquer ouvinte externo existir: quem assina `OnEffectApplied` depois lê `asc.ActiveEffects`.

---

## 2. Guia de criação de habilidades {#criacao}

### Passo a passo
1. Clique com o botão direito no Project → **Create → GAS → Gameplay Ability**.
2. No Inspector, localize a lista **On Activate** (era **Actions** na 2.x).
3. Clique no botão **+** e selecione a ação desejada (ex: `WaitAction`, `TargetingAction`).
4. Configure os parâmetros da ação diretamente ali.

### Exemplo: habilidade de área (explosão)
1. **Action 0 (AnimationAction):** Trigger: "Attack".
2. **Action 1 (WaitAction):** Duration: 0.5s.
3. **Action 2 (TargetingAction):** Strategy: (arraste um TS_Circle).
4. **Action 3 (PlayCueAction):** VFX_Prefab: (Explosão), SFX_Clip: (Explosão).
5. **Action 4 (ApplyEffectAction):** Effects: (arraste um GE_Damage).

---

## 3. Lista de ações disponíveis {#acoes}
- **WaitAction:** pausa a execução por X segundos do tempo do ASC (veja [Ações que esperam](#acoes-que-esperam)).
- **TargetingAction:** busca alvos usando uma estratégia (Raio, Mouse...). `Mode` Replace substitui os alvos atuais, Append adiciona.
- **ApplyEffectAction:** aplica GameplayEffects aos alvos ou ao conjurador.
- **PlayCueAction:** dispara efeitos visuais e sonoros (veja [Ações de spawn](#acoes-de-spawn)).
- **AnimationAction:** altera parâmetros (Trigger, Bool, Int) no Animator do personagem.
- **SpawnProjectileAction:** dispara projéteis (detalhes em [Projéteis](#projectile)).
- **RepeatAction:** repete uma lista de ações N vezes, com intervalo opcional.
- **ParallelAction:** executa várias ações ao mesmo tempo e espera todas terminarem.
- **ConditionalAction:** executa `Then` ou `Else` conforme tags do conjurador ou existência de alvos.
- **RaiseEventAction:** dispara um canal de eventos (detalhes em [RaiseEventAction](#raiseeventaction)).

#### Ações que esperam {#acoes-que-esperam}
O `Duration` da `WaitAction`, o `Interval` da `RepeatAction` e o `DelayBetweenShots` da `SpawnProjectileAction` contam o tempo do ASC que conjurou, o [`TickedTime`](#tempo-do-asc), e não o relógio da Unity.

- Com a fonte padrão (tempo escalado), a espera dura o mesmo que na 2.x.
- Se o ASC usa um [grupo pausável](tempo.md#pausabletimesource), pausar o grupo pausa a habilidade no meio da espera. Ao retomar, ela continua de onde parou.
- Com `autoTick` desligado, a espera só anda quando alguém chama `Tick`.
- Se o ASC for destruído, as ações que ele rodava param.
- As três esperas contam do fim do passo anterior. Um `Tick` no mesmo quadro em que o passo terminou já entra na espera, e o excesso de um `Tick` grande passa para a espera seguinte ([Base nova das ações](#gameplayaction)).
- Ações sem espera não custam quadro: rodam todas na mesma chamada. Uma `RepeatAction` com `Count` 3 e `Interval` 0 roda as três repetições de uma vez, e uma `WaitAction` com `Duration` zero ou negativo termina na hora. Na 2.x, as duas custavam um quadro.

Numa ação própria, espere devolvendo `ActionStatus.Wait(segundos)`, e não com `WaitForSeconds`.

#### Projéteis {#projectile}
A `SpawnProjectileAction` dispara projéteis: cria o `ProjectilePrefab` pelo campo `Spawner` e chama `Setup` no componente `Projectile`. As **On Hit Actions** da ação viram o `OnImpact` do projétil (era `OnHitActions`). No impacto, essa lista roda no ASC de quem lançou, por `RunActions` e sem dono: ela não é cancelada junto com a habilidade, e as esperas dela andam no `Tick` desse ASC. O alvo atingido entra nos `EventTargets`.

O contexto do impacto é derivado do contexto da habilidade no `Setup` (`CreateDerived`), com a mesma habilidade, o mesmo nível e o mesmo instigador, e recebe o ponto de impacto. A cópia do `CustomData` é rasa: o impacto enxerga as chaves gravadas antes do disparo (como `"crit"`), mas o que ele grava não volta ao contexto original, e vice-versa. Valores que são objetos continuam sendo as mesmas referências.

- **`Setup(SpawnerAsset spawner, ActionContext parentContext, List<GameplayAction> onImpact)`** guarda o spawner que criou o projétil. Há uma sobrecarga com `AbilitySystemComponent` no lugar do contexto, que roda o impacto num contexto do pool desse ASC.
- **`ReturnToPool()`** devolve o projétil por esse mesmo spawner (`spawner.Release`). Sem spawner, ele volta pelo `SpawnerAsset.Default`, que destrói a instância.
- O `Projectile` reinicia o estado no `OnEnable`, que roda tanto no `Instantiate` quanto ao sair de um pool. Ele não implementa mais o `IPoolable`.

#### Ações de spawn {#acoes-de-spawn}
`SpawnProjectileAction`, `PlayCueAction` e `SpawnVFXEffectAction` (ação de efeito) criam objetos só pelo campo **`Spawner`**, um [`SpawnerAsset`](pooling.md#spawnerasset). Vazio usa o spawner padrão, por `Instantiate`. Elas não procuram nenhum `PoolManager` na cena, então funcionam numa cena sem ele.

- **`SpawnProjectileAction`:** o projétil volta pelo mesmo spawner ([Projéteis](#projectile)).
- **`SpawnVFXEffectAction`:** com `DestroyOnRemoval`, o VFX volta pelo spawner quando o efeito é removido. Sem `DestroyOnRemoval`, um VFX com `ParticleSystem` na raiz volta quando a partícula para ([`ReleaseWhenParticlesStop`](pooling.md#releasewhenparticlesstop)).
- **`PlayCueAction`:** cria o VFX pelo spawner e toca o som com `AudioSource.PlayClipAtPoint`.

!!! warning "Armadilha: o VFX do `PlayCueAction` não volta"
    O `PlayCueAction` não devolve o VFX ao spawner. Com o padrão por `Instantiate`, o VFX precisa se destruir sozinho (por exemplo, `Stop Action` = `Destroy` na partícula). Com um spawner de pool, o pool cresce a cada uso. Isso continua assim até o módulo Cues.

#### RaiseEventAction {#raiseeventaction}
A `RaiseEventAction` dispara um [canal de eventos](eventos.md) quando a habilidade chega nela. Ela fica no assembly `SpellBook.GAS.Bridges`, que liga o GAS ao módulo de Eventos, e aparece no seletor de ações na categoria **Eventos**.

A ação guarda uma **emissão**, o mesmo campo do [`EventEmitter`](eventos.md#eventemitter): o canal e o payload fixo.

- **Canal sem payload** (Void): a ação só dispara o canal.
- **Canal com payload** (`EventChannel<T>`, como Int ou um payload próprio): a ação envia o valor fixo do campo **Payload**. Trocar o canal por um de outro tipo recria a emissão, e o payload volta ao valor padrão.
- Cada passagem pela ação dispara o canal uma vez, e a ação termina na mesma chamada. O ASC que conjurou aparece como quem disparou no log do canal.
- Sem emissão ou sem canal, a ação não faz nada, e o validador acusa o erro na habilidade.

O campo **Payload Source** escolhe de onde vem o payload. O padrão, `Fixed`, envia o valor fixo da emissão e serve para qualquer canal. As outras fontes leem a habilidade na hora do disparo e ignoram o payload fixo:

| Fonte | Canal | Valor enviado |
|---|---|---|
| `Fixed` | qualquer | o payload fixo da emissão |
| `AttributeValue` | Float | valor atual do **Attribute** no conjurador |
| `AbilityName` | String | nome do asset da habilidade |
| `SourceObject` | GameObject | objeto do conjurador |
| `TargetObject` | GameObject | objeto do primeiro alvo válido |
| `EventData` | `AbilityEventData` | dono, habilidade, nível e se a habilidade foi cancelada |

- Fonte que não serve para o tipo do canal não dispara nada, sem exceção, e o validador acusa o erro. `AttributeValue` sem **Attribute** também é erro.
- Sem valor para enviar (conjurador sem o atributo, nenhum alvo válido), a ação não dispara.
- Em código, `GasPayloadSourceUtility.Supports(fonte, tipoDoPayload)` diz se a fonte serve para um tipo de canal.
- No Inspector, inclusive na lista de ações da habilidade, o campo **Payload Source** lista só as fontes que servem para o tipo do canal escolhido. Se a fonte atual não serve (o canal mudou depois), ela continua na lista com a marca "não serve para este canal". O campo **Attribute** só aparece com `AttributeValue`, e um aviso lembra que o payload fixo é ignorado quando a fonte não é `Fixed`.
- As fontes do lado do efeito (a magnitude de um modificador, as pilhas, o nome do efeito e o `EffectEventData`) ainda não existem.

### 3.1 Criando ações próprias {#acoes-proprias}
Herde de `GameplayAction` e sobrescreva `Execute(ActionContext context, ref ActionFrame frame)`, que devolve um `ActionStatus`. O override é `protected override` em qualquer assembly, e quem chama o `Execute` é o executor do ASC. A mesma base serve às listas da habilidade, do efeito e do projétil, e o contrato está em [Base nova das ações](#gameplayaction).

- A mesma instância da ação é compartilhada por todos que usam a habilidade. Guarde o estado da execução no `ActionFrame` ou no `context.CustomData`, nunca em campos.
- Sobrescreva `OnCancel(context, ref frame)` para restaurar estado se a habilidade for cancelada no meio da ação (ex: reabilitar movimento após um dash).
- Para rodar sub-ações, devolva `ActionStatus.Run(lista)`, ou `ActionStatus.RunParallel(lista)` para rodá-las ao mesmo tempo. O cancelamento da habilidade chega às filhas.
- Quem lançou está em `context.Instigator`, e o ponto em `context.Point`. Os alvos que a `TargetingAction` encontra ficam em `context.EventTargets`; tire da lista os alvos destruídos antes de usá-la.
- Para esperar, devolva `ActionStatus.Wait(segundos)`, que conta o tempo do ASC ([Ações que esperam](#acoes-que-esperam)).
- Para acusar uma configuração inválida, sobrescreva `Validate(ValidationResult result)` e chame `result.AddError(...)`. O `GameplayAbility` repassa a validação a cada ação da lista.

### 3.2 Targeting {#targeting}
`TargetingStrategy` respeita `physicsMode` (2D/3D), procura o `AbilitySystemComponent` no collider ou nos pais (sem duplicatas) e filtra por:

- `includeSelf` (padrão: falso);
- `requiredTargetTags` / `ignoredTargetTags` (ex: ignorar a tag do próprio time).

#### CircleTargeting {#circletargeting}
Os alvos num raio em volta da origem. Aceita `maxTargets` (prioriza os mais próximos). Crie em **Create → GAS → Targeting → Circle**.

#### MouseTargetingStrategy {#mousetargetingstrategy}
O alvo válido mais próximo do ponto mirado, num raio pequeno. Crie em **Create → GAS → Targeting → Mouse**.

### 3.3 Base nova das ações {#gameplayaction}
`GameplayAction` (namespace `SpellBook.GAS.Actions`) é a base única das ações da 3.0, e o `AbilitySystemComponent` tem o executor que a roda. A lista `OnActivate` da habilidade, as listas do efeito e o `OnImpact` do projétil usam essa base, e a mesma instância de ação pode estar em listas de donos diferentes.

A ativação roda o `OnActivate` com o spec como dono e termina quando a execução acaba, na mesma chamada se nenhuma ação esperar. `CancelAbility` cancela as execuções desse dono antes de terminar a habilidade.

Uma ação implementa `Execute(ActionContext context, ref ActionFrame frame)` e devolve um `ActionStatus`:

- `ActionStatus.Done`: a ação terminou, e o executor passa à próxima da lista.
- `ActionStatus.Wait(segundos)`: o executor chama a ação de novo depois desse tempo do ASC. Zero ou negativo não espera.
- `ActionStatus.Run(filhas)`: o executor roda as filhas em sequência e chama a ação de novo quando elas terminam.
- `ActionStatus.RunParallel(filhas)`: o executor roda as filhas ao mesmo tempo e chama a ação de novo quando todas terminam. A espera seguinte conta do fim da filha que terminou por último.

A mesma instância da ação serve a todas as execuções. Guarde o estado de uma execução no `ActionFrame` (`Phase`, `Index`, `Count` e `Data`), nunca em campos da ação. O quadro chega zerado na primeira chamada. Uma ação que espera e depois age, por exemplo, devolve `Wait` com `Phase` em 0, muda `Phase` para 1 e, na chamada seguinte, faz o trabalho e devolve `Done`. Sobrescreva `OnCancel(context, ref frame)` para desfazer o que a ação deixou pela metade quando a execução é cancelada.

Para rodar uma lista pelo código, chame `asc.RunActions(acoes, contexto, dono)`. As ações sem espera rodam na mesma chamada, e as esperas andam no `Tick` do ASC. Uma entrada nula da lista é pulada. O `dono` é qualquer objeto que identifique a execução. `asc.CancelActions(dono)` cancela as execuções dele, e `asc.HasRunningActions(dono)` diz se ainda há alguma em curso.

No cancelamento, cada ação em curso recebe `OnCancel`. As filhas paralelas recebem antes da ação que as rodou, e as filhas em sequência antes da ação mãe. Um `OnCancel` pode cancelar ou iniciar outra execução sem quebrar o cancelamento em curso.

Uma espera conta a partir do fim do passo anterior, no tempo do ASC. Um `Tick` no mesmo quadro em que a execução começou conta, e o excesso de um `Tick` grande passa para a espera seguinte: com duas esperas de 1 s, `Tick(1.5)` e depois `Tick(0.5)` terminam as duas.

O `ActionContext` leva os dados da execução: `Owner` (o ASC que roda), `Instigator`, `EventTargets`, `Targets`, `Point` com `HasPoint`, `Ability`, `Spec`, `Level`, `EffectInstance`, `Effect` com `HasEffect`, `EventInstigator`, `IsCancelled` e `CustomData`. Passe `null` como contexto para o ASC usar um, com ele mesmo como `Owner` e `Instigator`.

O ASC guarda execuções e contextos em pool, e rodar a mesma lista de novo não aloca. Se você passar um contexto seu, o executor fica com ele e o devolve ao pool quando a execução termina ou é cancelada. Não use essa referência depois disso. Um `ActionExecutionHandle` antigo continua dizendo que a execução não roda, mesmo depois que o pool a reaproveita.

As chaves do `CustomData` usam o comparador padrão. Para usar outro, como `StringComparer.OrdinalIgnoreCase`, chame `context.SetCustomDataComparer(comparador)` antes de gravar a primeira chave. Com chaves gravadas, a chamada lança `InvalidOperationException`. Um contexto que volta do pool vem com o comparador padrão.

`context.CreateDerived(ponto)` devolve um contexto novo com esse ponto, o mesmo `Owner`, `Instigator`, `Ability` e `Level`, e uma cópia rasa do `CustomData`, com o mesmo comparador. Uma chave acrescentada no derivado não aparece no original, mas um valor que é objeto fica compartilhado. O derivado não tem vínculo de cancelamento com a execução de origem.

---

## 4. Ciclo de vida, cooldown e custo {#ciclo-de-vida}

### 4.1 Concedendo habilidades
O ASC só ativa habilidades concedidas. Use a lista `startingAbilities` no Inspector ou, via código, `asc.GiveAbility(ability, level)` / `asc.RemoveAbility(ability)`. Cada habilidade concedida vira um `AbilitySpec` (nível, se está ativa e o contexto da execução atual).

`OnAbilityGranted(spec)` dispara quando `GiveAbility` cria o spec, já na lista; conceder de novo a mesma habilidade devolve o spec existente e não dispara. `OnAbilityRemoved(spec)` dispara em `RemoveAbility`, depois do cancelamento e da saída da lista. As habilidades de `startingAbilities` são concedidas no `Awake`, antes de qualquer ouvinte externo existir: quem assina depois lê `asc.Abilities` para pegar as que já estão lá.

### 4.2 Ativação
`asc.TryActivateAbility(ability, targetPos)` retorna um `AbilityActivationResult`. Em ordem, as checagens são:

1. concedida (`NotGranted`) e não ativa (`AlreadyActive`);
2. `BlockedTags` (`BlockedByTags`) e `RequiredTags` (`MissingRequiredTags`);
3. `BlockAbilitiesWithTag` de habilidades ativas (`BlockedByActiveAbility`);
4. [cargas](#cargas) (`NoCharges`), se a habilidade tem cargas, e depois cooldown (`OnCooldown`), pelas tags do `CooldownEffect` **ou** por uma instância ativa dele;
5. custo (`CannotAffordCost`), só checado, sem aplicar;
6. regras de ativação (`BlockedByCondition`), na ordem da lista; a primeira que falha decide.

Nada é gasto antes do fim das checagens: a carga, a recarga e o custo saem só depois que todas passam.

Uma habilidade sem ações, ou só com ações sem espera, termina dentro do `TryActivateAbility`. Uma consulta logo depois já vê a recarga (`OnCooldown`), e não `AlreadyActive`. Na 2.x, ela ficava ativa por um quadro.

**Regras de ativação.** A lista **Activation Conditions** da habilidade (`ActivationConditions`) tem uma `ActivationRule` por entrada: uma [condição](#condicoes) e o motivo da falha, com **Failure Tag** (uma `GameplayTag`, opcional) e **Failure Key** (chave de texto). Com a chave vazia, vale a chave padrão da condição. Entradas vazias ou sem condição são puladas. As condições recebem o alvo da ativação no `ConditionContext.Target`.

**Alvo e motivo.** `asc.TryActivateAbility(ability, alvo)` recebe um `AbilityTarget` (`AbilityTarget.AtPoint(ponto)` ou `AbilityTarget.None`); a forma com `Vector3` usa `AtPoint`. Para consultar sem ativar, `asc.GetActivationResult(ability, alvo, out ActivationFailure falha)` passa pelas mesmas checagens, na mesma ordem, sem mudar o ASC, sem disparar evento e sem alocar; `GetActivationResult(ability)` consulta sem alvo. O `ActivationFailure` traz:

| Campo | O que é |
|---|---|
| `Result` e `IsSuccess` | O `AbilityActivationResult` e se a checagem passou. |
| `ReasonTag` | Nas falhas de tag, a tag que decidiu: a de `BlockedTags` que o ASC tem, a de `RequiredTags` que falta ou o `AbilityTag` bloqueado por uma habilidade ativa. Nas de regra, a **Failure Tag**. Inválida quando não há. |
| `ReasonKey` | A chave de texto da regra que falhou; nula nas outras falhas (a UI mapeia o `Result`). |
| `ConditionIndex` e `Rule` | Índice e regra que falharam; -1 e nula fora das regras. |

Com a regra "Mana `GreaterOrEqual` 20" e a tag `Fail.LowMana`, a Mana em 10 devolve `BlockedByCondition` com `ReasonTag` `Fail.LowMana`; em 30, a habilidade ativa.

**Motivo na UI.** Para mostrar por que a habilidade não ativa, leia o `ActivationFailure` da consulta ou do `OnAbilityFailed`. Com `ReasonKey`, procure o texto pela chave; sem chave, escolha o texto pelo `Result` (`NoCharges`, `OnCooldown`, `CannotAffordCost` e os outros). Use a `ReasonTag` quando a lógica de jogo precisa reagir a um motivo, como tocar um som diferente para "sem mana".

**No Inspector.** Abaixo dos campos da habilidade, a seção **Ativação** mostra uma linha por regra, na ordem da lista: o número, o resumo da condição e o motivo, como `1. Mana Greater Or Equal 20 → motivo: Fail.LowMana`. O motivo é a **Failure Tag**; sem ela, a chave efetiva (a **Failure Key** ou a chave padrão da condição). A condição de cada regra é escolhida no seletor de tipo da lista **Activation Conditions**, e as composições abrem as filhas no mesmo lugar.

Em caso de sucesso, o ASC:

- aplica o `CooldownEffect` (duração reduzida por `CooldownReductionAttribute`) e o `CostEffect`;
- cancela as habilidades cujo `AbilityTag` está em `CancelAbilitiesWithTag`;
- concede as `ActivationOwnedTags` enquanto a habilidade roda.

Para consultar sem ativar, use `ability.CanActivate(asc, out var reason)` ou, com alvo e motivo, `ability.CanActivate(asc, alvo, out var falha)`.

**Consultando a recarga.** `asc.GetCooldownTimeRemaining(cooldownEffect)` devolve o tempo restante e `asc.GetCooldownNormalized(cooldownEffect)`, o valor de 1 a 0 para barras de recarga. Contam a instância do próprio `CooldownEffect` e qualquer efeito ativo que conceda uma tag igual ou **filha** de alguma tag dele, pela mesma regra de `HasTag`: um efeito com `Cooldown.Fire` conta para uma recarga com a tag `Cooldown`, mas um efeito com `Cooldown` não conta para uma recarga com `Cooldown.Fire`. Com vários efeitos casando, vale o de maior tempo restante. O normalizado divide esse tempo pela duração aplicada da mesma instância, e não pela `Duration` do asset: com `CooldownReductionAttribute` em 0,25 sobre 8 s, a recarga aplicada é de 6 s e o normalizado vale 1 logo após ativar, 0,5 com 3 s restantes e 0 com a habilidade pronta. Sem recarga, ou com duração aplicada zero ou negativa, o normalizado é 0.

**Controlando a recarga.** A recarga é um efeito ativo, e o ASC a controla por habilidade, concedida ou não, sobre as mesmas instâncias que contam no tempo restante (o próprio `CooldownEffect` e as de tag filha):

- `asc.ReduceCooldown(ability, segundos)` tira tempo de cada uma; a que chega a 0 expira na hora. Segundos negativos ou `NaN` são ignorados com aviso.
- `asc.ResetCooldown(ability)` remove todas (motivo `Removed`), e a habilidade volta a ser ativável. Exceção: uma tag de recarga dada fora de efeito, com `AddTag`, continua bloqueando, porque não há instância para remover.
- `asc.RestartCooldown(ability)` volta cada uma à duração aplicada, e não à `Duration` do asset.

Os três devolvem se algo mudou. Habilidade nula, sem `CooldownEffect` ou pronta: `false`, sem exceção. Numa habilidade com [cargas](#cargas), eles também agem sobre a recarga da carga em curso.

**Evento de recarga.** `OnCooldownChanged(spec, mudança)` acompanha a recarga aplicada pela ativação de uma habilidade concedida, com o `CooldownChange`:

- `Started`: a ativação aplicou a recarga (antes do `OnAbilityActivated`);
- `Changed`: `ReduceCooldown` ou `RestartCooldown` mudou o tempo e a recarga continua;
- `Ended`: a habilidade ficou pronta, porque as instâncias expiraram, foram reduzidas a 0 ou removidas (inclusive por `ResetCooldown`). Sai depois do `OnEffectRemoved` da instância que faltava.

Recarga aplicada à mão, com `ApplyEffect`, não dispara o evento: ela aparece no tempo restante e em `OnEffectApplied`/`OnEffectRemoved`.

#### Cargas {#cargas}
Com cargas, a habilidade pode ser usada várias vezes seguidas, e as cargas voltam com o tempo. Ligue **Enabled** no bloco **Charges** da habilidade (`GameplayAbility.Charges`) e preencha:

- **Max Charges:** o máximo de cargas, com as mesmas origens de um modificador: valor por nível, `%nome` da tabela, atributo ou fórmula. O valor é arredondado para baixo, e abaixo de 0 vale 0.
- **Recharge Time:** os segundos para voltar uma carga, por nível ou por `%nome`.
- **Mode:** só `Sequential`, uma carga por vez.

As cargas nascem cheias ao conceder a habilidade, com o máximo avaliado no nível do spec. O ASC volta a avaliar o máximo a cada `SetAbilityLevel` e a cada `Tick`, então um `%nome` da tabela acompanha o nível e um atributo acompanha o valor dele. Com `%Charges` valendo 2, 3 e 4 nos níveis 1 a 3, a habilidade no nível 3 guarda até 4 cargas. Quando o máximo sobe, as cargas não enchem: se faltar alguma, a recarga começa. Quando o máximo desce abaixo das cargas, elas são limitadas a ele, com `OnChargesChanged`, e a recarga para.

Cada ativação com sucesso gasta uma carga junto do custo. Se a recarga estava parada, ela começa. Quando a recarga termina, volta uma carga e, se ainda faltar alguma, a recarga da seguinte começa na hora, com o tempo que sobrou do `Tick` passado adiante. No máximo, a recarga para. Um `Tick` grande pode devolver mais de uma carga.

O tempo de recarga passa pela mesma redução da recarga da habilidade: o `CooldownReductionAttribute` e a [CooldownReductionPolicy](#cooldownreductionpolicy), com piso e teto. Com **Max Reduction** 0,5 e **Min Duration** 1 s, uma recarga de 8 s com redução de 0,9 dura 4 s. O tempo anda só pelo [`Tick` do ASC](#tempo-do-asc), nunca pelo relógio da Unity.

Na checagem, as cargas substituem a recarga: com uma carga ou mais, a habilidade ativa mesmo com a recarga de carga correndo, e sem carga o resultado é `NoCharges`. O `CooldownEffect` é opcional e continua valendo como tempo mínimo entre usos. Com carga disponível e o `CooldownEffect` correndo, o resultado é `OnCooldown`; sem carga, `NoCharges` vem primeiro. A recarga de carga não é efeito ativo: não concede tag, não aparece em `ActiveEffects` e não dispara `OnCooldownChanged`. Com **Enabled** desligado, a habilidade se comporta como antes.

O estado fica no `AbilitySpec` (`Charges`, `MaxCharges`, `ChargeRechargeRemaining` e `ChargeRechargeDuration`, só leitura). Pelo ASC:

| Membro | O que faz |
|---|---|
| `GetCharges(spec)` e `GetMaxCharges(spec)` | Cargas disponíveis e máximo. |
| `GetChargeRechargeRemaining(spec)` | Segundos até a próxima carga; 0 com a recarga parada. |
| `GetChargeRechargeNormalized(spec)` | De 1 a 0 pela duração aplicada da recarga em curso, para barras; 0 com a recarga parada. |
| `AddCharges(spec, n)` | Soma `n` cargas, sem passar do máximo. Devolve `false` se nada mudou, inclusive com `n` ≤ 0. |
| `RefillCharges(spec)` | Enche as cargas. Devolve `false` se já estavam cheias. |
| `OnChargesChanged(spec, antes, depois)` | Sai só quando as cargas mudam: no gasto, a cada carga que volta, em `AddCharges` e `RefillCharges` e quando o máximo desce abaixo das cargas. |

Com spec nulo ou sem cargas, as consultas devolvem 0 e os métodos, `false`. Encher pelo `AddCharges` ou pelo `RefillCharges` para a recarga.

Os controles de recarga da habilidade também agem sobre a recarga da carga em curso, além do `CooldownEffect`:

- `ReduceCooldown(ability, segundos)` tira o tempo da recarga da carga. Se passar do que falta, volta uma carga e o resto sai da recarga seguinte, então uma redução grande pode devolver mais de uma.
- `ResetCooldown(ability)` devolve uma carga na hora e recomeça a recarga da seguinte com a duração cheia. No máximo, a recarga para.
- `RestartCooldown(ability)` volta a recarga da carga à duração aplicada, já com a redução de recarga.

Com a recarga da carga parada e sem `CooldownEffect` correndo, os três devolvem `false`.

Com **Enabled** ligado, a seção **Ativação** do Inspector mostra a prévia das cargas no **Nível de prévia**, como `1 carga a cada 6 s, máximo 4 no nível 3`. A prévia não aplica a redução do atributo. Com **Max Charges** por atributo ou por fórmula, ela mostra o tipo da fonte no lugar do número, porque o valor depende do ASC.

```csharp
AbilitySpec spec = asc.FindSpec(dash);
asc.OnChargesChanged += (s, antes, depois) => Debug.Log($"{s.Ability.name}: {antes} -> {depois}");
asc.TryActivateAbility(dash, AbilityTarget.None); // 3 -> 2, a recarga começa
float barra = asc.GetChargeRechargeNormalized(spec);
```

#### CooldownReductionPolicy {#cooldownreductionpolicy}
A `CooldownReductionPolicy` diz como o valor do `CooldownReductionAttribute` vira duração de recarga. Crie o asset por *Create > GAS > Cooldown Reduction Policy* e escolha-o no campo **Cooldown Reduction Policy** da habilidade. A política só vale com um `CooldownReductionAttribute`; sem ele, a recarga usa a `Duration` do asset e o validador avisa.

- **Min Duration:** piso da recarga, em segundos (padrão 0,1).
- **Max Reduction:** teto da redução, de 0 a 1 (padrão 1, ou 100 %).
- **Mode:** `Percent` lê o atributo como fração de redução (0,2 = 20 %). `Haste` lê o atributo como velocidade extra: a redução é `1 - 1 / (1 + haste)`, então haste 1 dobra a velocidade e corta a recarga pela metade. Nos dois modos, valor negativo conta como 0 e a redução para no **Max Reduction**.

A duração é `max(Min Duration, duração × (1 - redução))`. Exemplo: com **Min Duration** 1 s e **Max Reduction** 0,5, uma recarga de 8 s com redução de 0,9 dura 4 s, porque o teto vale. Com **Max Reduction** 1 e redução de 0,99, o piso de 1 s vale.

Sem política, a habilidade usa `CooldownReductionPolicy.ApplyDefault`: piso de 0,1 s, redução limitada a [0, 1] e modo `Percent`, a mesma regra de antes da política. O validador acusa erro em **Min Duration** negativo e em **Max Reduction** fora de 0 a 1.

### 4.3 Custo
`CostEffect` é um GameplayEffect Instant, normalmente com modifier `Add` negativo (ex: `Mana Add -20`). A ativação falha se algum atributo ficaria abaixo do `MinValue`.

### 4.4 Cancelamento
- `asc.CancelAbility(ability)` interrompe a habilidade e chama `OnCancel` nas ações em execução.
- `CancelledByTags`: a habilidade é cancelada quando o dono recebe uma destas tags (ex: `State.Stunned`).
- Desativar o GameObject do ASC cancela todas as habilidades ativas.

### 4.5 Eventos do ASC
`OnAbilityActivated(spec)`, `OnAbilityEnded(spec, cancelled)` e `OnAbilityFailed(ability, falha)`, este último útil para feedback de UI ("Sem mana", "Em recarga"). A `falha` é a mesma `ActivationFailure` que `GetActivationResult(ability, alvo, out falha)` devolveria, com o `Result` e o motivo (`ReasonTag`, `ReasonKey`). Só `TryActivateAbility` dispara o evento; a consulta não.
Para efeitos com duração ou infinitos, por instância:

- `OnEffectApplied(instancia)`: uma instância nova entrou, depois de tags e modificadores. Efeito instantâneo, renovação e pilha não disparam.
- `OnEffectStacksChanged(instancia, antes, depois)`: a contagem de pilhas mudou de fato. Na reaplicação que empilha, `(instancia, 1, 2)`; em `MaxStacks`, não dispara. Também dispara em `SetEffectStacks` e `RemoveEffectStacks`.
- `OnEffectRemoved(instancia, motivo)`: a instância saiu, com o `EffectRemovalReason` (`Expired`, `Removed`, `RemovedByTag` ou `StacksDepleted`). Dispara depois de devolver tags e modificadores, uma vez por instância.

Um ouvinte pode aplicar ou remover efeitos dentro desses eventos sem quebrar a remoção em grupo nem perder eventos. Sem ouvintes, disparar não aloca.

O ASC não dispara mais evento global; veja a [Migração 2.x → 3.0](../regras/migracao-2-para-3.md#eventos-do-gas).

Para ver esses eventos, os atributos, as tags, os efeitos e as recargas de um ASC em Play, use o [GAS Debugger](../ferramentas/gas-debugger.md).

### 4.6 Tempo do ASC {#tempo-do-asc}
Durações de efeitos, ticks periódicos e recargas contam só o tempo que o ASC recebe. O ASC não lê o `Time` da Unity.

- **`timeSource`** (Inspector): um `TimeSourceAsset` do módulo [Tempo](tempo.md). Vazio usa o tempo escalado da Unity, como na 2.x.
- **`autoTick`** (Inspector, ligado por padrão): o ASC avança sozinho no `Update` pela diferença do `ElapsedTime` da fonte ([consumo por diferença](tempo.md#consumo-por-diferenca)). Desligado, ele só avança quando alguém chama `Tick`.
- **`Tick(float deltaTime)`**: avança efeitos e recargas em `deltaTime` segundos. Delta zero ou negativo é ignorado.
- **`TickedTime`**: o relógio do ASC, a soma de todos os `Tick` aceitos.
- **`SetTimeSource(ITimeSource)`**: troca a fonte por código, por exemplo para um `ManualTimeSource` num teste. `null` volta à fonte do Inspector. O tempo que passou na fonte antiga não é contado.

Com `autoTick` desligado, a mesma sequência de `Tick` sempre dá o mesmo estado, o que serve para testes e para simulação.

```csharp
asc.SetTimeSource(manualTime); // com autoTick desligado no Inspector
asc.ApplyEffect(burning);      // efeito de 2 s
asc.Tick(1.9f);                // ainda ativo
asc.Tick(0.2f);                // expirou
```

**Receita: pausar a gameplay e manter a UI.** Crie um grupo pausável pelo menu *SpellBook > Time > Pausable Time Group*, chame-o de "Gameplay" e coloque-o no `timeSource` dos ASCs dos personagens. Ao chamar `Pause` no grupo, as recargas e as durações desses ASCs param. Um ASC com fonte não escalada e o resto da cena continuam. Ao chamar `Resume`, tudo volta de onde parou. Veja [Grupo pausável](tempo.md#pausabletimesource).

O `Projectile` também tem um campo `timeSource`. Ele conta o movimento, o `Lifetime` e a `InitialInvulnerabilityDuration` no `FixedUpdate`, e não o relógio da Unity.

!!! warning "Armadilha: somar floats na fronteira"
    Um efeito expira quando o tempo restante fica menor ou igual a 0. Em `float`, `2f - 1.9f - 0.1f` dá cerca de `+2,2e-8`, um valor positivo, e não 0. Por isso uma recarga de 2 s **não** expira depois de `Tick(1.9f)` seguido de `Tick(0.1f)`. Já `3f - 2.9f - 0.1f` dá cerca de `-9,7e-8`, e essa recarga expira. O resultado na fronteira depende dos valores, então não conte com ele. Nos testes, avance direto até o valor da fronteira (`Tick(2f)`) ou passe um pouco dela.

---

## 5. Integração via código (C#) {#codigo}

```csharp
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;

public class PlayerCombat : MonoBehaviour {
    public AbilitySystemComponent asc;
    public GameplayAbility fireball;

    void Start() {
        asc.GiveAbility(fireball);
        asc.OnAbilityFailed += (ability, falha) => Debug.Log($"{ability.DisplayName}: {falha.ReasonKey ?? falha.Result.ToString()}");
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.E)) {
            Vector3 targetPos = GetMouseWorldPosition();
            var result = asc.TryActivateAbility(fireball, targetPos);
        }
    }
}
```
