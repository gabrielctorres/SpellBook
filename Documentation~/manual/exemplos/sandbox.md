# Sample Sandbox {#sandbox}

O Sandbox é uma cena 3D de teste manual que liga os módulos do SpellBook num caso pequeno:
um conjurador lança uma Bola de Fogo num boneco de treino que não morre. Ele serve para ver
os módulos funcionando juntos antes de usá-los num projeto, e como ponto de partida para copiar
a configuração dos assets.

## O que o Sample mostra {#o-que-mostra}

- **GAS:** uma habilidade com custo de mana e recarga, um efeito de dano instantâneo e uma
  queimadura com duração, tique periódico e tag `Status.Queimando`.
- **Events:** o canal "Dano Recebido", com o valor real de cada dano, e o canal "Bola de Fogo
  Acertou", disparado pela ponte GAS → Events. Os dois aparecem no
  [Event Debugger](../ferramentas/event-debugger.md).
- **Pooling:** os projéteis saem de um `PooledSpawnerAsset` e voltam a ele no impacto ou no
  fim do tempo de vida.
- **Time:** o gameplay usa um `PausableTimeSource`, que a tecla `P` pausa; a câmera lenta muda o
  `Time.timeScale`.

A cena não tem HUD nem Canvas. Os valores de vida e mana aparecem no Inspector durante o Play,
e as mensagens saem no Console.

## Importar {#importar}

1. Abra **Window > Package Manager** e selecione o pacote **Spell Book**.
2. Na aba **Samples**, clique em **Import** ao lado de **Sandbox**.
3. A Unity copia o Sample para `Assets/Samples/Spell Book/<versão>/Sandbox/`.
4. Abra `Scenes/Sandbox.unity` e entre em Play.

A entrada usa o Input Manager legado: em **Project Settings > Player**, o **Active Input
Handling** precisa ser **Input Manager (Old)** ou **Both**. Os materiais usam o shader
`Unlit/Color` e desenham no Built-in e no URP; o HDRP não é coberto.

## Controles {#controles}

A placa 3D da cena e o `README.md` do Sample trazem este mesmo texto:

```text
SpellBook Sandbox
Mouse: mira no chão (o conjurador gira)
1: Bola de Fogo (custo, recarga, pool, dano, queimadura)
P: pausa e retoma o jogo (grupo de tempo do gameplay)
T: câmera lenta, escala 0,25 (tempo escalado)
R: reinicia (vida, mana, recarga, pausa e escala)
```

| Tecla | Ação | O que mostra |
|---|---|---|
| Mouse | Mira no chão | O conjurador gira para o ponto mirado |
| `1` | Conjura a Bola de Fogo | Custo de mana, recarga, projétil do pool, dano e queimadura |
| `P` | Pausa e retoma o jogo | O grupo de tempo do gameplay para: efeitos, recarga e projéteis |
| `T` | Alterna a câmera lenta (escala 0,25) | Tudo o que segue o tempo escalado fica mais lento |
| `R` | Reinicia a cena | Vida, mana, recarga, pausa e escala voltam ao início |

O boneco começa com 2000 de vida e o conjurador com 400 de mana, o que cabe em 20 conjurações
por reinício. A vida do boneco não passa de 0 e nada acontece quando chega lá: use `R` para
voltar ao início.

## Passo a passo {#passo-a-passo}

### S1. Conjurar e ver o dano

1. Entre em Play e mire com o mouse no boneco, a cerca de 8 m.
2. Aperte `1`. O Console mostra "Bola de Fogo conjurada", e a bola sai do pool (na Hierarchy, o objeto
   é ativado em `[SpellBook Pools]`).
3. No impacto, o boneco pisca e o Console mostra "Bola de Fogo acertou" e "Dano recebido: 20".
4. A cor de queimadura fica por 4 s. A cada 0,5 s o Console mostra "Queimadura: tique" e
   "Dano recebido: 5".
5. Selecione o boneco e o conjurador para ver Vida e Mana no Inspector (`AttributeReadout`).

### S2. Recarga, mana e renovação

1. Aperte `1` duas vezes em menos de 1,5 s. A segunda mostra "Bola de Fogo em recarga: N,N s".
2. Conjure de novo durante a queimadura: a duração volta a 4 s, sem segunda queimadura paralela.
3. Gaste a mana (20 conjurações). A próxima mostra "Sem mana para a Bola de Fogo". Use `R` para voltar ao início.

### S3. Pausa e câmera lenta

