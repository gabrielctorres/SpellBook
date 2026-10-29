# 0014 — Tempo injetável (`ITimeSource`)

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O runtime lê o relógio da Unity diretamente:

- `Runtime/GAS/Core/AbilitySystemComponent.cs:56` avança os efeitos com `Time.deltaTime`;
- `Runtime/GAS/Effects/ActiveEffectInstance.cs:31` guarda `Time.time`;
- `Runtime/GAS/Abilities/Projectiles/Projectile.cs:156-200` usa `Time.deltaTime`;
- `WaitForSeconds` aparece em `Runtime/GAS/Abilities/Actions/WaitAction.cs:13`, `FlowActions.cs:28` (`RepeatAction`), `SpawnProjectileAction.cs:102` e `Projectile.cs:131,137`.

Assim não dá para testar duração, recarga ou tick sem esperar tempo real, nem pausar só um grupo de sistemas, nem usar tempo não escalado em UI.

## Decisão

1. O módulo `SpellBook.Time` define:
   - **`ITimeSource`**: contrato mínimo para obter o delta do frame e o tempo acumulado da fonte (proposta; membros definidos na Spec);
   - **`TimeSourceAsset`** (`ScriptableObject`): fontes reutilizáveis (escalado, não escalado, grupo pausável);
   - **`ManualTimeSource`**: fonte avançada à mão, para testes e simulação.
2. Todo componente do pacote que evolui no tempo:
   - tem um campo serializado de fonte de tempo; campo vazio usa a fonte escalada padrão;
   - expõe `Tick(float deltaTime)` público e uma opção de desligar o tick automático, para que testes e hosts avancem o tempo à mão.
3. Durações guardam o tempo **restante** e são decrementadas pelo delta recebido; nada guarda timestamp absoluto da Unity.
4. Só o `SpellBook.Time` lê `UnityEngine.Time`.

## Motivo

- Testes determinísticos e rápidos (0004).
- Pausa por grupo (por exemplo, pausar o jogo sem pausar a UI) e tempo não escalado vêm de graça.

## Consequências

- `WaitForSeconds` e corrotinas que dependem do relógio da Unity deixam de servir para ações com tempo; o modelo de execução de ações longas é decidido na 0018.
- A UI avança animações e recargas por um `UIUpdateDriver` que lê uma fonte de tempo (0017).

## O que os agentes NÃO podem fazer

- Usar `Time.time`, `Time.deltaTime`, `Time.unscaledDeltaTime`, `Time.realtimeSinceStartup` ou `WaitForSeconds` fora do `SpellBook.Time`.
- Guardar timestamp absoluto para medir duração.
- Usar tempo real em testes.

## Decisões incorporadas (2026-10-29)

Decisão I21: o módulo entra completo na 0-M (`ITimeSource`, `TimeSourceAsset` escalado, não escalado e grupo pausável, `ManualTimeSource`). As decisões técnicas abaixo são do Arquiteto.

- **Membros do `ITimeSource`:**
  ```csharp
  public interface ITimeSource
  {
      double ElapsedTime { get; } // tempo acumulado da fonte, em segundos
      float DeltaTime { get; }    // delta do quadro atual, já com pausa aplicada
  }
  ```
  Só consulta, sem evento de tick. Motivo: um evento obrigaria a fonte a ter um `Update` próprio (MonoBehaviour ou loop do player) e a definir ordem entre fontes e consumidores. Pausa e escala não entram no contrato: são detalhes da fonte que as tem.
- **Como o consumidor avança:** quem tem `autoTick` (ASC, `EventListener` com espera pendente, `Projectile`) guarda o último `ElapsedTime` lido e avança pela diferença. Isso funciona para várias fontes e vários consumidores, para a `ManualTimeSource` avançada entre quadros e para fontes paradas (diferença 0). O valor guardado é um cursor da fonte, não um timestamp da Unity; as durações continuam guardando o tempo **restante** (item 3).
- **`TimeSourceAsset`** é um `ScriptableObject` abstrato que implementa `ITimeSource`. Variantes no menu `Create > SpellBook > Time`: `ScaledTimeSource` (`Time.timeAsDouble`, `Time.deltaTime`), `UnscaledTimeSource` (`Time.unscaledTimeAsDouble`, `Time.unscaledDeltaTime`) e `PausableTimeSource`.
- **`ManualTimeSource`** é um `TimeSourceAsset` sem item de menu, criado por `ScriptableObject.CreateInstance` (factories). `Advance(float seconds)` soma ao `ElapsedTime` e define `DeltaTime`. Ser asset permite ligá-lo a qualquer campo `TimeSourceAsset` num teste (por exemplo, a reação Invoke com delay).
- **Grupo pausável (PRD P19):** segue o tempo **escalado** da Unity (o `Time.timeScale` global também o afeta), tem **só pausa**, sem escala própria, e expõe `Pause()`, `Resume()` e `SetPaused(bool)` públicos (chamáveis por `UnityEvent`) e `IsPaused`. Motivo: o caso de uso do C6 é pausar a gameplay e deixar UI e menus andando; a UI usa a fonte não escalada. Escala por grupo (câmera lenta só de um grupo) e escolha de base ficam para quando houver caso de uso (Inferido). Isso não muda nada visível além do que o R7a do PRD já descreve, então não volta ao PRD.
- **Estado sem domain reload (PRD P20):** ao entrar em Play, toda fonte com estado volta ao inicial: o grupo pausável despausa e zera o tempo acumulado, a `ManualTimeSource` zera. As instâncias se registram no `OnEnable` num registro estático do módulo, limpo em `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`, o mesmo mecanismo do R17 dos eventos. Motivo: estado de uma sessão de Play não pode vazar para a próxima, e o comportamento com e sem domain reload fica igual.
- **Passo fixo:** não há fonte própria. Lido dentro de `FixedUpdate`, o `DeltaTime` da fonte escalada devolve o passo fixo, porque a própria Unity devolve `Time.fixedDeltaTime` em `Time.deltaTime` nesse contexto. O `Projectile` avança por diferença de `ElapsedTime`, então a fonte não precisa saber se está no `Update` ou no `FixedUpdate`.
- **Timer/agendador:** fora da 0-M (PRD seção 6). Entra com a 0018 ou com o primeiro caso de uso.
- **Esperas das ações do GAS na 0-M:** `WaitAction`, `RepeatAction`, o intervalo do `SpawnProjectileAction` e os tempos do `Projectile` deixam de usar `WaitForSeconds` e passam a contar pelo relógio do ASC (o que o `Tick` recebeu) ou pela fonte do próprio componente. As corrotinas continuam como mecanismo de execução até a 0018.
- **Ferramentas de Editor** (Event Debugger) podem ler `Time.frameCount` e `EditorApplication.timeSinceStartup` para carimbar o histórico; a proibição do item 4 vale para os assemblies de runtime.

## Em aberto

Nada.

## Relacionadas

- 0004 (testes), 0012 (módulo Time), 0017 (UI), 0018 (execução de ações).
