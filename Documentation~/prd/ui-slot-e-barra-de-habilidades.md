# PRD: UI-2, slot, barra de habilidades e barra de cast
Backlog: B-016 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção H.5 (slot, `AbilityLoadout`, `IKeyLabelSource`), seção H.6 (passo 8 e 9), seção E (UI-2). ADRs [0017](../adr/0017-ui-em-tres-camadas.md) (UI em três camadas), [0022](../adr/0022-ordem-das-checagens-de-ativacao.md) (ordem das checagens e motivo da falha), [0019](../adr/0019-hierarquia-unica-de-condition.md) (motivo por chave), [0023](../adr/0023-texto-estruturado-e-itextprovider.md) (texto), [0014](../adr/0014-tempo-injetavel-itimesource.md) (tempo) e [0015](../adr/0015-spawnerasset-como-seam-de-spawn-e-pool.md) (slots em pool).
Depende de: **B-015** (a base: `IView<T>`, `UISource<T>`, binder, `ActorResolver`, `UIUpdateDriver`, tema, widgets uGUI), **B-003** (`OnAbilityGranted/Removed`, controle e leitura de recarga), **B-009** (cargas, `OnChargesChanged`, `ActivationFailure`, ordem das checagens) e **B-008** (texto do motivo por chave e `ITextProvider`). Para o estado de toggle, canalização e autocast **e para a barra de cast** (4.8): **B-012** (R26, leitura de estado e de progresso da canalização). **A parte da barra de cast (4.8) só começa depois do B-012**; o restante deste item não espera por ele. Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6).
**Sample:** depois de B-015 e B-016, o Sample Sandbox (B-032) ganha o **HUD (S4)**, em **uGUI** e com a entrada pelo **Input Manager legado** (decisões do usuário, 2026-10-31; ver 4.7).
Este PRD não tem foco em nenhum jogo. As menções ao `AbilitySlotProvider` do jogo no plano são histórico.

---

## 1. Problema (contexto)

Mostrar as habilidades do jogador na tela é o que cada jogo mais reescreve:

- **Slot feito à mão.** O jogo do plano tem um `AbilitySlotProvider` de 423 linhas, com um acesso estático ao jogador, que lê recarga por reflexão e procura `UseChargeAction` no asset para achar as cargas.
- **Não há "quais habilidades aparecem, em qual ordem".** Sem um dado que ligue "tecla Q" à habilidade, a barra é montada em código.
- **Recarga, custo e cargas sem fonte.** A UI não tem leitura pública de recarga restante, cargas atuais e "pronta" (B-003 e B-009 as entregam; falta quem as junte num modelo).
- **O motivo da falha é invisível.** O jogador aperta o botão e nada acontece: não sabe se faltou mana, se está em recarga ou sem alvo (B-009 dá o motivo; falta mostrá-lo).
- **Sem rótulo de tecla.** O texto "Q" ou "Botão A" vem do jogo, preso ao sistema de entrada.
- **Concessão e remoção não atualizam a tela.** Ganhar ou perder uma habilidade em runtime não muda a barra.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, **pelo Inspector e sem script** no caso comum:

