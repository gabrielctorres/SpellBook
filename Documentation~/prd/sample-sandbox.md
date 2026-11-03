# PRD: Sample Sandbox, cena de teste manual dos módulos
Backlog: B-032 (adianta o item `Samples~` do B-019) · Issue: a criar depois do gate · Status: Rascunho (v2, respostas do usuário aplicadas; emenda do HUD S4 de 2026-11-01 aprovada no gate de 2026-11-01, seção 10) · Data: 2026-10-31

## 1. Problema

Hoje não há como ver o SpellBook funcionando como um todo. Os testes EditMode e PlayMode provam cada módulo isolado, mas ninguém abre uma cena, aperta Play e vê uma habilidade acertar um alvo e deixá-lo queimando. Antes de levar o pacote a outros projetos, o usuário precisa de um lugar para jogar e observar cada sistema funcionando junto, e para perceber rápido quando algo quebrou.

Quem instala o pacote em outro projeto também não tem exemplo de como ligar GAS, Events, Pooling e Time. O `package.json` e o plano já prevêem `Samples~`, mas a pasta não existe. O exemplo de UI/HUD fica para depois do módulo de UI (ver seção 6).

## 2. Objetivo

Existe um Sample chamado **Sandbox**, importável pelo Package Manager (aba Samples do pacote). Ao importar e abrir a cena dele, o usuário aperta Play e, só com teclado e mouse:

1. conjura uma habilidade com projétil que acerta um alvo, causa dano e o deixa queimando por um tempo, gastando mana e entrando em recarga;
2. vê o evento de dano recebido chegar ao alvo e a um ouvinte de log por canal, sem referência direta entre eles;
3. vê projéteis saírem do pool e voltarem a ele, e pausa o jogo ou o põe em câmera lenta pelo `SpellBook.Time`.

O feedback visual desta entrega **não é HUD**: cor do alvo, mensagens no Console e o Event Debugger. Barras e números na tela esperam o módulo de UI.

A cena é conferida **à mão**. Não há teste automático dela (decisão do usuário, 2026-10-31).

## 3. Usuários e cenários de uso

| Usuário | O que espera do Sandbox |
|---|---|
| **Mantenedor do pacote** | Jogar a cena depois de uma mudança e ver na hora se algum módulo quebrou |
| **Designer técnico** | Abrir os assets da cena (habilidade, efeito, canais) e copiá-los como ponto de partida |
| **Desenvolvedor de gameplay** | Ver como os módulos se ligam e quais assemblies a cena referencia |

Cenários (cada um é um trecho da cena, jogável sem sair do Play). O alvo **não morre** em nenhum deles:

- **S1, GAS:** o jogador mira com o mouse e aperta a tecla da habilidade "Bola de Fogo". Um projétil sai do conjurador, acerta o alvo (boneco de treino), aplica dano imediato e um efeito de queimadura (dano periódico por X segundos). O custo de mana é descontado ao conjurar e a habilidade entra em recarga.
- **S2, Events:** o dano recebido vira evento em um canal (asset). O alvo e um ouvinte de log reagem ouvindo o canal, sem apontar um para o outro nem para o conjurador.
- **S3, Pooling + Time:** cada projétil (e o efeito de impacto, se houver) vem de um pool e volta a ele. Uma tecla pausa o gameplay e outra liga a câmera lenta; durações, recarga, queimadura e projéteis obedecem.

O antigo S4 (UI/HUD) saiu desta entrega (seção 6).

## 4. Requisitos funcionais

### 4.1 Entrega

R1. O Sandbox fica em `Samples~/Sandbox/` do pacote e é declarado em `samples` no `package.json` (`displayName`, `description`, `path`).
R2. Importar o Sample pelo Package Manager copia cena, assets (habilidade, efeitos, canal, atributos, tags, pool) e scripts de apoio para `Assets/Samples/SpellBook/<versão>/Sandbox/`, e a cena abre sem erro de compilação nem de referência faltando.
R3. Os scripts de apoio da cena (entrada do jogador, boneco de treino, feedback) ficam num asmdef do próprio Sample, que referencia só os assemblies do pacote que a cena usa. Nada do Sample entra em `Runtime/`.
R4. O Sample não depende de pacote ou plugin de terceiros e não depende de módulo de UI do pacote.

### 4.2 Controles

