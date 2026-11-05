# Sandbox

A 3D scene for manual testing that wires the SpellBook modules together: a caster throws a Fireball
at a training dummy that never dies. The scene shows GAS (ability, cost, cooldown, damage and a burn
with a tag), Events (a damage-taken channel and a hit channel), Pooling (reused projectiles) and Time
(pausing the gameplay group and slow motion).

Open `Scenes/Sandbox.unity` and enter Play mode. Input uses the legacy Input Manager, and the
materials render in both Built-in and URP.

## Controls

The 3D sign in the scene shows this same text:

```text
SpellBook Sandbox
Mouse: aim at the ground (the caster turns)
1: Fireball (cost, cooldown, pool, damage, burn)
P: pause and resume the game (gameplay time group)
T: slow motion, scale 0.25 (scaled time)
R: restart (health, mana, cooldown, pause and scale)
```

| Key | Action | What it shows |
|---|---|---|
| Mouse | Aims at the ground | The caster turns toward the aimed point |
| `1` | Casts the Fireball | Mana cost, cooldown, pooled projectile, damage and burn |
| `P` | Pauses and resumes the game | The gameplay time group stops: effects, cooldown and projectiles |
| `T` | Toggles slow motion (scale 0.25) | Everything that follows scaled time slows down |
| `R` | Restarts the scene | Health, mana, cooldown, pause and scale go back to the start |

## Walkthrough

### S1. Cast and see the damage

1. Enter Play mode and aim the mouse at the dummy, about 8 m away.
2. Press `1`. The Console shows "Fireball cast", and the fireball comes out of the pool (in the
   Hierarchy, the object is activated under `[SpellBook Pools]`).
3. On impact, the dummy flashes and the Console shows "Fireball hit" and "Damage taken: 20".
4. The burn color stays for 4 s. Every 0.5 s the Console shows "Burn: tick" and
   "Damage taken: 5".
5. Select the dummy and the caster to see Health and Mana in the Inspector (`AttributeReadout`).

### S2. Cooldown, mana and refresh

1. Press `1` twice within 1.5 s. The second press shows "Fireball on cooldown: N.N s".
2. Cast again during the burn: the duration goes back to 4 s, with no second burn running alongside.
3. Spend the mana (20 casts). The next one shows "Not enough mana for Fireball". Press `R` to start over.

### S3. Pause and slow motion

1. Cast and press `P` while the fireball is in flight: projectile, burn and cooldown stop. `P` again resumes.
2. Press `T`: the scale goes to 0.25 and everything that follows scaled time slows down. `T` again returns it to 1.
3. `R` restarts the scene and sets the scale back to 1, also while the game is paused or in slow motion.

The asset values (dummy health 2000, mana 400, cost 20, damage 20, burn 5 every 0.5 s for 4 s, cooldown 1.5 s)
and the reason for the damage relay are in the manual.

The Console shows each cast, each failure (cooldown or lack of mana), each damage taken with the
actual value and each burn tick. The Sample's manual page is at
<https://gabrielctorres.github.io/SpellBook/exemplos/sandbox/>.