1. definir um **`AbilityLoadout`** (asset): a lista ordenada de pares (tag de slot, habilidade);
2. ter um **slot de habilidade** que mostra ícone, **recarga** (radial e segundos), **cargas**, **custo insuficiente**, **bloqueio por tag**, **estado** (pronto, em recarga, sem recurso, bloqueado, ativo) e um **pulso de "pronto"**;
3. ter uma **barra de habilidades** que **gera os slots** a partir do loadout e acompanha a **concessão e a remoção** de habilidades;
4. ver o **motivo da falha** de uma tentativa de ativação (qual checagem falhou e por quê), por chave de texto, no slot;
5. mostrar o **rótulo da tecla** por uma interface própria (`IKeyLabelSource`), sem o pacote conhecer o sistema de entrada;
6. **ver o slot em edição** (prévia com 50 % de recarga), sem Play;
7. usar uma **view própria** (`IView<SlotModel>`) sem tocar na fonte;
8. ter uma **barra de cast** que mostra o progresso da **canalização** (Channel) em curso, com nome, ícone e tempo restante, e o resultado (concluída ou cancelada), a partir do B-012 (P4 respondida: entra neste item).

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico / de UI** | Montar a barra de habilidades com um loadout e um prefab de slot; ver a prévia; escolher como o motivo da falha aparece |
| **Desenvolvedor de gameplay** | Conceder uma habilidade e ver o slot aparecer; `IKeyLabelSource` do projeto; nenhuma busca de dono |
| **Desenvolvedor de UI com kit próprio** | Implementar `IView<SlotModel>` e reaproveitar fonte, binder e loadout |
| **Mantenedor do pacote** | Um modelo de slot que a view lê sem conhecer o GAS; o GAS expõe só eventos e leituras |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, loadout:** o loadout `Slot.Q → Fireball`, `Slot.W → Heal`. A barra gera dois slots, na ordem da lista. Mudar a ordem no asset muda a ordem visual.
- **C2, recarga:** ativar `Fireball` (recarga 8 s): o slot mostra "em recarga", fração restante e segundos; passado 8 s, volta a "pronto" e dispara o **pulso de pronto** uma vez. O tempo vem do `ITimeSource` (pausa pausa o radial).
- **C3, recarga reduzida:** com redução de recarga no ASC, a duração mostrada é a **real** (normalizada, B-002 e B-003), não a do asset.
- **C4, cargas:** `MaxCharges = 3` (B-009). O slot mostra "3", ao gastar "2" e o radial da **próxima carga**; sem cargas mostra o estado "sem carga".
- **C5, custo insuficiente:** com mana menor que o custo, o slot mostra "sem recurso" (por `GetActivationResult` sem efeito colateral, B-009), e volta a "pronto" quando a mana sobe.
- **C6, bloqueio por tag:** o ASC atordoado (tag bloqueadora) mostra o slot "bloqueado", e volta ao sair a tag.
- **C7, motivo da falha:** o jogador tenta ativar com mana baixa. O slot recebe o `ActivationFailure` (B-009) e mostra a **chave** do motivo (e a tag quando há) por um tempo curto; o texto vem do `ITextProvider` (B-008). A ordem da falha segue a da ADR 0022: tags, recarga ou cargas, custo, condições.
- **C8, concessão e remoção:** conceder uma habilidade listada no loadout acende o slot; removê-la o apaga (estado "vazio"), por `OnAbilityGranted/Removed` (B-003), sem `Update`.
- **C9, nível e ativa:** habilidade em canalização, toggle ligado ou com autocast ligado mostra o estado "ativo" (B-012); sem o B-012 o modelo deixa esses campos em falso.
- **C10, tecla:** o `IKeyLabelSource` do teste devolve "Q"; o slot mostra o texto; trocar o rótulo atualiza o slot.
- **C11, dono:** o slot encontra o ASC por `ActorResolver` (âncora do jogador, B-015); se o jogador renasce, religa.
- **C12, prévia (S):** no host, o slot em edição mostra ícone, 50 % de recarga e "2" cargas, sem Play.
- **C13, slots em pool:** a barra cria os slots pelo `SpawnerAsset`; remover da lista devolve ao pool; a contagem de vivos não cresce.
- **C14, sem lixo:** 10 000 atualizações de slot (recarga correndo, cargas, estado) não alocam memória gerenciada depois do aquecimento.
- **C15, Inspector (S):** no host, o binder do slot e o `AbilityLoadout` mostram resumo de uma linha e validação no topo.
- **C16, barra de cast, progresso:** uma habilidade Channel de 3 s. Ao ativar, a barra aparece com nome e ícone, a fração sobe de 0 a 1 e os segundos restantes descem, pelo `ITimeSource` (pausa pausa a barra). Ao fim dos 3 s a barra marca "concluída" e some.
- **C17, barra de cast, cancelamento:** cancelar a canalização por código ou por tag bloqueadora faz a barra marcar "cancelada" (a view mostra de outro jeito) e sumir; o resultado fica no modelo por um contador, como o motivo de falha do slot.
- **C18, barra de cast, sem duração:** Channel sem duração (até cancelar, B-012 P4) mostra a barra no modo **indeterminado** (sem fração, só "ativa"), sem segundos restantes.
- **C19, barra de cast, sem canalização:** fora de uma canalização a barra não existe para o modelo (estado `Idle`); habilidade `Instant`, `Toggle` ou `Passive` **não** acende a barra.
- **C20, barra de cast, dono e sem lixo:** a barra acha o ASC pelo `ActorResolver` (religa no respawn); 10 000 ticks de progresso não alocam depois do aquecimento.

