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
