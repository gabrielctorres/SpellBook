# Tempo {#tempo}

O módulo Tempo dá ao SpellBook um relógio que você pode trocar. Em vez de ler `Time.deltaTime`
direto, um componente lê uma **fonte de tempo**. Assim dá para pausar só a gameplay, deixar a UI
andando no tempo não escalado e testar durações sem esperar tempo real.

O módulo fica no assembly `SpellBook.Time` (namespace `SpellBook.Time`). Um asmdef seu que usa as
fontes precisa referenciar o `SpellBook.Time`. Veja [Conceitos](../inicio/conceitos.md#modulos).

## ITimeSource {#itimesource}

Toda fonte implementa a mesma interface, só de consulta:

```csharp
public interface ITimeSource
{
    double ElapsedTime { get; } // tempo acumulado da fonte, em segundos
    float DeltaTime { get; }    // delta do quadro atual, já com a pausa da fonte aplicada
}
```

A interface não tem evento de tick nem `Update`. Quem consome a fonte é que a lê quando precisa.

## Fontes prontas {#fontes}

| Fonte | `ElapsedTime` | `DeltaTime` | Menu |
|---|---|---|---|
| `ScaledTimeSource` | `Time.timeAsDouble` | `Time.deltaTime` | **Create → SpellBook → Time → Scaled Time Source** |
| `UnscaledTimeSource` | `Time.unscaledTimeAsDouble` | `Time.unscaledDeltaTime` | **Create → SpellBook → Time → Unscaled Time Source** |
| `PausableTimeSource` | tempo escalado sem os trechos pausados | `Time.deltaTime`, ou 0 se pausado | **Create → SpellBook → Time → Pausable Time Group** |
| `ManualTimeSource` | soma dos `Advance` | último `Advance` | sem menu |

Todas herdam de `TimeSourceAsset`, um `ScriptableObject`. Por isso uma fonte é um asset que você
arrasta para qualquer campo do tipo `TimeSourceAsset`.

### ScaledTimeSource {#scaledtimesource}

Segue o `Time.timeScale` da Unity: com `timeScale = 0`, o tempo para. É a fonte padrão. Um campo de
fonte vazio usa `TimeSourceAsset.Scaled`, uma instância criada em memória e nunca salva em disco.
Você só precisa criar um asset escalado se quiser deixar a escolha explícita no Inspector.

Lida dentro de `FixedUpdate`, ela devolve o passo fixo, porque a própria Unity devolve o
`Time.fixedDeltaTime` em `Time.deltaTime` nesse contexto.

### UnscaledTimeSource {#unscaledtimesource}

Ignora o `Time.timeScale`. Use em UI, menus e tudo o que precisa andar com o jogo pausado.

### Grupo pausável {#pausabletimesource}

O `PausableTimeSource` serve para pausar parte do jogo sem mexer no `Time.timeScale`. O caso
típico é pausar a gameplay e deixar a UI andando:

1. Crie um asset em **Create → SpellBook → Time → Pausable Time Group**, por exemplo `Gameplay`.
2. Ligue todos os componentes de gameplay a esse asset.
3. Ligue a UI e os menus a uma `UnscaledTimeSource`, ou deixe-os no tempo escalado padrão.
4. Chame `Pause()` e `Resume()` no asset `Gameplay`.

Todos os consumidores que leem o mesmo asset param e voltam juntos. Enquanto pausado, o
`DeltaTime` é 0 e o `ElapsedTime` não muda. Ao retomar, o `ElapsedTime` continua do valor em que
parou, sem salto.

Os métodos são públicos e sem retorno, então dá para chamá-los por `UnityEvent` no Inspector, sem
código. Arraste o asset para o evento `OnClick` de um botão e escolha
`PausableTimeSource.Pause` ou `PausableTimeSource.Resume`. Num `Toggle`, use `SetPaused(bool)`
como função dinâmica (o valor do toggle vira o estado de pausa). Por código:

```csharp
[SerializeField] private PausableTimeSource gameplay;

public void AbrirMenu()  { gameplay.Pause(); }
public void FecharMenu() { gameplay.Resume(); }
```

O grupo **segue o tempo escalado** da Unity e **só pausa**: não tem escala própria. Por isso:

- com `Time.timeScale = 0`, o grupo também para, mesmo sem estar pausado;
- com `Time.timeScale = 0.5`, o grupo anda na metade da velocidade, como o tempo escalado;
- para câmera lenta só na gameplay, ainda não há fonte pronta. Crie a sua herdando de
  `TimeSourceAsset` (veja [Estado entre sessões de Play](#reset)).

`IsPaused` diz se o grupo está pausado. `Pause()` com o grupo já pausado e `Resume()` com ele
andando não fazem nada.

### ManualTimeSource {#manualtimesource}

Só anda quando você chama `Advance(seconds)`. Cada chamada soma `seconds` ao `ElapsedTime` e
define o `DeltaTime` como `seconds`. Um valor negativo ou NaN lança `ArgumentOutOfRangeException`.

Ela não tem item de menu. Crie por código, em testes ou simulações:

```csharp
var tempo = ScriptableObject.CreateInstance<ManualTimeSource>();
tempo.Advance(1.5f);   // ElapsedTime 1.5, DeltaTime 1.5
tempo.Advance(0.5f);   // ElapsedTime 2.0, DeltaTime 0.5
// ...
Object.DestroyImmediate(tempo);   // no TearDown do teste
```

Como é um `TimeSourceAsset`, ela entra em qualquer campo de fonte. O teste fica determinístico e
rápido, sem `WaitForSeconds` nem relógio real.

## Consumo por diferença {#consumo-por-diferenca}

O jeito certo de consumir uma fonte é guardar o último `ElapsedTime` lido (o **cursor**) e avançar
pela diferença:

```csharp
using SpellBook.Time;
using UnityEngine;

public sealed class Cronometro : MonoBehaviour
{
    [SerializeField] private TimeSourceAsset timeSource;   // vazio: fonte escalada padrão
    [SerializeField] private float duracao = 3f;

    private ITimeSource _fonte;
    private double _cursor;
    private float _restante;

    private void OnEnable()
    {
        _fonte = timeSource != null ? timeSource : TimeSourceAsset.Scaled;
        _cursor = _fonte.ElapsedTime;   // sem salto no primeiro quadro
        _restante = duracao;
    }

    private void Update()
    {
        double agora = _fonte.ElapsedTime;
        _restante -= (float)(agora - _cursor);
        _cursor = agora;
    }
}
```

- O cursor recebe o valor atual ao ligar o componente e ao trocar de fonte. Sem isso, o primeiro
  quadro recebe de uma vez todo o tempo que a fonte já acumulou.
- Guarde o tempo **restante** e decremente. Não guarde um instante absoluto para comparar depois.
- Funciona com várias fontes e vários consumidores, com a `ManualTimeSource` avançada entre
  quadros e com uma fonte parada (a diferença é 0).

## Estado entre sessões de Play {#reset}

Ao entrar em Play, toda fonte com estado volta ao inicial. A `ManualTimeSource` volta a 0. O
`PausableTimeSource` volta despausado e o `ElapsedTime` dele recomeça do 0, mesmo que você tenha
saído do Play com o grupo pausado. Isso acontece com e sem domain reload (opção **Enter Play Mode Options**), pelo
`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, então o tempo de uma sessão não vaza para a
próxima.

Se você criar sua própria fonte herdando de `TimeSourceAsset`:

- sobrescreva `ResetState()` para zerar o estado;
- se sobrescrever `OnEnable` ou `OnDisable`, chame a base: é ela que registra a instância para o
  reset.

## Armadilhas {#armadilhas}

### O nome `Time` em `namespace SpellBook.*` {#armadilha-nome-time}

Dentro de um `namespace SpellBook.Alguma.Coisa`, o nome `Time` passa a ser o namespace
`SpellBook.Time`, e não mais a classe `UnityEngine.Time`. Código assim deixa de compilar quando o
assembly referencia o `SpellBook.Time`:

```csharp
namespace SpellBook.MeuJogo
{
    public class Exemplo : MonoBehaviour
    {
        private void Update()
        {
            float dt = Time.deltaTime;   // erro: 'Time' é um namespace
        }
    }
}
```

Para corrigir, leia uma fonte de tempo (o recomendado) ou escreva `UnityEngine.Time.deltaTime`
por extenso.

### Testes com tempo real {#armadilha-tempo-real}

Teste que espera segundos de verdade é lento e instável. Use a `ManualTimeSource` e avance o tempo
à mão.