R5. Controles padrão (podem mudar na Spec, mas os do exemplo precisam existir): mouse para mirar, `1` conjura a Bola de Fogo, `P` pausa e retoma o gameplay, `T` alterna câmera lenta, `R` reinicia a cena (vida, mana, recarga e alvo voltam ao início).
R6. A cena informa os controles e uma linha curta do que cada um demonstra. O meio de exibir esse texto sem módulo de UI (ex.: `OnGUI`, texto 3D, `README` ao lado da cena) é **decisão do Arquiteto**, e não pode virar HUD de barras ou números.
R7. O Sandbox lê a entrada pelo **Input Manager legado** (`UnityEngine.Input`), sem exigir que o projeto de destino mude as configurações. Decisão do usuário, 2026-10-31.

### 4.3 S1, GAS

R8. O conjurador tem atributos de vida e mana e uma habilidade "Bola de Fogo" com: custo de mana, recarga, e uma ação de projétil que, no impacto, aplica **dano imediato** e o **efeito de queimadura** (duração e período, usando `Duration` e `Period` do efeito).
R9. O alvo é um boneco de treino 3D com ASC próprio e atributo de vida. O dano e a queimadura reduzem a vida dele, e a queimadura acaba sozinha ao fim da duração. O alvo não morre: a cena não trata vida zero. Como garantir isso (vida alta com `R` para reiniciar, ou piso de vida) é **decisão do Arquiteto**.
R10. O projétil que erra o alvo some ao fim do `Lifetime` e volta ao pool, sem causar dano.
R11. Sem mana suficiente ou em recarga, a habilidade não dispara e o motivo é registrado no Console (ver R21).
R12. Reaplicar a queimadura em um alvo que já queima segue a política de empilhamento do efeito (renova ou empilha), e o comportamento é o mesmo descrito na legenda da cena.

### 4.4 S2, Events

R13. O dano recebido trafega por um canal do módulo Events (asset na pasta do Sample), com o valor do dano. Se o relay do ASC (B-004) não existir na hora da Spec, o payload pode ser fixo; a escolha é **decisão do Arquiteto** (antiga Q2).
R14. O alvo (reação visual) e um ouvinte de log ouvem o canal com `EventListener` e reações prontas, ou por código com `EventBus<T>`. Eles não guardam referência direta ao ASC que originou o evento.
R15. Quem dispara o evento do lado do GAS usa o que o pacote já tem (`RaiseEventAction` no impacto) ou o que existir quando a Spec for escrita.
R16. O Event Debugger mostra os disparos de **dano recebido** durante o Play, com quem ouviu.

### 4.5 S3, Pooling + Time

R17. Projéteis (e o efeito de impacto, se houver) vêm de um `PooledSpawnerAsset` e voltam ao pool. Conjurar várias vezes seguidas não aumenta o número de objetos instanciados além do tamanho do pool.
R18. A cena usa um `TimeSourceAsset` do tipo grupo pausável para o gameplay: ASCs, efeitos, recarga e projéteis o usam.
R19. `P` pausa o grupo: queimadura, recarga e projéteis em voo param no lugar; ao retomar, continuam do ponto onde pararam. A leitura de `P` segue funcionando durante a pausa.
R20. `T` põe o gameplay em câmera lenta e volta ao normal ao apertar de novo. A escala afeta o gameplay do mesmo jeito que a pausa. O mecanismo (`Time.timeScale` ou fonte nova de escala própria, que seria mudança no módulo Time) é **decisão do Arquiteto** (antiga Q6). O valor da escala é registrado no Console ao alternar.

### 4.6 Feedback visual sem HUD

R21. Mensagens curtas, em português, no Console: acerto (com o valor, quando houver), tique da queimadura, falha de conjuração (sem mana, em recarga), pausa, câmera lenta e reinício.
R22. O alvo muda de cor ao receber dano e enquanto queima, e volta à cor normal quando a queimadura acaba. Vida e mana atuais ficam visíveis no Inspector durante o Play.
R23. O feedback não usa Canvas, barra, nem número flutuante. Isso é do HUD futuro.

## 5. Requisitos não funcionais

- Compatível com a Unity 6000.0 ou mais nova, a mesma do pacote. Roda na versão do host de testes (6000.3.13f1).
- Sem alocação por quadro causada pelo próprio Sample nos caminhos quentes (projétil, feedback). O Sample não pode ensinar um mau padrão.
- Os assets do Sample não têm GUIDs que colidam com os do pacote, e importar o Sample duas vezes (versões diferentes) não quebra o projeto.
- Os assets e scripts do Sample seguem as regras do pacote (validação do `SpellBook > Validar projeto` sem erros nos assets do Sample).
- Textos em português.
- O Sample não mexe em `ProjectSettings` do projeto de destino.

## 6. Fora do escopo