1. Conjure e aperte `P` com a bola em voo: projétil, queimadura e recarga param. `P` de novo retoma.
2. Aperte `T`: a escala vai a 0,25 e tudo o que segue o tempo escalado fica mais lento. `T` de novo volta a 1.
3. `R` reinicia a cena e devolve a escala a 1, também com o jogo pausado ou em câmera lenta.

## Dados do Sample {#dados}

Os valores abaixo são os dos assets do Sample e podem ser ajustados no playtest.

| Asset | Configuração |
|---|---|
| `ATR_Vida` | Mínimo 0, máximo 9999 |
| `ATR_Mana` | Mínimo 0, máximo 9999 |
| Conjurador (`AttributeSet`) | Vida 100, Mana 400 |
| Boneco (`AttributeSet`) | Vida 2000 |
| `GE_BolaDeFogo_Custo` | Instantâneo. Mana soma −20 |
| `GE_BolaDeFogo_Recarga` | Com duração de 1,5 s. Sem modificadores |
| `GE_BolaDeFogo_Dano` | Instantâneo. Vida soma −20 |
| `GE_Queimadura` | Com duração de 4 s e período de 0,5 s. Modificador periódico: Vida soma −5. Tique: `DebugLogEffectAction` "Queimadura: tique". Tag concedida: `Status.Queimando`. `CanStack` ligado, `MaxStacks` 1 e `RefreshDurationOnStack` ligado |
| `GA_BolaDeFogo` | Custo e recarga acima. `SpawnProjectileAction` com `SpawnOffset` (0, 1,2, 1). No impacto: `ApplyEffectAction` (Dano e Queimadura) e `RaiseEventAction` (`CH_BolaDeFogoAcertou`) |
| `SP_Projeteis` | `initialSize` 5 e `canExpand` ligado |
| `BolaDeFogo.prefab` | `Projectile` com `Speed` 12 e `Lifetime` 3 s, na fonte de tempo `TS_Gameplay` |
| Câmera lenta | `Time.timeScale` 0,25 |

## O relay de dano é do Sample {#relay}

A Bola de Fogo dispara "Bola de Fogo Acertou" pela ponte GAS → Events do pacote (`RaiseEventAction`), mas essa
ação tem payload fixo e não alcança a queimadura, que é um efeito. Por isso o `DamageTakenRelay` ouve a Vida
do próprio boneco e dispara "Dano Recebido" com o valor real. Ele é um substituto local do relay de
eventos do ASC (B-004), que o pacote ainda não tem. Quando o B-004 entrar, o Sample troca o relay pelo do pacote.

## Renovação da queimadura {#renovacao-da-queimadura}

Reaplicar a queimadura **renova** a duração para 4 s, sem somar pilhas e sem criar segunda instância.
Isso vem de `CanStack` ligado, `MaxStacks` 1 e `RefreshDurationOnStack` ligado. Com `CanStack` desligado, um
efeito com duração reaplicado cria uma segunda instância paralela, que não renova nem empilha.

## Scripts do Sample {#scripts}

Os scripts ficam no assembly `SpellBook.Samples.Sandbox`, que referencia só Core, Time, Events,
Tags e GAS. Eles são exemplos para ler e copiar, não API do pacote.

### SandboxCaster {#sandboxcaster}

Fica no conjurador. Projeta o mouse num plano na altura da mira, gira o conjurador para o ponto
e, com a tecla de conjuração, chama `TryActivateAbility` com o ponto como alvo. Escreve no
Console a conjuração e o motivo de cada falha: recarga, com o tempo restante, ou falta de mana.

### SandboxDirector {#sandboxdirector}

Fica num objeto da cena. Cria o conjurador e o boneco nos pontos de spawn e lê as teclas `P`,
`T` e `R`. A leitura não depende do grupo de tempo, então `P` funciona com o jogo pausado. Ao
reiniciar e ao sair da cena, devolve o `Time.timeScale` a 1.

### DamageTakenRelay {#damagetakenrelay}

Fica no boneco. Ouve a mudança da própria Vida e, a cada queda, dispara o canal "Dano Recebido"
com o valor perdido. Cobre o dano imediato e cada tique da queimadura.

### TrainingDummyFeedback {#trainingdummyfeedback}

Fica no boneco. Pisca a cor de acerto a cada dano recebido, medida pela fonte de tempo do
gameplay, e usa a cor de queimadura enquanto a tag de queimadura estiver ativa. A cor vai por
`MaterialPropertyBlock`, sem instanciar material.

### AttributeReadout {#attributereadout}

Fica nos dois personagens. Copia o valor atual de cada atributo da lista para um campo visível no
Inspector a cada mudança, para acompanhar vida e mana durante o Play.
