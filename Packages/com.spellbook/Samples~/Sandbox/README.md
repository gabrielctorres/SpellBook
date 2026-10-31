# Sandbox

Cena 3D de teste manual que liga os módulos do SpellBook: um conjurador lança uma Bola de Fogo
num boneco de treino que não morre. A cena mostra o GAS (habilidade, custo, recarga, dano e
queimadura com tag), o Events (canal de dano recebido e canal de acerto), o Pooling (projéteis
reaproveitados) e o Time (pausa do grupo de gameplay e câmera lenta).

Abra `Scenes/Sandbox.unity` e entre em Play. A entrada usa o Input Manager legado, e os
materiais desenham no Built-in e no URP.

## Controles

A placa 3D da cena traz este mesmo texto:

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

## Passo a passo

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

Valores dos assets (vida do boneco 2000, mana 400, custo 20, dano 20, queimadura 5 a cada 0,5 s por 4 s, recarga 1,5 s) e
o motivo do relay de dano estão no manual.

O Console mostra cada conjuração, cada falha (recarga ou falta de mana), cada dano recebido com o
valor real e cada tique da queimadura. O manual do Sample está em
<https://gabrielctorres.github.io/SpellBook/exemplos/sandbox/>.
