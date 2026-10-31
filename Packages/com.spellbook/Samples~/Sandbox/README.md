# Sandbox

Cena 3D de teste manual que liga os módulos do SpellBook: um conjurador lança uma Bola de Fogo
num boneco de treino que não morre. A cena mostra o GAS (habilidade, custo, recarga, dano e
queimadura com tag), o Events (canal de dano recebido e canal de acerto), o Pooling (projéteis
reaproveitados) e o Time (pausa do grupo de gameplay e câmera lenta).

Abra `Scenes/Sandbox.unity` e entre em Play. A entrada usa o Input Manager legado, e os
materiais desenham no Built-in e no URP.

## Controles

| Tecla | Ação | O que mostra |
|---|---|---|
| Mouse | Mira no chão | O conjurador gira para o ponto mirado |
| `1` | Conjura a Bola de Fogo | Custo de mana, recarga, projétil do pool, dano e queimadura |
| `P` | Pausa e retoma o jogo | O grupo de tempo do gameplay para: efeitos, recarga e projéteis |
| `T` | Alterna a câmera lenta (escala 0,25) | Tudo o que segue o tempo escalado fica mais lento |
| `R` | Reinicia a cena | Vida, mana, recarga, pausa e escala voltam ao início |

O Console mostra cada conjuração, cada falha (recarga ou falta de mana), cada dano recebido com o
valor real e cada tique da queimadura. O manual do Sample está em
<https://gabrielctorres.github.io/SpellBook/exemplos/sandbox/>.