## 4. Requisitos funcionais

### 4.1 `AbilityLoadout`

R1. **`AbilityLoadout`.** Um `ScriptableObject` (`Create > SpellBook > UI > Ability Loadout`) com uma **lista ordenada** de entradas (`SlotTag` (`GameplayTag`) e `GameplayAbility`). A ordem da lista é a ordem visual (H.5 do plano). O mesmo loadout serve a vários jogadores ou personagens; a concessão da habilidade ao ASC **não** é feita por ele (P3).

R2. **Leitura.** O loadout é um asset de leitura; trocar o loadout de um binder em runtime é permitido por campo e atualiza a barra. Sem alocação ao ler.

### 4.2 Modelo e fonte do slot (`SpellBook.UI` e `SpellBook.GAS.UI`)

R3. **`SlotModel`** (struct, em `SpellBook.UI`): ícone, **estado** (`Empty`, `Ready`, `OnCooldown`, `NoResource`, `Blocked`, `NoCharges`, `Active`), **fração de recarga** (0 a 1) e segundos restantes, **cargas atuais** e **máximas** (com "tem cargas?"), fração da **recarga da próxima carga**, **rótulo da tecla**, **último motivo de falha** (chave de texto, tag e um contador que muda a cada falha) e o contador do **pulso de pronto**. Sem GAS, sem string montada: o texto sai da view.