- **S4, UI/HUD** (barras de vida e mana, indicador de recarga na tela, números de dano flutuantes, mensagens na tela). Sai desta entrega por decisão do usuário (2026-10-31) e fica **condicionado ao módulo de UI (B-015 e B-016)**. Quando entrar, usará uGUI (decisão do usuário, alinhada ao `UI.UGUI` do B-015) e será um complemento do Sandbox, com PRD ou emenda própria.
- Morte do alvo, evento de morte e o B-010 (`OnDepleted`). Cortados por decisão do usuário.
- Teste PlayMode ou EditMode automático da cena (decisão do usuário).
- UI-3 (tooltip e descrição estruturada) e UI-4 (lista de buffs e debuffs).
- Teams, Cues, thinkers, auras e qualquer módulo que ainda não exista no pacote.
- Arte final: vale cubos, esferas, partículas simples. O visual não é critério.
- 2D. A cena é 3D (decisão do usuário).
- Multiplayer, save, IA do alvo (o alvo só fica parado e recebe dano).
- Os outros itens do B-019 (wizard, tabela de balanceamento, simulador, overlay de debug, wizard "Create Event Type", DocFX).
- Sample separado por módulo (Pooling só, Events só). Esta entrega é uma cena única; os cenários C1 a C10 do PRD da 0-M continuam nos testes.

**Dependência futura:** HUD do Sandbox depende de B-015 e B-016 (módulo de UI com uGUI).

## 7. Critérios de aceite

Todos conferidos à mão, numa Unity 6000.3.13f1, num projeto vazio com o pacote instalado.

**Entrega**
- [ ] CA1. O Package Manager lista o Sample "Sandbox" na aba Samples do SpellBook, e **Import** termina sem erros no Console.
- [ ] CA2. A cena do Sample abre, sem erro de compilação, sem "Missing" em nenhum componente e sem aviso de referência perdida ao apertar Play.
- [ ] CA3. O projeto de teste não tem nenhum pacote ou plugin de terceiros instalado e a cena funciona, sem módulo de UI do pacote.
- [ ] CA4. Nenhum arquivo do Sample está em `Runtime/` ou `Editor/` do pacote.
- [ ] CA5. A cena lê a entrada só pelo Input Manager legado e funciona sem alterar `ProjectSettings` do projeto de teste.

**GAS (S1)**
- [ ] CA6. `1` com mira no alvo faz sair um projétil que, ao acertar, tira vida do alvo e inicia a queimadura.
- [ ] CA7. A vida do alvo (Inspector) continua caindo em passos regulares durante a queimadura e para quando ela acaba. A duração observada bate com a configurada (tolerância de 0,5 s).
- [ ] CA8. A mana cai pelo custo ao conjurar. Sem mana suficiente, nada é disparado e a mensagem aparece no Console.
- [ ] CA9. Durante a recarga, `1` não dispara e a mensagem aparece no Console. Ao fim, volta a disparar.
- [ ] CA10. Um projétil que não acerta ninguém some ao fim do tempo de vida e não causa dano.
- [ ] CA11. Em nenhum momento do uso normal o alvo morre, some ou dispara tratamento de vida zero.

**Events (S2)**
- [ ] CA12. Cada acerto dispara o canal de dano recebido; o ouvinte de log registra o evento e o alvo reage (cor).
- [ ] CA13. Os scripts do alvo e do ouvinte de log não têm campo de referência para o ASC do conjurador (conferido no Inspector).
- [ ] CA14. O Event Debugger lista os disparos de dano recebido, com os ouvintes, durante o Play.

**Pooling + Time (S3)**
- [ ] CA15. Conjurar 20 vezes seguidas (esperando a recarga) não cria mais projéteis do que o tamanho do pool; os objetos do pool aparecem na Hierarchy ativando e desativando.
- [ ] CA16. `P` congela queimadura, recarga e projéteis em voo; ao retomar, eles continuam do ponto em que estavam (sem pulo de vida nem de recarga).
- [ ] CA17. Durante a pausa, `P` continua sendo lido e retoma o jogo.
- [ ] CA18. `T` deixa projétil, queimadura e recarga visivelmente mais lentos; o Console registra a escala. Apertar de novo volta ao normal.

**Feedback e estabilidade**
- [ ] CA19. O alvo muda de cor ao receber dano e enquanto queima, e volta ao normal quando a queimadura acaba. Não há Canvas, barra nem número flutuante na cena.
- [ ] CA20. `R` devolve cena, atributos, recarga e alvo ao início, sem erro no Console e sem objetos duplicados.
- [ ] CA21. Uma sessão de 5 minutos de uso livre não gera erro nem exceção no Console.

