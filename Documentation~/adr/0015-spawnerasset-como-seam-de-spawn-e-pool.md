# 0015 — `SpawnerAsset` como ponto único de spawn e pool

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

Os sistemas do pacote criam objetos de três jeitos diferentes:

- `PoolManager.Instance` (singleton) em `Runtime/GAS/Abilities/Actions/SpawnProjectileAction.cs:78,83` e `Runtime/GAS/Abilities/Actions/PlayCueAction.cs:37`, com `NullReferenceException` se não houver `PoolManager` na cena;
- `Instantiate` e `Destroy` em `SpawnVFXEffectAction` (`Runtime/GAS/Effects/Actions/CommonEffectActions.cs:42,58`);
- `AudioSource.PlayClipAtPoint` em `PlayCueAction.cs:41`.

Com os módulos (0012), Cues e o GAS não podem depender de `SpellBook.Pooling`, mas precisam criar objetos com pool quando o usuário quiser.

## Decisão

1. O `SpellBook.Core` define o `ScriptableObject` abstrato **`SpawnerAsset`**, com o contrato de criar uma instância de um prefab numa posição e rotação e de devolver essa instância (proposta; assinatura definida na Spec).
2. A implementação padrão, no Core, usa `Instantiate`/`Destroy`. O `SpellBook.Pooling` fornece `PooledSpawnerAsset`.
3. Todo sistema do pacote que cria objetos de jogo em runtime (projéteis, VFX, áudio de cue, números flutuantes, itens de lista de UI, thinkers) recebe um `SpawnerAsset` por campo serializado. Campo vazio usa a implementação padrão, e o validador avisa.
4. O objeto é devolvido pelo mesmo spawner que o criou.

## Motivo

- Elimina a NRE do singleton ausente e a dependência do GAS e de Cues em Pooling.
- Quem usa o pacote troca a estratégia (pool próprio, Addressables) criando outra subclasse, sem tocar nos sistemas.

## Consequências

- O `PoolManager` singleton deixa de ser o ponto de entrada dos sistemas do pacote.
- Prefabs que voltam ao pool precisam de um caminho para se devolver (por exemplo, ao fim da duração do VFX), definido na Spec.

## O que os agentes NÃO podem fazer

- Usar `PoolManager.Instance`, `Instantiate`, `Destroy` ou `PlayClipAtPoint` para objetos de gameplay fora de uma implementação de `SpawnerAsset`.
- Fazer o GAS, Cues ou UI referenciarem `SpellBook.Pooling`.
- Procurar spawner por singleton, `Find` ou canal global.

## Decisões incorporadas (2026-10-29)

- **Spawn e cue no GAS** (decisões I22 e I37): `SpawnProjectileAction`, `Projectile`, `SpawnVFXEffectAction` e `PlayCueAction` ficam em `SpellBook.GAS` e criam objetos só pelo `SpawnerAsset`. O `PlayCueAction` só troca `PoolManager.Instance` pelo spawner; o `AudioSource.PlayClipAtPoint` e a falta de devolução do VFX continuam até o módulo Cues (B-013), como exceção registrada ao item "O que os agentes NÃO podem fazer".
- **Assinatura** (decisão técnica):
  ```csharp
  public abstract class SpawnerAsset : ScriptableObject
  {
      public abstract GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);
      public abstract void Release(GameObject instance);
      public static SpawnerAsset Default { get; } // InstantiateSpawnerAsset criado sob demanda, HideFlags.DontSave
      public static SpawnerAsset OrDefault(SpawnerAsset spawner);
  }
  ```
  Trabalha com `GameObject` e devolve a instância. Motivo: é o que pool, Instantiate e os chamadores atuais já usam; um handle ou um `Component` genérico não traz ganho na 0-M e complicaria o `[SerializeField]` do campo.
- **Implementação padrão:** `InstantiateSpawnerAsset` (Core, item de menu `Create > SpellBook > Spawn > Instantiate Spawner`), com `Instantiate` e `Destroy`. Em EditMode usa `DestroyImmediate`, porque `Destroy` não roda fora do Play.
- **Quem devolve:** o sistema que criou guarda o `SpawnerAsset` usado e chama `Release` (por exemplo, o `SpawnVFXEffectAction` no `Cleanup` do efeito). Um objeto que se devolve sozinho recebe o spawner na configuração (`Projectile.Setup(spawner, …)`) e chama `spawner.Release(gameObject)`. Para VFX de partícula sem dono, o Core oferece `SpawnerAsset.ReleaseWhenParticlesStop(GameObject instance)`, que liga o `stopAction` da partícula a um componente interno que chama `Release`, sem ler o relógio.
- **`PoolManager` mantido** (0012): o `PooledSpawnerAsset` cria um `ComponentPooler` por prefab, sob demanda, embaixo de um objeto raiz próprio (`[SpellBook Pools]`, `DontDestroyOnLoad` só em Play). O `PoolManager` da cena, se existir, continua só como pré-aquecimento e não é procurado pelo spawner. O cache de pools do asset é limpo ao entrar em Play sem domain reload (mesmo mecanismo de 0014 e 0016).
- **`IPoolable` continua no Pooling:** o `Projectile` deixa de implementar `IPoolable` e reinicia o estado no `OnEnable`, que roda tanto no Instantiate quanto ao sair do pool.
- **Validador:** campo de spawner vazio não gera aviso na 0-M (a regra mínima do R35 não o pede). Fica para o primeiro épico que tiver um caso em que o padrão por Instantiate seja um erro (Inferido).

## Em aberto

Nada.

## Relacionadas

- 0012 (módulos), 0013 (critério SO vs `[SerializeReference]`), 0017 (UI).