R4. **`AbilitySlotSource`.** Em `SpellBook.GAS.UI`: lê, do ASC dono, para uma habilidade ou para uma tag de slot do loadout: recarga restante (normalizada e em segundos), cargas, custo, estado das tags e o último `ActivationFailure`. Atualiza por **push** (eventos C# do ASC: recarga, efeito aplicado e removido, `OnChargesChanged`, tags, mudança de atributo do custo, falha de ativação) e só pede tick enquanto há recarga ou carga correndo, pelo `UIUpdateDriver` (ADR 0017, item 1). O estado "pode ativar?" usa **a mesma consulta sem efeito colateral** da ativação (`GetActivationResult`, B-009 R11 e R13): a UI nunca mostra "pronto" quando a ativação diria "não pode".

R5. **`AbilityLoadoutSource` e a barra.** Lê o loadout e o ASC, e produz a lista de slots; escuta `OnAbilityGranted/Removed` (B-003). O componente de barra (`AbilityBarBinder`) cria um slot por entrada pelo `SpawnerAsset` (ADR 0015) e o devolve ao remover; slot sem habilidade concedida fica no estado `Empty` (a view decide se o esconde, P2).

R6. **Estado.** A ordem de decisão do estado segue a ADR 0022 (tags, cargas ou recarga, custo, condições): o estado mostrado é o do **primeiro motivo** que falha, e `Ready` só quando nada falha. `Active` (canalizando, toggle ligado, autocast ligado) vem do estado lido de B-012 e **não** substitui o estado de falha.

R7. **Motivo da falha.** O slot guarda o **último** `ActivationFailure` (resultado, chave, tag) por um tempo configurável (padrão 1 s, pelo `ITimeSource`) e o contador sobe a cada falha, para a view tremer ou piscar. O texto vem do `ITextProvider` (B-008, R16 daquele PRD), nunca é montado no GAS. A falha **do autocast é silenciosa** (B-012, P8) e **não** aparece no slot.

R8. **Pulso de pronto.** Quando uma habilidade deixa de estar em recarga (ou volta uma carga) o modelo incrementa o contador do pulso. Não pulsa ao conceder a habilidade nem ao primeiro desenho.

### 4.3 Rótulo de tecla

R9. **`IKeyLabelSource`.** Interface (ponto de extensão `[SerializeReference]`, ADR 0013) que devolve o texto da tecla para um slot. O pacote entrega **uma** implementação por texto fixo configurado no loadout ou no slot. **Nenhuma** implementação do pacote depende do Input System nem do Input Manager (o projeto escreve a sua; o Sample usa o Input Manager legado, 4.7).

### 4.4 Views padrão (`SpellBook.UI.UGUI`)

R10. **View do slot (uGUI + TextMeshPro).** O pacote entrega uma view e um **prefab** de slot: ícone, máscara radial de recarga, segundos, contador de cargas, rótulo da tecla, estado (escurecer quando sem recurso ou bloqueado), pulso de pronto e o aviso do motivo da falha. E uma view da **barra** (layout horizontal por padrão). São de protótipo e Sample (ADR 0017, consequências).

R11. **Clique no slot.** O slot é **só exibição**: a view **não ativa** a habilidade. A ativação é da entrada do jogo (P1).

### 4.5 Prévia em edição

R12. **Prévia.** Cada fonte tem um `PreviewModel` (slot com ícone da habilidade do loadout, 50 % de recarga, "2" cargas, rótulo "Q"), mostrado fora do Play (H.7 do plano, regra da base do B-015). Com o loadout, a barra mostra a ordem dos slots em edição.

### 4.6 Editor, validação e documentação

R13. **Inspector** (UI Toolkit, ADR 0026, `SpellBookEditor`): `AbilityLoadout` com a lista (resumo de uma linha: "Slot.Q · Fireball · recarga 8 s"), o binder do slot e da barra com fonte, resolvedor e view, e a validação no topo.

R14. **Validação** (`IValidatable`): loadout vazio (aviso); entrada sem tag ou sem habilidade; `SlotTag` repetida no mesmo loadout; habilidade repetida (aviso); binder sem fonte, sem view ou sem loadout; barra sem prefab de slot; habilidade do loadout **sem** ícone (aviso); habilidade com cargas sem `ChargesConfig` válido (aviso do B-009 repetido). Mensagens clicáveis.

R15. **Documentação.** Páginas **Slot de habilidade** e **Barra de habilidades** (loadout, estados, recarga, cargas, motivo da falha, rótulo da tecla, como criar uma view e um `IKeyLabelSource` próprios). Os tipos novos têm `[HelpURL]` (decisão I15). O CHANGELOG cita `AbilityLoadout`, `SlotModel`, `AbilitySlotSource`, `AbilityLoadoutSource` e `IKeyLabelSource`.

### 4.7 Dependência com o Sample

R16. **O Sample ganha o HUD (S4) depois.** O Sample Sandbox (B-032) **não** é alterado por este item. Depois de B-015 e B-016, o **S4** (barras de vida e mana, indicador de recarga, números de dano) entra como complemento do Sandbox, **em uGUI** e com a entrada pelo **Input Manager legado** (decisões do usuário, 2026-10-31); o `IKeyLabelSource` do Sample é um texto fixo por slot. O PRD ou a emenda do S4 é do Sample.

### 4.8 Barra de cast (depende do B-012)

R17. **`CastBarModel`** (struct, em `SpellBook.UI`): **estado** (`Idle`, `Channeling`), ícone e nome da habilidade (por chave, ADR 0023), **fração** (0 a 1) e **segundos restantes**, marca **indeterminada** (canalização sem duração) e **último resultado** (`Completed` ou `Cancelled`) com um contador que muda a cada fim. Sem GAS e sem string montada.

R18. **`ChannelSource`.** Em `SpellBook.GAS.UI`: lê do ASC dono a **canalização em curso** (habilidade, duração, progresso, B-012 R26) e escuta o início, o passo, o fim e o cancelamento por **push** (eventos C# do ASC). Pede tick ao `UIUpdateDriver` **só enquanto há canalização com duração** (ADR 0017, item 1). O tempo vem do `ITimeSource`. Não altera o ASC e não ativa nem cancela nada (a barra é **só exibição**, como o slot, R11). A duração mostrada é a **real** da instância em curso, não a do asset.

R19. **Uma canalização por vez.** A fonte mostra uma canalização por ASC. Se o B-012 permitir canalizações simultâneas, a regra de qual aparecer (a mais recente) é do Arquiteto (P7 do Arquiteto); o modelo continua com uma só.

R20. **View padrão (`SpellBook.UI.UGUI`).** Uma view e um **prefab** de barra de cast: preenchimento, nome, ícone, segundos restantes, modo indeterminado, e um feedback distinto para concluída e cancelada. É de protótipo e Sample (ADR 0017, consequências). A view não conhece o GAS.

R21. **Prévia, Inspector, validação e documentação.** A fonte tem um `PreviewModel` (barra em 60 %, "Fireball", 1,2 s) mostrado fora do Play; o binder segue o Inspector do R13. O validador cobre: binder sem fonte, sem view ou sem prefab (aviso, como R14). Página **Barra de cast**, `[HelpURL]` nos tipos novos e CHANGELOG citando `CastBarModel` e `ChannelSource`.

## 5. Requisitos não funcionais

N1. **Alocação:** atualizar o slot (recarga, cargas, estado) e criar e devolver slots não alocam depois do aquecimento (C14). Evento sem ouvinte não aloca.
N2. **Módulos (ADR 0012, 0017):** `SlotModel`, `IKeyLabelSource` e os contratos em `SpellBook.UI`; fontes em `SpellBook.GAS.UI` (GAS, UI, Tags, Time; **não** Bridges); views em `SpellBook.UI.UGUI`. A view não conhece o GAS nem a fonte. O GAS não referencia UI. O teste de assemblies cobre.
N3. **Tempo e estado:** só `ITimeSource`; a fonte guarda **configuração**, nunca o estado da ligação (ADR 0013); duas barras do mesmo asset não compartilham estado.
N4. **Um caminho só:** o "pronto" da UI e o resultado da ativação usam a mesma checagem (B-009 R13).
N5. **Determinismo:** testes com `ManualTimeSource`, `GasFactory` e uma view de teste, sem Canvas real nos testes de lógica; a view uGUI tem teste PlayMode no host.
N6. **Serialização e asmdefs:** nativa; a **Spec** cria os asmdefs, **não** este PRD (ADR 0012, emenda 0026).

## 6. Fora do escopo

- **A base de UI (contratos, resolvedor, driver, tema, barra de atributo, números flutuantes):** **B-015**.
- **Tooltip do slot** (ao passar o mouse): **B-017**; o slot só expõe o gancho para o `TooltipTrigger`.
- **Lista de buffs:** **B-018**.
- **Tempo de conjuração antes da ativação** (cast point com `CommitMode.Manual`, B-012 R30): a barra de cast mostra só a **canalização** (Channel). Barra para espera de cast point fica fora (P6).
- **Recarga, cargas, `ActivationFailure`, ordem das checagens e controle de recarga:** **B-003** e **B-009**. **Toggle, canalização, autocast e a leitura de estado:** **B-012**. **Texto do motivo e `ITextProvider`:** **B-008**.
- **Ativar a habilidade pelo slot** (clique, toque, arrastar e soltar), **rearranjar slots em runtime** e **salvar o loadout do jogador:** fora (P1); do jogo.
- **Conceder habilidades por loadout:** o loadout só **lista** o que mostrar (P3).
- **Teclas e entrada:** fora; só a interface do rótulo.
- **Animação e efeitos do slot** além do pulso, do tremor e do escurecer: do kit do projeto.
- **UI Toolkit em runtime.**
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

- CA1 (T). Cenário C1: a barra gera os slots na ordem do loadout; mudar a ordem muda a ordem visual.
- CA2 (T). Cenário C2: o slot passa de pronto a em recarga e volta, com fração e segundos pelo `ManualTimeSource`; o pulso dispara uma vez; em pausa o radial não anda.
- CA3 (T). Cenário C3: com redução de recarga, a duração mostrada é a real.
- CA4 (T). Cenário C4: as cargas, a recarga da próxima carga e o estado sem carga seguem o ASC.
- CA5 (T). Cenário C5: custo insuficiente mostra "sem recurso" pela consulta sem efeito colateral e volta quando o recurso sobe.
- CA6 (T). Cenário C6: tag bloqueadora mostra "bloqueado" e volta ao sair.
- CA7 (T). Cenário C7: o motivo da falha chega ao slot com a chave (e a tag) na ordem da ADR 0022; o texto vem do provedor; a falha de autocast não aparece.
- CA8 (T). Cenário C8: conceder e remover atualiza o slot por evento, sem `Update`.
- CA9 (T). Cenário C9: o estado ativo vem do B-012 e não substitui a falha; sem o B-012 fica falso.
- CA10 (T). Cenário C10: o `IKeyLabelSource` do teste alimenta o rótulo e atualiza ao mudar.
- CA11 (T). Cenário C11: o dono por âncora religa ao respawn.
- CA12 (S). Cenário C12: a prévia aparece em edição.
- CA13 (T). Cenário C13: os slots vêm do `SpawnerAsset` e voltam; a contagem de vivos não cresce.
- CA14 (T). Cenário C14: 10 000 atualizações alocam 0 bytes gerenciados depois do aquecimento.
- CA15 (S). Cenário C15: no host, `AbilityLoadout` e binders mostram o resumo e a validação.
- CA16 (T). O "pronto" do slot e o resultado de `TryActivateAbility` coincidem em todos os casos da ordem das checagens (N4).
- CA17 (T). O teste de assemblies da 0-M passa (N2); a view não referencia o GAS; nenhuma implementação do pacote referencia sistema de entrada.
- CA18 (T). O validador cobre os casos do R14, com o objeto clicável.
- CA19 (E). O CHANGELOG cita os itens do R15; as páginas existem; os tipos novos têm `[HelpURL]`; nenhum tipo do módulo usa singleton, `Find`, `Instantiate` ou `Destroy` em regime, `Time.time` nem corrotina da Unity (busca de código).
- CA20 (E). O PRD do Sample (B-032) fica sem alteração neste item; a nota do R16 está registrada.
- CA21 (T). Cenário C16: a barra de cast sobe de 0 a 1 e os segundos descem pelo `ManualTimeSource`; ao fim marca "concluída"; em pausa não anda.
- CA22 (T). Cenário C17: cancelar por código e por tag marca "cancelada" e o contador de resultado muda.
- CA23 (T). Cenário C18: canalização sem duração mostra o modo indeterminado, sem fração nem segundos.
- CA24 (T). Cenário C19: habilidade `Instant`, `Toggle` e `Passive` não acende a barra.
- CA25 (T). Cenário C20: o dono por âncora religa; 10 000 ticks de progresso alocam 0 bytes gerenciados depois do aquecimento; a barra de cast não ativa nem cancela a habilidade.
- CA26 (S/E). A prévia da barra de cast aparece em edição; a página existe, os tipos novos têm `[HelpURL]` e o CHANGELOG cita `CastBarModel` e `ChannelSource`.

## 8. Perguntas em aberto

### Com o usuário

- **P1. Clicar no slot ativa a habilidade?** *Respondida (usuário, 2026-10-31): aceita. Slot só exibição (R11); sem `SlotClickActivator`.*
- **P2. Slot sem habilidade concedida.** *Respondida (usuário, 2026-10-31): aceita. O estado `Empty` vai à view, que por padrão mostra a moldura vazia, com a opção de esconder (R5).*
- **P3. Loadout e concessão.** *Respondida (usuário, 2026-10-31): aceita. O loadout só lista; não concede (R1).*
- **P4. Barra de cast.** *Respondida (usuário, 2026-10-31): **a barra de cast entra neste item** (diferente da proposta). Seção 4.8, C16 a C20, CA21 a CA26; essa parte depende do Channel do B-012 e só começa depois dele.*
- **P5. Estados de toggle, canalização e autocast no slot.** *Respondida (usuário, 2026-10-31): aceita. O `SlotModel` traz `Active` e o campo de autocast, preenchidos só quando o B-012 existe (R3, R6).*
- **P6. Barra de cast para o cast point.** *Respondida (usuário, 2026-10-31): aceita. A barra mostra **só a canalização** (Channel); a espera de cast point fica fora (seção 6, C19).*

### Com o Arquiteto (decisão técnica da Spec)

- **P1. Eventos do ASC que o `AbilitySlotSource` escuta** sem polling (recarga que começa e termina, cargas, falha de ativação, mudança do custo): o que B-003 e B-009 já entregam e o que falta.
- **P2. Tempo da recarga:** a fonte lê fração e segundos do ASC a cada tick do driver ou calcula pelo instante final (sem consultar a cada quadro).
- **P3. Estado `NoResource` sem efeito colateral:** como a fonte consulta `GetActivationResult` sem alocar a cada mudança de atributo do custo, e quais atributos observa.
- **P4. Slot de uma habilidade versus slot por tag** (R4): como a fonte mapeia tag de slot e habilidade quando o ASC concede a mesma habilidade duas vezes (instâncias, B-003 P4).
- **P5. `IKeyLabelSource` por `[SerializeReference]`** com genéricos (ADR 0013) e onde mora (`SpellBook.UI`).
- **P6. Ordem entre tickets:** loadout, modelo e fonte do slot por leitura de recarga (B-003) primeiro; cargas e motivo da falha depois (B-009, B-008); barra e `AbilityLoadoutSource` com concessão; views uGUI e prévia; estado `Active` e barra de cast por último (B-012).
- **P7. Canalização e a barra de cast** (R17 a R19): que leitura pública o B-012 (R26) dá de habilidade em canalização, duração real e progresso; eventos de início, passo, fim e cancelamento; o que fazer com canalizações simultâneas; como o `ChannelSource` pede tick só com duração.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-016, do plano (H.5, H.6, UI-2) e das ADRs 0017, 0019, 0022, 0023, 0014 e 0015; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador): o HUD do Sample (S4) usa uGUI e o Input Manager legado quando o módulo de UI existir; o Sample ganha o HUD depois de B-015 e B-016 | R16, cabeçalho | Aplicado |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-015 (base), B-003, B-009, B-008, B-012; tooltip e lista de buffs ficam em B-017 e B-018 | Seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1, P2, P3, P5: propostas aceitas (slot só exibição; `Empty` mostrado vazio; loadout só lista; `Active` no modelo). Critério de design: sistema modular, sem remendos | Seção 8 (sem mudança de texto nos requisitos) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P4: a barra de cast entra no B-016, depende do Channel do B-012 | Objetivo 8, C16 a C20, 4.8 (R17 a R21), CA21 a CA26, cabeçalho, seções 6 e 8 (P6, P7) | Aplicado |
| 2026-10-31 | usuário (via Controlador) P6: a barra de cast mostra **só a canalização**; cast point fica fora | Seção 8 (P6 respondida); seção 6 e C19 já diziam isso | Aplicado |
