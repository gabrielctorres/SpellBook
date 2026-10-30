# Pooling {#pooling}

Esta página mostra como o SpellBook cria e devolve objetos de jogo, como projéteis e partículas.
Por enquanto ela cobre o `SpawnerAsset`, que fica no Core. O pool de objetos vai entrar nesta
página junto com o módulo `SpellBook.Pooling`.

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

## Armadilhas {#armadilhas}

- **Devolva pelo spawner que criou.** Devolver por outro spawner pode destruir um objeto que
  deveria voltar para um pool, ou o contrário.
- **O `ParticleSystem` precisa estar na raiz do prefab.** O `ReleaseWhenParticlesStop` lança
  `ArgumentException` se a raiz não tiver um. Partículas só nos filhos não são observadas.
- **Partícula em loop nunca para.** Com **Looping** ligado, a partícula não termina sozinha e o
  objeto não é devolvido. Desligue o **Looping** ou chame `Stop()` no `ParticleSystem`.
- **O Stop Action é trocado.** Um **Stop Action** configurado no prefab (Destroy, Disable) é
  substituído por **Callback** na instância.
