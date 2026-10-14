# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

SpellBook is a **Unity UPM package** (`com.spellbook`, see `package.json`) of reusable tools, installed in other projects via Package Manager → "Add package from git URL" → `https://github.com/gabrielctorres/SpellBook.git`. The package root is the repo root: only `package.json`, `README.md`, `Runtime/` and `Editor/` (plus their `.meta` files) are tracked.

The `Assets/`, `Packages/`, `ProjectSettings/`, `Library/`, `Logs/`, `UserSettings/` folders are a local Unity 6 (6000.3.5f2) host project that is **untracked** and not part of the package — do not commit them (they are listed in `.gitignore`; still review `git status` before committing). `Assets/antigo.md` and the `.asset` files there are unrelated scratch work.

Code comments, log messages and commit messages are in Portuguese.

The package is consumed by other games (e.g. `gabrielctorres/PlaygroundRumble`), so keep it game-agnostic: no game-specific assets, tags or assumptions in package code. Issues for SpellBook are filed in the **PlaygroundRumble** repo with a `[SpellBook]` title prefix / `spellbook` label (`gh issue list -R gabrielctorres/PlaygroundRumble --label spellbook`). After pushing here, the consumer updates the package hash in its `Packages/packages-lock.json`.

## Building / testing

There is no lint or test suite. Compilation is verified by opening a Unity project that includes the package (e.g. PlaygroundRumble with `"com.spellbook": "file:../../SpellBook"` temporarily in its `Packages/manifest.json`) and checking the Console.

Quick compile check of `Runtime/` without the Editor, using Unity's bundled Roslyn (Git Bash; Odin DLL taken from PlaygroundRumble):

```bash
U="/c/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data"
find Runtime -name "*.cs" > /tmp/files.txt
REFS=""; for d in "$U/Managed/UnityEngine/"UnityEngine*.dll; do REFS="$REFS -r:\"$d\""; done
eval "\"$U/NetCoreRuntime/dotnet.exe\" \"$U/DotNetSdkRoslyn/csc.dll\" -nologo -noconfig -nostdlib -t:library -langversion:9 -out:/tmp/SpellBook.Core.dll \
  -r:\"$U/NetStandard/ref/2.1.0/netstandard.dll\" -r:\"$U/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll\" $REFS \
  -r:\"../PlaygroundRumble/Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll\" @/tmp/files.txt"
``` When bumping a release, update `version` in `package.json` (consumers pull by git URL).

Every file under `Runtime/` and `Editor/` needs a matching `.meta` file with a stable GUID; never regenerate or delete existing ones, since assets and asmdefs reference them by GUID. Because consumers install the package from git (immutable package), Unity will **not** generate missing metas there and ignores the asset, so new files must be committed with a `.meta` (random 32-hex `guid`).

## Assemblies

- `Runtime/SpellBook.Core.asmdef` — all runtime code, root namespace `SpellBook`.
- `Editor/SpellBook.Editor.asmdef` — Editor-only; references `SpellBook.Core` **by GUID** (`7834e8f3d0902ad45bfb6fab58f7b919`, from `SpellBook.Core.asmdef.meta`). Renaming the runtime asmdef is fine as long as its `.meta` GUID is kept.

Namespaces follow folders: `SpellBook.Core.Events`, `SpellBook.Core.NewEventSystem`, `SpellBook.Core.Variables`, `SpellBook.Core.RuntimeSets`, `SpellBook.MathTools`, `SpellBook.Editor[.Variables]`.

## Architecture

The core is a ScriptableObject-based architecture (Ryan Hipple / "Unite 2017" style): shared state and events live in assets so scene objects decouple through them.

- **Two event systems coexist:**
  - `Runtime/Core/Events/` (legacy) — untyped `GameEvent.Raise(Component sender, object data)` with `GameEventListner` MonoBehaviour forwarding to a `UnityEvent<Component, object>`.
  - `Runtime/Core/NewEventSystem/` (typed) — generic `BaseGameEvent<T>` (ScriptableObject) + `BaseGameEventListener<T, E, UER>` (MonoBehaviour registering in `OnEnable`/`OnDisable`, forwarding to a `UnityEvent<T>`). Adding a new event type means adding three thin classes: `XEvent : BaseGameEvent<X>` in `CustomEvents/` (with `[CreateAssetMenu(menuName = "SpellBook/NewEventSystem/...")]`), `UnityXEvent : UnityEvent<X>` in `UnityEvents/`, and `XListener : BaseGameEventListener<X, XEvent, UnityXEvent>` in `Listerners/`. Parameterless events use the `Void` struct.
  - Note existing spellings that are part of the public API: folder `Listerners`, class `GameEventListner`, method `RegisterLister`.
- **Variables** — `FloatVariable` (ScriptableObject) and `FloatReference` (serializable choice between a constant and a variable, implicitly converts to `float`); `Editor/FloatReferenceDrawer.cs` is its custom PropertyDrawer.
- **RuntimeSets** — `RuntimeSet<T>` ScriptableObject list for tracking live objects.
- **GAS** (`Runtime/GAS/`, namespaces `SpellBook.GAS.*`) — Gameplay Ability System inspired by Unreal's. `AbilitySystemComponent` (requires an `AttributeSet` on the same GameObject) owns ref-counted active tags, active `GameplayEffect` instances and the ability lifecycle. `GameplayAbility` is pure data (shared ScriptableObject); per-actor state is an `AbilitySpec` created by `GiveAbility` — `TryActivateAbility` returns `NotGranted` for abilities never given. The ASC runs the ability's `AbilityAction` list as a coroutine via `AbilityAction.RunSequence`, tracking running actions in the `AbilityContext` so `CancelAbility` (also triggered by `CancelledByTags` or `OnDisable`) can call `OnCancel` on them. Actions are shared instances: per-execution state belongs in the context, not in fields. `AttributeState` computes `CurrentValue` = (base + ΣAdd) × (1 + ΣMultiply), then last Override, clamped; Instant effects and periodic ticks instead write the base value directly. Extension points are `[SerializeReference]` lists of `AbilityAction` / `EffectAction` subclasses and ScriptableObject `TargetingStrategy` / `Formula` subclasses. See `Runtime/GAS/GAS_Documentation.md`. The GAS inspectors depend on **Odin Inspector** (`Sirenix.OdinInspector`), which the consuming project must have.
- **Systems** (`Runtime/Systems/`) — static, struct-based `EventManager` (`IEvent`) and a pooling system (`PoolManager`, `ComponentPooler`, `IPoolable`).
- **MathTools** — static helpers in `SpellBook.MathTools.Math.MathTools` (nested static class inside a MonoBehaviour).
- **Editor tools** — `Tools/Create Default Project Folders` menu item (`FolderStructureCreator`) scaffolds `Assets/_Project/...` in the consuming project.
