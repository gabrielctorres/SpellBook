# Pooling {#pooling}

Esta página mostra como o SpellBook cria e devolve objetos de jogo, como projéteis e partículas.
O `SpawnerAsset` fica no Core. O pool de objetos fica no módulo `SpellBook.Pooling` (assembly
`SpellBook.Pooling`, namespace `SpellBook.Pooling`), que depende só do Core.

## SpawnerAsset {#spawnerasset}

O `SpawnerAsset` é um asset que cria uma instância de um prefab e depois a devolve. Ele fica no
assembly `SpellBook.Core`, no namespace `SpellBook.Core`.

### Por que existe {#por-que-existe}

Os sistemas do pacote não escolhem como um objeto é criado. Eles recebem um `SpawnerAsset` num
campo do Inspector e pedem a ele a instância. Assim, você troca a estratégia (Instantiate, pool,
Addressables) trocando o asset do campo, sem mexer no sistema. E nenhum sistema depende de um
`PoolManager` na cena.

O contrato tem dois métodos:

```csharp
public abstract GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);
public abstract void Release(GameObject instance);
```

### Padrão por Instantiate {#instantiate}

O `InstantiateSpawnerAsset` é a implementação padrão: o `Spawn` usa `Instantiate` e o `Release`
destrói a instância. Fora do Play, o `Release` usa `DestroyImmediate`, porque o `Destroy` só
roda em Play.

Para criar o asset: clique com o botão direito no Project → **Create → SpellBook → Spawn →
Instantiate Spawner**.

Um campo de spawner vazio usa o `SpawnerAsset.Default`, um `InstantiateSpawnerAsset` criado
sob demanda e que não é salvo em disco. Em código, use `SpawnerAsset.OrDefault(campo)` para
ter o spawner do campo ou o padrão:

```csharp
using SpellBook.Core;

[SerializeField] private SpawnerAsset spawner;

GameObject vfx = SpawnerAsset.OrDefault(spawner).Spawn(prefab, position, rotation);
```

### Criar o seu próprio spawner {#proprio-spawner}

Herde de `SpawnerAsset` e implemente os dois métodos. Adicione um `[CreateAssetMenu]` para criar
o asset pelo menu:

```csharp
using SpellBook.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "MeuJogo/Spawner com contagem")]
public class CountingSpawnerAsset : SpawnerAsset
{
    public int Alive { get; private set; }

    public override GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        Alive++;
        return Instantiate(prefab, position, rotation, parent);
    }

    public override void Release(GameObject instance)
    {
        if (instance == null) return;
        Alive--;
        Destroy(instance);
    }
}
```

### Quem devolve {#quem-devolve}

O objeto volta pelo **mesmo** spawner que o criou. Quem chamou o `Spawn` guarda o spawner usado
e chama o `Release` dele quando o objeto não serve mais. Um objeto que se devolve sozinho, como
um projétil, recebe o spawner na configuração e chama `spawner.Release(gameObject)`.

### ReleaseWhenParticlesStop {#releasewhenparticlesstop}

Para uma partícula sem dono, que só precisa sumir quando acabar, chame
`ReleaseWhenParticlesStop` logo depois do `Spawn`:

```csharp
SpawnerAsset vfxSpawner = SpawnerAsset.OrDefault(spawner);
GameObject vfx = vfxSpawner.Spawn(prefab, position, rotation);
vfxSpawner.ReleaseWhenParticlesStop(vfx);
```

O método troca o **Stop Action** do `ParticleSystem` da raiz da instância para **Callback** e
adiciona um componente interno que chama `Release` desse spawner quando a partícula para. Ele
não conta tempo: a devolução acontece quando o `ParticleSystem` termina.

## PooledSpawnerAsset {#pooledspawnerasset}

O `PooledSpawnerAsset` é o `SpawnerAsset` com pool. Use-o no campo **Spawner** de qualquer
sistema para reaproveitar instâncias em vez de criar e destruir.

Para criar o asset: clique com o botão direito no Project → **Create → SpellBook → Pooling →
Pooled Spawner**.