## 8. Perguntas em aberto

Levantamento do que existe no pacote em 2026-10-31 (`Packages/com.spellbook`):

- **Existe:** módulos Core, Time (inclui grupo pausável), Events (canais, `EventBus`, `EventListener`, reações prontas, Event Debugger), Pooling, Tags, GAS (ASC, atributos, efeitos com `Duration` e `Period`, custo, recarga, `SpawnProjectileAction`, `Projectile`) e `GAS.Bridges` com a `RaiseEventAction`.
- **Não existe:** módulo de UI (`SpellBook.UI`, B-015 a B-018), relay de eventos do ASC (B-004), dano com meta-atributo, `OnDepleted` e morte (B-010), Cues (B-013), Samples~.

**Respondidas pelo usuário (2026-10-31):**
- Q1 (morte): cortada. Só dano recebido.
- Q3 (HUD): esperar B-015 e B-016. S4 sai e fica condicionado a eles. S1 a S3 seguem.
- Q4 (UI base): uGUI, para quando o HUD entrar.
- Q5 (entrada): Input Manager legado.
- Q7 (2D ou 3D): 3D.

**Decisão do Arquiteto (na Spec, com base no código):**
- Q2. Payload do evento de dano recebido (R13): relay do ASC (B-004) não existe. Opções: payload fixo pela `RaiseEventAction`, ou script de apoio do Sample publicando o valor real ao ouvir `AttributeState.OnValueChanged`.
- Q6. Câmera lenta (R20): `Time.timeScale` ou fonte de tempo com escala própria. O `TimeSourceAsset` hoje tem escalado, não escalado e grupo pausável; se criar fonte nova, é mudança no módulo Time e pede PRD ou emenda própria.
- Q9 (nova). Como o alvo não morre (R9): vida alta, piso de vida ou outra forma, sem criar tratamento de morte.
- Q10 (nova). Como exibir o texto dos controles sem módulo de UI (R6).

**Fica para o gate:**
- Q8. Versão da entrega: qual pré-release do 3.0 (`3.0.0-pre.N`) e se entra no lote atual ou em um posterior.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|

## 10. Emenda de 2026-11-01: HUD do Sandbox (S4)

Status: **aprovada** no gate de 2026-11-01 (respostas Q11 a Q14 abaixo).

Motivo: o módulo de UI (B-015, B-016, B-017) e a barra de habilidades chegam no Lote E, e o plano de 2026-10-31 já previa o HUD do Sample nesse lote. A Spec `ui-slot-e-barra-de-habilidades.md` (SL12, SL13) planeja só dados, âncoras, prefab e cena, sem script novo. Esta emenda revê o que a seção 6 deixou condicionado. **Vale a partir do gate aprovado (2026-11-01).**

**Revisões (substituem o texto antigo)**

- **R4 (revisto).** O Sample não depende de pacote ou plugin de terceiros. Os cenários S1 a S3 não dependem do módulo de UI. O HUD (S4) depende dos módulos de UI do pacote (`UI`, `UI.UGUI` e a camada de UI do GAS) e do uGUI/TextMeshPro da Unity.
- **R6 (revisto).** A legenda dos controles continua na placa `TextMesh` do mundo (decisão do Arquiteto, Spec D3). O HUD não a substitui.
- **R23 (revisto).** Fora do HUD, o feedback continua sem Canvas. O Canvas existe **só** no prefab do HUD (R24), e o HUD é o único lugar com barra e número flutuante.
- **Seção 6, item S4:** deixa de ser fora de escopo. Os demais itens (morte, B-010, UI-3, UI-4, teste automático da cena) seguem fora.
- **R21 (revisto, Q12).** As mensagens do Console que o HUD já mostra são cortadas **enquanto o prefab do HUD está na cena**: tique da queimadura (o HUD mostra pelo número de dano e pela barra) e falha de conjuração, sem mana e em recarga (o HUD mostra pelo estado do slot). Ficam no Console, sempre, as que o HUD não mostra: acerto (valor registrado pelo ouvinte de log do S2), pausa, câmera lenta (com a escala) e reinício. Sem o prefab do HUD (R24), as mensagens de tique e de falha voltam, para S1 continuar conferível pelo Console. Como a cena sabe se o HUD está presente é **decisão do Arquiteto**.
- **R11 (revisto, Q12).** Sem mana suficiente ou em recarga, a habilidade não dispara. O motivo aparece no estado do slot do HUD (R27); sem o HUD, é registrado no Console.
- **CA3 (revisto).** Sem pacote de terceiros e **sem o prefab do HUD**, S1 a S3 funcionam sem módulo de UI. **CA19 (revisto):** sem o prefab do HUD, não há Canvas, barra nem número flutuante na cena.

