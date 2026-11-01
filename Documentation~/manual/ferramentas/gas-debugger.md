# GAS Debugger {#gas-debugger}

O GAS Debugger é uma janela do Editor que mostra, em Play, o estado de um
[AbilitySystemComponent](../modulos/gas.md#abilitysystemcomponent) (ASC): atributos com os
modificadores, tags, efeitos ativos, habilidades com a recarga e o log dos eventos. Ela também tem
ações para testar o ASC sem escrever código: aplicar e remover efeito, conceder e retirar habilidade
e zerar a recarga.

A janela fica no assembly `SpellBook.GAS.Editor` e só existe no Editor. As linhas vêm do
`GasDebuggerModel` (namespace `SpellBook.GAS.Diagnostics`), que fica no runtime e lê o ASC só pela
API pública.

## Abrir {#abrir}

Menu **Window > SpellBook > GAS Debugger**. Fora do Play a janela mostra "Entre em Play para
inspecionar um ASC.".

## Escolher o ASC {#escolher-o-asc}

Há dois jeitos:

- selecione na Hierarchy um objeto com `AbilitySystemComponent`;
- arraste um ASC da cena para o campo **ASC** da barra de cima. Com o campo preenchido, a seleção é
  ignorada; esvazie o campo para voltar a seguir a seleção.

Ao trocar de ASC, o log recomeça. Sair do Play ou fechar a janela para a gravação.

## Blocos {#blocos}

A janela tem um bloco recolhível para cada parte do ASC, atualizado a cada 250 ms:

| Bloco | O que mostra |
|---|---|
| Atributos | cada atributo do `AttributeSet` com o valor atual, a base e o mínimo e o máximo efetivos; abaixo, um modificador por linha, com a operação, o valor e a fonte (o efeito que o aplicou ou o objeto dado por código) |
| Tags | as tags ativas em ordem alfabética, com quantas fontes concedem cada uma (`State.Stunned ×2`) |
| Efeitos | as instâncias ativas na ordem de aplicação, com a origem, o tempo restante, a duração aplicada (ou "infinito") e as pilhas |
| Habilidades | as habilidades concedidas, com o nível, se estão ativas e a recarga restante em segundos e em porcentagem, ou "pronta" |
| Log de eventos | os últimos 50 eventos do ASC, o mais recente primeiro |

## Ações {#acoes}

As ações só funcionam em Play, com um ASC escolhido:

- **Aplicar** (bloco Efeitos): aplica o efeito do campo **Efeito** com o próprio ASC como origem,
  como `TryApplyEffect`. O resultado (`Applied`, `Stacked`, `BlockedByImmunity` e os outros de
  [Resultado da aplicação](../modulos/gas.md#resultado-da-aplicacao)) aparece no Console.
- **Remover selecionado** (bloco Efeitos): remove a instância selecionada na lista, como
  `RemoveEffect`, com o motivo `Removed`.
- **Conceder** e **Retirar** (bloco Habilidades): concedem no nível 1 e retiram a habilidade do
  campo **Habilidade**, como `GiveAbility` e `RemoveAbility`.
- **Zerar recarga** (bloco Habilidades): zera a recarga da habilidade selecionada na lista, como
  `ResetCooldown`. Uma habilidade pronta não muda.

## Log de eventos {#log}

O log grava os eventos do ASC escolhido, com o tempo do ASC (`TickedTime`) em segundos:

| Evento | Detalhe |
|---|---|
| `EffectApplied` | o efeito que entrou |
| `EffectRemoved` | o efeito e o motivo (`Expired`, `Removed`, `RemovedByTag`, `StacksDepleted`) |
| `EffectStacksChanged` | o efeito e as pilhas antes e depois |
| `AbilityGranted`, `AbilityRemoved` | a habilidade |
| `CooldownChanged` | a habilidade e a mudança (`Started`, `Changed`, `Ended`) |

São os eventos de [Eventos do ASC](../modulos/gas.md#ciclo-de-vida). O log guarda os **últimos 50**:
quando chega o 51º, o mais antigo sai. O botão **Limpar log** apaga o log, e o botão **?** abre esta
página.

## Exemplo: conferir um veneno {#exemplo}

1. Na cena, selecione o objeto do jogador, com `AbilitySystemComponent` e `AttributeSet`.
2. Abra o GAS Debugger e entre em Play.
3. Arraste o efeito `Veneno` (com duração, modificador periódico em `Vida`) para o campo **Efeito** e
   clique em **Aplicar**.
4. O bloco Efeitos ganha `Veneno` com o tempo descendo; o log ganha `EffectApplied  Veneno`.
5. Selecione `Veneno` na lista e clique em **Remover selecionado**. O log ganha
   `EffectRemoved  Veneno (Removed)`, e a vida para de cair.
