# 0037 — Cues: tempo no módulo, roteador por âncora, objetos rastreados pelo roteador e integração com o GAS só por ações

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O B-013 (PRD `cues.md`) cria o módulo `SpellBook.Cues`. A 0012 deixou em aberto "`SpellBook.Cues` precisa de `Time`?". A 0015 registrou a exceção do `PlayClipAtPoint` e do VFX que não é devolvido "até o módulo Cues". O PRD pede:

- um roteador achado sem singleton e sem `Find` (R8);
- VFX e som que voltam sozinhos ao spawner (R2, R3);
- cues `Burst` e `Looping` com handle (R7);
- uma lista de cues no `GameplayEffect` e no `GameplayAbility` tocada por um componente `AbilitySystemCueBridge` (R21 a R23).

O B-012 já deu a efeitos e habilidades as abas de ações por evento: ao aplicar, a cada tick e ao remover, no efeito; ao ativar, ao terminar e ao ser cancelada, na habilidade. A 0036 criou o vínculo `FollowsOwner`.

## Decisão

1. **`SpellBook.Cues` referencia Core, Tags e Time** (fecha o "Em aberto" da 0012). O roteador conta duração fixa e fim de clipe pelo `ITimeSource` dele (padrão: `TimeSourceAsset.Scaled`), para pausa e câmera lenta valerem. O spawner continua sem relógio (0015).
2. **O roteador é um componente de cena** (`CueRouter`) que se publica numa âncora asset (`CueRouterAnchor : RuntimeAnchor<CueRouter>`). `RuntimeAnchor<T>` nasce no Core, como a 0012 já prevê na tabela. Ele guarda a referência sem serializá-la e volta a vazio ao entrar em Play.
   - Assets (habilidades, efeitos) apontam a âncora.
   - Componentes de cena podem apontar o roteador direto.
   - Âncora vazia ou sem roteador: aviso uma vez em desenvolvimento, nada toca, sem exceção.
   - Não há roteador "padrão do projeto" global.
3. **Tudo o que um cue deixa no mundo é um objeto do spawner, rastreado pelo roteador.** Os itens (`CueItem`, `[SerializeReference]`) não guardam estado. Eles pedem ao roteador para rastrear cada objeto criado, com o spawner, a regra de fim (partículas paradas, duração fixa, ao ser removido), o alvo a seguir e o deslocamento. A cada `Tick`, o roteador:
   - move os objetos que seguem o alvo (posição, ou posição e rotação; **sem `SetParent`**, para um alvo destruído não levar junto o objeto do pool);
   - vê o fim de cada um;
   - devolve pelo mesmo spawner.

   Um item próprio do projeto (tremor de câmera, háptico) que precisa durar faz isso com um prefab, também rastreado.
4. **Handle com geração.** `CueHandle` é `(slot, geração)`. Um slot reutilizado invalida handles antigos, então `Remove` com handle velho não faz nada.
5. **A integração com o GAS é só por ações**, no `SpellBook.GAS.Bridges`:
   - `ExecuteCueAction`: toca um cue `Burst` por tag, numa aba de ação qualquer;
   - `AddCueAction`: liga um cue `Looping` e devolve o controle na hora. Uma execução de vigia `FollowsOwner` (0036) o remove quando o dono termina ou é cancelado: remoção do efeito por qualquer motivo, fim ou cancelamento da ativação.

   Os momentos do PRD viram abas. "Ao aplicar", "A cada tick" e "Ao remover" são `ExecuteCueAction` nas abas do efeito. "Enquanto ativo" é `AddCueAction` em "Ao ser aplicado". "Ao ativar", "Ao terminar" e "Ao ser cancelada" são as abas da habilidade. **Não entram** a lista de tags de cue no `GameplayEffect` e no `GameplayAbility` nem o `AbilitySystemCueBridge`. Empilhar não toca de novo, porque "Ao ser aplicado" roda só na criação da instância (B-003, `OnEffectApplied`).
6. **Fim do `PlayCueAction` e do `SpawnVFXAction` no GAS** (ex-`SpawnVFXEffectAction`). Feedback visual e sonoro é do módulo Cues. A exceção da 0015 termina, e `PlayClipAtPoint`, `Instantiate` e `Destroy` de VFX saem do GAS. Quem usa o GAS sem Cues não tem VFX pronto no pacote. Pode criar uma ação própria que chama um `SpawnerAsset`.

## Motivo

- Um mecanismo por coisa. A aba de ação já é "quando". A lista de cues no efeito seria uma segunda lista de "quando" ao lado dela, e o componente de ponte seria um terceiro lugar para lembrar de montar.
- O rastreio no roteador cobre `Burst`, `Looping`, seguir o alvo, limite de simultâneos e alvo destruído com a mesma estrutura, sem corrotina e sem estado nos itens (0013).
- A âncora resolve "asset aponta para objeto de cena" sem singleton (0015, item "não procurar por singleton").

## Consequências

- O PRD do B-013 muda nos R20 a R24: sem `AbilitySystemCueBridge` e sem lista de cues no efeito e na habilidade. O PRD do B-014 muda no R21: sem `SpawnVFXAction` como alternativa. As duas mudanças são perguntas ao usuário na Spec do B-013.
- A 0012 ganha, ao ser aceita esta ADR, uma emenda datada de uma linha fechando o "Em aberto" de `Time`. A 0015 ganha uma linha dizendo que a exceção terminou.
- O teste de `PlayClipAtPoint` vira guarda textual sobre o pacote inteiro.

## O que os agentes NÃO podem fazer

- Usar `SetParent` para prender um objeto de cue a um alvo.
- Guardar handle, objeto criado ou temporizador num `CueItem`.
- Criar lista de cues em `GameplayEffect`/`GameplayAbility` ou componente de ponte de cues no ASC.
- Usar `PlayClipAtPoint`, corrotina ou `Time.time` no Cues.

## Em aberto

Nada.

## Relacionadas

- 0012 (módulos), 0013 (estado), 0014 (tempo), 0015 (spawner), 0018 e 0034 (ações), 0036 (`FollowsOwner`), Spec `cues.md`.