**Requisitos novos**

R24. O HUD é um prefab único (`HUD.prefab`) em `Samples~/Sandbox/`, presente na cena. Tirar o prefab da cena não quebra S1 a S3, as mensagens do Console (R21) nem a cor do alvo (R22).
R25. O HUD é montado só com componentes do pacote e dados (assets). Não há script `.cs` novo no Sample por causa do HUD, e o asmdef do Sample não ganha referência a UI. Autoria pela ADR 0028.
R26. Barras de vida e mana do jogador e barra de vida do boneco. Acompanham dano, custo e regeneração; o boneco não morre (R9).
R27. A barra de habilidades mostra o slot da Bola de Fogo com a tecla "1", o ícone ou nome, a recarga e o estado "sem mana", todos vindos do ASC do jogador, não de código da cena.
R28. Números de dano sobem sobre o boneco e voltam ao pool (`PooledSpawnerAsset`, R17). Dano aparece como dano e cura como cura.
R29. O HUD obedece o grupo pausável (R18): `P` para a recarga e os números; `T` os deixa lentos.
R30. `R` (reinício) religa o HUD ao jogador e ao boneco novos, sem HUD duplicado nem referência perdida.
R31. Os textos do HUD saem de tabelas em `pt-BR`, não de texto fixo no prefab.
R32. O HUD só exibe e não consome entrada: a entrada segue só pelo Input Manager legado (R7), e o Sample não exige `EventSystem`.
R33. O HUD cumpre as regras de desempenho do pacote: sem alocação por quadro causada pelo Sample.

**Critérios de aceite novos** (à mão, no host 6000.3.13f1, com o Sample importado pela aba Samples)

- [ ] CA22. A cena com HUD abre sem "Missing" nem erro de compilação, e não acusa referência perdida ao apertar Play.
- [ ] CA23. As barras de vida e mana do jogador e a de vida do boneco acompanham custo, dano e queimadura, e batem com os valores do Inspector.
- [ ] CA24. O slot "1" mostra a tecla, a recarga correndo e "sem mana" quando falta mana; volta a disponível ao fim da recarga.
- [ ] CA25. Cada acerto e cada tique da queimadura geram um número de dano que sobe e some; 20 conjurações seguidas não criam mais números que o tamanho do pool.
- [ ] CA26. `P` congela recarga e números, e `T` os deixa mais lentos.
- [ ] CA27. `R` religa as barras e o slot ao jogador e ao boneco novos, sem HUD duplicado e sem erro no Console.
- [ ] CA28. Apagar o prefab do HUD da cena mantém CA6 a CA21 válidos (exceto a parte "sem Canvas" do CA19, que passa a valer sem o prefab). Com o HUD na cena, a mensagem de Console exigida por CA8 e CA9 é substituída pelo estado do slot (CA24); o Console segue como no CA30.
- [ ] CA30 (Q12). Com o HUD na cena, o Console não registra tique da queimadura nem falha de conjuração, mas registra acerto, pausa, câmera lenta (com a escala) e reinício. Sem o prefab do HUD, tique e falha voltam ao Console.
- [ ] CA29. Nenhum `.cs` do Sample referencia `UnityEngine.UI` ou `TMPro` (as guardas do SB-T1 seguem verdes) e a `SpellBook > Validar projeto` não acusa erro nos assets do HUD.

**Perguntas do gate (emenda), respondidas pelo usuário em 2026-11-01**

- Q11. Onde mora o HUD? **Resposta:** no mesmo Sample Sandbox, como prefab removível (R24). Proposta aceita.
- Q12. As mensagens do Console (R21) ficam com o HUD? **Resposta:** ALTERNATIVA. Cortar as que o HUD já mostra (tique da queimadura, falha de conjuração); ficam só as que o HUD não mostra. Aplicada em R21 e R11 (revistos), CA28 e CA30 (novo).
- Q13. A placa `TextMesh` de controles (R6) fica? **Resposta:** fica. Proposta aceita.
- Q14. TextMeshPro: **Resposta:** usar TextMeshPro, com o passo do TMP Essentials documentado na página do Sample. Proposta aceita.

**Em aberto para o Arquiteto (Spec):** como a cena detecta o HUD para religar as mensagens de tique e de falha quando o prefab é removido (R21 revisto).