| Campo | Padrão | O que faz |
|---|---|---|
| **Initial Size** | 5 | Instâncias criadas quando o pool de um prefab nasce, no primeiro `Spawn` dele. Não pode ser negativo. |
| **Can Expand** | ligado | Cria uma instância nova quando o pool está vazio. Desligado, o `Spawn` devolve `null`. |

Como funciona:

- O pool de cada prefab é criado no primeiro `Spawn` desse prefab. Não precisa de `PoolManager`
  na cena.
- Os pools ficam embaixo de um objeto raiz `[SpellBook Pools]`, criado na hora. Em Play ele é
  `DontDestroyOnLoad`, então os pools sobrevivem à troca de cena.
- O `Release` devolve a instância ao pool de onde ela saiu. Uma instância que não veio de um pool
  é destruída.
- Ao entrar em Play, o asset esquece os pools da sessão anterior, mesmo sem domain reload.

```csharp
using SpellBook.Core;
using UnityEngine;

[SerializeField] private SpawnerAsset spawner; // arraste um PooledSpawnerAsset aqui

GameObject bullet = spawner.Spawn(prefab, position, rotation);
// ...quando o objeto não serve mais:
spawner.Release(bullet);
```

O validador acusa erro se **Initial Size** for negativo.

## PoolManager (pré-aquecimento) {#poolmanager}

O `PoolManager` é um componente de cena que cria pools no `Awake`, antes do primeiro uso. Ele é
opcional e o `PooledSpawnerAsset` não o procura: os pools dele são separados.

Cada pool pré-aquecido vem de um `PoolConfiguration` (**Create → SpellBook → Pooling → Pool
Configuration**), com o prefab, o tamanho inicial e se pode crescer. Coloque as configurações na
lista **Starting Pools** do `PoolManager`.

O `PoolManager` é um singleton com `DontDestroyOnLoad`. Os sistemas do pacote não usam o
`PoolManager.Instance`; use um `PooledSpawnerAsset` no campo **Spawner** deles.

## ComponentPooler e IPoolable {#componentpooler}

O `ComponentPooler` é o pool de um prefab: uma fila de instâncias desativadas. O
`PooledSpawnerAsset` e o `PoolManager` criam um por prefab. Cada instância ganha um
`PoolableObject`, que guarda o pool de origem. O `PoolableObject.ReturnToPool()` devolve a
instância a esse pool, ou a destrói se ela não veio de um.

Para reiniciar o estado de um objeto ao sair e ao voltar do pool, implemente `IPoolable` num
componente da raiz do prefab:

```csharp
using SpellBook.Pooling;
using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    public void OnSpawnFromPool() { /* reinicia vida, velocidade... */ }
    public void OnReturnToPool() { /* para efeitos, limpa alvos... */ }
}
```

Se o objeto também pode nascer por `Instantiate`, prefira reiniciar no `OnEnable`, que roda nos
dois casos. É o que o `Projectile` do GAS faz.

## Armadilhas {#armadilhas}

- **Devolva pelo spawner que criou.** Devolver por outro spawner pode destruir um objeto que
  deveria voltar para um pool, ou o contrário.
- **O `ParticleSystem` precisa estar na raiz do prefab.** O `ReleaseWhenParticlesStop` lança
  `ArgumentException` se a raiz não tiver um. Partículas só nos filhos não são observadas.
- **Partícula em loop nunca para.** Com **Looping** ligado, a partícula não termina sozinha e o
  objeto não é devolvido. Desligue o **Looping** ou chame `Stop()` no `ParticleSystem`.
- **O Stop Action é trocado.** Um **Stop Action** configurado no prefab (Destroy, Disable) é
  substituído por **Callback** na instância.
- **O pool cresce se nada devolve.** Com **Can Expand** ligado, cada `Spawn` sem `Release`
  cria uma instância nova. O VFX do `PlayCueAction` não é devolvido (veja a
  [página GAS](gas.md#acoes-de-spawn)), então com um `PooledSpawnerAsset` o pool dele cresce a
  cada uso até o módulo Cues.
- **Devolver duas vezes coloca a instância duas vezes na fila.** Chame `Release` uma vez por
  `Spawn`.
