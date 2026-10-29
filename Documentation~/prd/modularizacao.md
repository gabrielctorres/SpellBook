# PRD: Fase 0-M, modularização do SpellBook
Backlog: B-001 · Issues: #2–#41 (0M-NN = #NN+1) · Status: Aprovado (gate da revisão sem Odin, 2026-10-29) · Data: 2026-10-29

> **Revisão sem Odin (2026-10-29).** A versão anterior foi aprovada no gate de 2026-10-29. Ela volta a rascunho por causa das decisões I45 a I47 do usuário: o SpellBook não depende do Odin, o editor do pacote é próprio e feito em UI Toolkit, e não há compatibilidade com o Odin (ADR 0026, que substitui a 0011). Os números de R, CA e P existentes não mudam; os cancelados ficam riscados com o motivo.

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seções D (Fundação), H.2, H.3, H.4, H.7, H.8, H.9 e I.
A seção I do plano é a fonte de verdade. Onde as seções A a H citam o PlaygroundRumble, a citação é só histórico: este PRD não tem foco em nenhum jogo.

---

## 1. Problema (contexto)

Hoje, quem instala o SpellBook recebe um bloco único e difícil de usar por partes:

- **Um único assembly** (`SpellBook.Core`) junta GAS, eventos, pooling e matemática. Não dá para usar o Pooling ou os Eventos sem levar o GAS junto, e o barramento de eventos depende do GAS (`Systems/Events/GAS_Events.cs`).
- **Três sistemas de eventos** convivem (`Core/Events`, `Core/NewEventSystem`, `Systems/Events`). Nenhum tem ferramenta de depuração. O `EventManager` vaza ouvintes quando o domain reload está desligado (Enter Play Mode Options). Há erros de grafia de API (`GameEventListner`).
- **Não há testes** nem asmdefs de teste. O tempo do GAS não pode ser injetado: o ASC lê `Time.deltaTime` e os efeitos leem `Time.time`, então um teste não controla o relógio.
- **O pacote depende de um plugin pago de terceiros sem dizer isso.** Oito arquivos do GAS usam atributos do Odin Inspector, mas isso não está escrito em lugar nenhum: sem o Odin, o pacote só mostra erros de compilação. Os campos `[SerializeReference]` (ações, elementos de efeito) dependem do seletor do Odin para receber um tipo. O `package.json` declara Unity 2021.3, versão que o pacote não suporta de fato.
- **O spawn depende de um singleton** (`PoolManager.Instance`). Quando o pool não existe na cena, as ações de spawn lançam NullReferenceException.
- **Não há documentação pública.** `documentationUrl` aponta para `https://example.com/`, os componentes não têm `[HelpURL]`, os assets não são validados e o `GAS_Documentation.md` está dentro de `Runtime/`.
- **Não há CI.** Nada garante que o pacote compila e passa nos testes nas versões da Unity que ele declara suportar.

A 0-M organiza o pacote em módulos independentes e cria a fundação (tempo, eventos, spawn, testes, validação, doc e CI) sobre a qual as Fases 1 a 9 são construídas. É o primeiro épico do roadmap (decisão I20).

## 2. Objetivo

Ao fim da 0-M, um desenvolvedor ou designer consegue:

1. instalar o SpellBook numa Unity 6000.0 ou mais nova, sem nenhum pacote ou plugin de terceiros, e usar cada módulo de infraestrutura (Time, Events, Pooling, Tags) **sem** depender do GAS;
2. montar eventos globais só pelo Inspector (canal, emissor, ouvinte, reações prontas) e ver pelo Event Debugger o que foi disparado e quem ouviu;
3. escolher a fonte de tempo do GAS por asset (escalada, não escalada ou grupo pausável) e, em testes, avançá-lo manualmente;
4. escolher como o pacote instancia objetos (Instantiate ou pool) por asset, sem singleton obrigatório;
5. validar o projeto inteiro por um menu e por um teste EditMode no CI;
6. abrir a página de documentação de qualquer componente pelo ícone de ajuda do Inspector, num site público com conteúdo real.

A 0-M é parte da versão 3.0, que aceita quebras sem fachada de compatibilidade (decisão I1). Ela sai como pré-release `3.0.0-pre.N` (decisão I24).

## 3. Usuários e cenários de uso

| Usuário | Quem é | O que espera da 0-M |
|---|---|---|
| **Desenvolvedor de gameplay** | Programa sistemas do jogo e integra o pacote | Assemblies pequenos e independentes, `EventBus<T>` sem alocação, tempo injetável, factories para testar o próprio código |
| **Designer técnico** | Monta comportamento pelo Inspector, sem escrever código | Canais, emissores e ouvintes por asset e componente, reações prontas, fontes de tempo por asset, validação com erros claros, link para a doc |
| **Mantenedor do pacote** | Evolui o SpellBook nas próximas fases | Testes EditMode/PlayMode por módulo, validadores no CI em duas versões da Unity, regras de dependência que o compilador garante, site de doc que nasce junto com o código |

**Cenários** (cada um vira um cenário verificável num Sample ou no projeto host de testes):

- **C1, só Pooling:** o desenvolvedor importa o pacote num projeto sem GAS em uso, cria um `PooledSpawnerAsset` e o usa num script próprio. O projeto compila e nenhum tipo do GAS é carregado pelo módulo.
- **C2, evento sem código:** o designer cria `Create > SpellBook > Events > Void Channel` com o nome "PlayerDied", põe um `EventEmitter` num botão de UI e um `EventListener` num objeto da cena com a reação pronta SetActive, que ativa um painel. Em Play, o Event Debugger mostra o disparo e o ouvinte que reagiu.
- **C3, payload próprio:** o desenvolvedor cria um struct de payload e duas classes de uma linha. O canal aparece no menu Create e o seletor do ouvinte só oferece canais desse tipo.
- **C4, só código:** o desenvolvedor declara `struct X : IEvent` e usa `EventBus<X>`, sem asset.
- **C5, tempo controlado:** num teste, o desenvolvedor dá um `ManualTimeSource` ao ASC, aplica um efeito de 2 s, avança 1,9 s e depois 0,2 s, e verifica que o efeito expira só no segundo avanço.
- **C6, pausa de gameplay:** o designer cria um `TimeSourceAsset` do tipo grupo pausável chamado "Gameplay" e o atribui aos ASCs dos personagens. Quando o grupo é pausado, recargas e durações desses ASCs param; um ASC com fonte não escalada e o resto da cena continuam. Ao retomar, tudo volta do ponto onde parou.
- **C7, validação:** o designer roda `SpellBook > Validar projeto` e recebe a lista de problemas (por exemplo, um ouvinte sem canal), cada um clicável para o asset ou objeto.
- **C8, projeto limpo:** alguém instala o pacote num projeto Unity 6000.0 limpo, sem nenhum plugin de terceiros. O projeto compila sem erros vindos do SpellBook, e o seletor de tipo funciona: numa habilidade, o designer adiciona uma ação escolhendo o tipo numa lista com busca, e num `EventListener` adiciona uma reação pronta escolhida pela categoria.
- **C9, doc:** o designer clica no ícone de ajuda de um `EventListener` e abre, no site público, a página do módulo Eventos com conteúdo real.
- **C10, habilidade dispara evento:** o designer põe uma `RaiseEventAction` numa habilidade, ligada a um canal; ao ativar a habilidade, um `EventListener` na cena reage, sem código.

## 4. Requisitos funcionais (regras e comportamento por módulo)

### 4.1 Mapa de módulos e regras de dependência

R1. O runtime é dividido nos assemblies abaixo. Teams, Cues e a família UI não nascem nesta fase (ver seção 6).

| Assembly | Conteúdo na 0-M | Pode referenciar |
|---|---|---|
| `SpellBook.Core` | `Void`, `RuntimeSet<T>`, `SpawnerAsset` abstrato com implementação padrão por Instantiate/Destroy, constante da URL base da doc, classe estática de matemática (4.1, R5a), atributos de Inspector (`TypePicker`, `TypeCategory`, `ShowIf`) e a interface de validação `IValidatable` | Unity |
| `SpellBook.Time` | `ITimeSource`, `ManualTimeSource` e `TimeSourceAsset` com as variantes escalado, não escalado e grupo pausável (4.2) | Core |
| `SpellBook.Events` | Canais, `EventBus<T>`, `EventListener`, `EventEmitter`, reações prontas, registry | Core, Time (decisão I38) |
| `SpellBook.Pooling` | `PoolManager`, `ComponentPooler`, `IPoolable`, `PooledSpawnerAsset` | Core |
| `SpellBook.Tags` | `GameplayTag`, `GameplayTagDefinition` (namespace `SpellBook.Tags`) | Core |
| `SpellBook.GAS` | ASC, atributos, efeitos, habilidades e ações, incluindo as de spawn (projétil, VFX), que usam só o `SpawnerAsset` do Core. Só expõe eventos C# | Core, Tags, Time |
| `SpellBook.GAS.Bridges` | Só a `RaiseEventAction` nesta fase (R25a) | GAS, Events (e Core, Tags, Time) |
| `SpellBook.Core.Editor` | Seletor de tipo para `[SerializeReference]` (busca e categorias), drawer do `ShowIf`, Inspector base com as mensagens de validação no topo, validador do projeto e a janela dele | Core |
| `SpellBook.<Módulo>.Editor` (Time, Tags, Pooling, Events, GAS) | Inspectors, drawers, janelas e debuggers do módulo; cada componente e asset do módulo usa o Inspector base do pacote | o runtime do módulo, as dependências dele e `SpellBook.Core.Editor`; nunca o `.Editor` de outro módulo |
| `SpellBook.<Módulo>.Tests.EditMode` e `SpellBook.<Módulo>.Tests.PlayMode` | Testes por módulo (R30) | o módulo, Time e as factories |

Todo drawer, Inspector e janela do pacote é feito em UI Toolkit. Nenhum assembly do pacote referencia o Odin (decisões I45 a I48).

R2. A infraestrutura (Core, Time, Events, Pooling, Tags) **nunca** referencia o GAS. O GAS **nunca** referencia Events nem Pooling; a ligação com Events existe só em `GAS.Bridges` (decisão I22).
R3. Nenhum assembly de runtime referencia `UnityEditor`.
R4. O `package.json` declara `"unity": "6000.0"`, `"version"` no formato `3.0.0-pre.N` (decisão I24) e `documentationUrl` igual a `https://gabrielctorres.github.io/SpellBook/` (decisão I29).
R5. A pasta `Runtime/MathTools`, os eventos antigos e as pastas `Systems/` deixam de existir como estão hoje; cada arquivo vai para o módulo do R1 ou é apagado (4.3).
R5a. **Matemática** (decisão I31): o `MonoBehaviour` `Math` que hoje embrulha a classe estática `MathTools` é removido. Só a classe estática fica, no `SpellBook.Core`.

### 4.2 Módulo Time

O módulo entra completo na 0-M (decisão I21).

R6. `ITimeSource` expõe, no mínimo, o tempo decorrido desde o último quadro e o tempo acumulado da fonte.
R7. `ManualTimeSource` só avança quando o código chama o método de avanço. Serve para testes e para simulações.
R7a. `TimeSourceAsset` é um ScriptableObject que implementa `ITimeSource`, criado pelo menu `Create > SpellBook > Time > <Variante>`, nas variantes:
- *Escalado:* segue o tempo escalado da Unity (`Time.timeScale` afeta);
- *Não escalado:* segue o tempo real e ignora `Time.timeScale`;
- *Grupo pausável:* pode ser pausado e retomado por métodos públicos (chamáveis por código e por `UnityEvent`). Enquanto pausado, o tempo decorrido por quadro é 0 e o tempo acumulado não avança. Todos os consumidores que referenciam o mesmo asset param e voltam juntos. A base de tempo do grupo é decisão técnica da Spec (P19).
R8. O ASC aceita um `TimeSourceAsset` pelo Inspector e qualquer `ITimeSource` por código (por exemplo, um `ManualTimeSource` num teste). Sem fonte, ele usa o tempo escalado da Unity (o comportamento de hoje).
R9. O ASC ganha `Tick(float deltaTime)` público e uma opção `autoTick`. Com `autoTick` ligado, o próprio ASC avança no `Update` usando a fonte configurada; desligado, só avança quando alguém chama `Tick`.
R10. Efeitos ativos, recargas e qualquer outra contagem de tempo do GAS usam só o tempo recebido pelo ASC. Nenhuma classe do GAS lê `Time.time` ou `Time.deltaTime` diretamente.

### 4.3 Módulo Events

R11. **Três usos com uma regra escrita** (vai para o manual e para o DS):
- *Canal em asset:* fato global que o designer liga sem código.
- *`EventBus<T>`:* evento global só entre sistemas de código.
- *Eventos C# por instância:* estado de uma entidade (por exemplo, os eventos do ASC). Nunca passam por canal.

R12. **Canal.** `EventChannel` (sem payload) e `EventChannel<T>`. Cada canal tem:
- `Raise(T)` e um evento C# assinável;
- campo de descrição, mostrado ao designer nos ouvintes;
- botão `Raise` no Inspector, ativo só no Play;
- lista dos ouvintes vivos, último payload disparado e opção `logRaises` que escreve cada disparo no console.

R13. Canais concretos prontos, cada um no menu `Create > SpellBook > Events > <Tipo> Channel`: Void, Int, Float, Bool, String, Vector3 e GameObject. Não há canal de `Component` (decisão I36).
R14. **Payload próprio:** um struct mais duas classes de uma linha (`XEventChannel : EventChannel<X>` e `XEventResponse : EventResponse<X>`) bastam para o tipo aparecer no menu Create, no ouvinte e no debugger.
R15. **Segurança do disparo:**
- um ouvinte que se inscreve ou se desinscreve durante um disparo não quebra a iteração nem faz outro ouvinte ser pulado ou chamado duas vezes no mesmo disparo;
- no Editor e em development build (`DEVELOPMENT_BUILD`), uma exceção lançada por um ouvinte é registrada no console e não impede os outros de receberem o evento;
- em release build, não há captura por ouvinte: a exceção sobe normalmente (decisão I36).

R16. **`EventBus<T>`:** `T` é `struct` e implementa `IEvent`. É estático por tipo e não aloca memória gerenciada em `Raise` depois da primeira inscrição. Segue a mesma segurança do R15.
R17. **Registry limpo sem domain reload:** ao entrar em Play com o domain reload desligado, nenhum ouvinte de sessões anteriores continua inscrito em canal ou em `EventBus<T>`.
R18. **Ouvinte.** Um único componente `EventListener` com uma lista de respostas. Cada resposta tem:
- um canal, escolhido num seletor que só lista canais compatíveis com o tipo da resposta;
- a descrição do canal visível ao lado do seletor;
- uma lista de reações tipadas (`[SerializeReference]`), escolhidas no seletor de tipo do SpellBook (R44);
- um `UnityEvent<T>` opcional, recolhido por padrão e marcado como "escape".

O ouvinte se inscreve ao habilitar e se desinscreve ao desabilitar. As reações rodam na ordem da lista, antes do `UnityEvent`.
R18a. **Reações prontas** (decisão I28). O pacote entrega, além do ponto de extensão para reações próprias:
- *SetActive:* ativa ou desativa um GameObject configurado;
- *RaiseChannel* (decisão I40): tem duas formas, escolhidas no Inspector: (a) repassar o payload recebido para outro canal do mesmo tipo; (b) disparar um canal sem payload;
- *Log:* escreve uma mensagem configurada no console;
- *Toggle de componente:* liga ou desliga (`enabled`) um componente configurado;
- *Invoke com delay* (decisão I38): depois de um atraso configurado, roda uma lista de reações, na ordem. O atraso é contado por um `TimeSourceAsset` escolhido no Inspector; sem asset escolhido, usa o tempo escalado. Se o ouvinte for desabilitado durante a espera, a execução pendente é cancelada e não roda depois.

Cada reação pronta tem página no manual e validação de campo obrigatório vazio (R35).
R19. **Emissor.** `EventEmitter` dispara um canal com um payload configurado no Inspector. O disparo pode vir de um método público (para `UnityEvent` e eventos de animação) ou de um botão no Inspector durante o Play.
R20. **Event Debugger** (janela do Editor): lista os canais do projeto; em Play, mostra para cada canal os ouvintes vivos e um histórico de disparos com horário, payload e quem disparou (quando conhecido). Tem busca por nome e um botão para limpar o histórico.
R21. **Find references:** a partir de um canal (menu de contexto do asset ou botão no debugger), lista quem emite e quem ouve aquele canal entre prefabs, ScriptableObjects e cenas abertas. Cenas fechadas não são abertas nem varridas (decisão I36). Cada item é clicável e seleciona o objeto.
R22. **Remoção dos sistemas antigos** (sem `[Obsolete]`, decisão I1): `Core/Events/**`, `Core/NewEventSystem/**`, `Systems/Events/GameEvent.cs`, `Systems/Events/GameEventListener.cs`, `Systems/Events/EventManager.cs` (substituído pelo `EventBus<T>`) e `Systems/Events/GAS_Events.cs` são apagados. O ASC deixa de disparar evento no barramento global e mantém só os seus eventos C# por instância. O CHANGELOG traz a tabela "tipo antigo → tipo novo".
R22a. **Lacuna aceita** (decisão I23): entre a 0-M e o B-004, o pacote não tem canal global de "habilidade ativada" nem de "slot atualizado". Quem precisa desses fatos usa os eventos C# do ASC. O relay para canais vem no B-004. O CHANGELOG e a página de migração dizem isso.

### 4.4 Spawn (Core), Pooling e Bridges

R23. `SpawnerAsset` (Core) é um ScriptableObject abstrato com operações de criar uma instância de um prefab (posição, rotação e pai opcional) e de devolvê-la. A implementação padrão do pacote usa Instantiate e Destroy.
R24. `PooledSpawnerAsset` (Pooling) cria e devolve instâncias por pool. Se o pool daquele prefab ainda não existe, ele é criado sob demanda. Nenhum caminho lança NullReferenceException por falta de `PoolManager` na cena.
R25. As ações do GAS que hoje instanciam objetos (spawn de projétil e VFX) ficam no `SpellBook.GAS` e recebem um `SpawnerAsset` pelo Inspector (decisão I22). Sem spawner configurado, usam a implementação padrão por Instantiate. Nenhuma delas acessa `PoolManager.Instance` diretamente. O `PlayCueAction` também fica no `SpellBook.GAS` e passa a instanciar pelo `SpawnerAsset` do Core; fora isso, fica como está até o módulo Cues (B-013) (decisão I37).
R25a. **`RaiseEventAction`** (decisões I22 e I39), em `SpellBook.GAS.Bridges`: uma ação de habilidade que, ao executar, dispara um canal configurado no Inspector. Aceita canais sem payload (`EventChannel`) e `EventChannel<T>` com um payload fixo configurado no Inspector. Payload dinâmico (vindo da habilidade ou do alvo) e a `RaiseEventEffectAction` ficam no B-004.
R26. `PoolManager.Instance` deixa de ser exigido por qualquer tipo do pacote fora do próprio módulo Pooling.

### 4.5 Tags

R27. `GameplayTag` e `GameplayTagDefinition` passam para o assembly e o namespace `SpellBook.Tags`, sem mudança de comportamento. Mudanças de API das tags ficam no B-006.

### 4.6 Sem dependências de terceiros e editor próprio

R28. **Sem Odin** (decisões I45 e I47): o pacote não depende do Odin Inspector nem de nenhum outro pacote ou plugin de terceiros. Nenhum arquivo do pacote (código, asmdef, `package.json`, testes) cita `Sirenix` ou `ODIN_INSPECTOR`. A serialização é a nativa da Unity (`[SerializeField]`, `[SerializeReference]`). Remover os atributos do Odin não muda o formato dos dados: assets criados com a versão anterior continuam válidos.
R29. O README e a página de instalação do manual dizem que só a Unity 6000.0 ou mais nova é necessária, e não citam o Odin.
R44. **Seletor de tipo** (decisão I45): todo campo `[SerializeReference]` do pacote que o designer preenche pelo Inspector (ações de habilidade, elementos de efeito e de descrição, respostas e reações do `EventListener`) tem um seletor de tipo do SpellBook. O seletor:
- lista só os tipos que podem ser escolhidos para o campo: concretos, não genéricos, serializáveis, com construtor sem parâmetros, não derivados de `UnityEngine.Object` e compatíveis com o tipo declarado do campo;
- inclui tipos de qualquer assembly do projeto, inclusive os criados pelo usuário, sem registro manual;
- agrupa os tipos em categorias declaradas no próprio tipo, com um resumo de uma linha; tipo sem categoria aparece na raiz;
- tem busca por nome e por categoria, sem diferenciar maiúsculas e minúsculas;
- tem a opção "(Nenhum)", que deixa o campo vazio; trocar o tipo cria uma instância nova e pode ser desfeita (Undo);
- ao duplicar um elemento de lista, o novo elemento é uma cópia independente: editar um não muda o outro.

R45. **Tipo ausente** (decisão I45): quando o tipo de uma referência `[SerializeReference]` deixa de existir (apagado ou renomeado), o pacote nunca apaga os dados sozinho. O Inspector mostra um erro com o assembly e a classe que faltam e um botão para limpar a referência; o validador do projeto (R33) acusa erro; restaurar o tipo recupera os dados.
R46. **UI Toolkit** (decisões I46 e I48): drawers, Inspectors e janelas do pacote (seletor, Inspector base, Inspectors de canal e emissor, Event Debugger, janela de validação) são feitos em UI Toolkit, salvo as exceções aprovadas na ADR 0026. As mensagens de validação no Inspector não usam exceção IMGUI.
R47. **Atributos próprios só sob demanda** (decisão I45): o pacote só cria um atributo de Inspector próprio quando um atributo nativo da Unity não resolve e a mesma necessidade aparece em mais de um módulo (o critério está na ADR 0026). Na 0-M, além dos atributos do seletor (R44), há só um atributo que mostra um campo conforme o valor de um campo irmão `bool` ou `enum`, para manter escondidos os campos do GAS que só valem em um modo.

### 4.7 Testes

R30. Cada módulo de runtime da 0-M tem um assembly `SpellBook.<Módulo>.Tests.EditMode`; os que dependem de ciclo de vida de MonoBehaviour também têm `SpellBook.<Módulo>.Tests.PlayMode` (decisão I27).
R31. Existem factories de teste que criam, em código e sem assets em disco: ASC com atributos, efeitos, habilidades, tags, canais de evento, fontes de tempo e spawners. As factories ficam num assembly reutilizável pelos testes de todos os módulos.

### 4.8 Validação

R32. Os assets e componentes do pacote se validam implementando a interface de validação do SpellBook (`IValidatable`, no `SpellBook.Core`). As mensagens de erro e aviso aparecem em dois lugares: no topo do Inspector do próprio objeto, desenhadas pelo Inspector base do pacote, e na janela de `SpellBook > Validar projeto` (R33). Os dois lugares mostram as mesmas mensagens, vindas da mesma validação.
R33. O menu `SpellBook > Validar projeto` roda todos os validadores sobre os assets do projeto e as cenas abertas e mostra o resultado numa janela, com erros e avisos clicáveis.
R34. Um teste EditMode roda os mesmos validadores e falha se houver algum erro. O teste roda no CI (R43).
R35. Regras mínimas de validação da 0-M:
- resposta de `EventListener` sem canal: erro;
- reação pronta com campo obrigatório vazio (por exemplo, SetActive sem GameObject, RaiseChannel sem canal): erro;
- `UnityEvent` de escape com chamada sem alvo ou sem método: erro;
- `EventEmitter` sem canal: erro;
- `RaiseEventAction` sem canal: erro;
- `PooledSpawnerAsset` com configuração inválida (por exemplo, tamanho inicial negativo): erro;
- `MonoBehaviour` ou `ScriptableObject` público do pacote sem `[HelpURL]`: erro (decisão I15). Esta regra é verificada sobre os tipos dos assemblies do SpellBook, não sobre assets.

### 4.9 Documentação e ferramentas de suporte

R36. Toda classe pública do pacote que deriva de `MonoBehaviour` ou `ScriptableObject` tem `[HelpURL]` montado a partir de uma **única** constante de URL base no Core, com valor `https://gabrielctorres.github.io/SpellBook/` (decisão I29). Trocar o domínio do site exige mudar só essa constante (e o `documentationUrl`).
R37. Existe o manual em `Documentation~/manual/` (MkDocs Material ou Zensical, a ADR decide, decisão I14), seguindo a árvore de páginas da seção H.8 do plano. **Toda página publicada tem conteúdo real, curto se preciso; não há páginas "em breve" nem stubs** (decisão I26). Na 0-M, existem pelo menos: Início (O que é, Instalação), Conceitos (mapa de módulos), Eventos (incluindo as reações prontas), Tempo, Pooling, GAS (R39), Validador, Event Debugger e uma página (ou seção de página) para cada componente e asset da 0-M que tem `[HelpURL]`. As demais páginas da árvore nascem nas fases que entregam o que elas descrevem.
R38. Uma GitHub Action gera o site e publica no GitHub Pages, **público** (decisão I29), a cada push na `main` que altere `Documentation~/manual/`. Só `Documentation~/manual/` é publicado; `prd/`, `spec/`, `adr/`, `planos/`, `backlog.md` e `TIME.md` nunca vão para o site.
R39. `Runtime/GAS/GAS_Documentation.md` sai de `Runtime/`. O conteúdo vira página do manual (seção GAS) e o arquivo antigo, com o `.meta`, é apagado.
~~R40. **Odin ausente** (decisão I25): `defineConstraints: ["ODIN_INSPECTOR"]` nos asmdefs que usam o Odin e `SpellBook.Setup.Editor` com diálogo de Odin ausente.~~ **Cancelado** pela I47 (2026-10-29): o pacote não depende do Odin (R28), então não há o que desligar nem aviso a mostrar. Não existe `SpellBook.Setup.Editor`.

### 4.10 Projeto host de testes, repositório e CI

R41. **Host de testes** (decisão I41, que substitui a I32): o projeto Unity host fica em `HostProject~/`, não na raiz. A raiz contém só o pacote. O `HostProject~/Packages/manifest.json` referencia o pacote por `file:../..` (a Unity resolve o caminho a partir de `HostProject~/Packages/`; confirmação no 0M-06) e o lista em `testables`. O host não tem o Odin nem outro plugin de terceiros (decisão I45), e os artefatos gerados do host (`Library/`, `Temp/` e afins) estão no `.gitignore`. Nenhuma pasta de projeto Unity (`Assets/`, `Library/`, `ProjectSettings/`) existe na raiz do pacote.
R42. **Repositório público** (decisão I33, ajustada pela I45): o repositório é público e o CI roda sem baixar nada privado. Os únicos secrets do CI são os da licença da Unity, e nenhum arquivo de workflow traz credencial escrita.
R43. **CI** (decisão I30): usando GameCI, o CI compila o pacote e roda as suítes EditMode e PlayMode (incluindo o teste de validação do R34) na Unity 6000.0 e na última 6000.x.

## 5. Requisitos não funcionais

N1. **Compatibilidade:** Unity 6000.0 ou mais nova. O pacote compila sem avisos novos e passa nos testes na 6000.0 e na última 6000.x, verificados no CI (R43).
N2. **Alocação:** `EventBus<T>.Raise` e `EventChannel<T>.Raise` sem reações e sem `UnityEvent` não alocam memória gerenciada depois do aquecimento. `ASC.Tick` não aloca em regime (sem efeitos sendo aplicados ou removidos naquele quadro).
N3. **Determinismo:** com `ManualTimeSource`, duas execuções com os mesmos avanços produzem o mesmo estado do ASC.
N4. **Independência:** cada módulo de infraestrutura compila só com as dependências do R1 (Core; o Events, com Core e Time), sem o GAS no projeto.
N5. **Tempo de teste:** a suíte EditMode da 0-M roda em menos de 30 s numa máquina de desenvolvimento comum.
N6. **Versão:** a 0-M sai como `3.0.0-pre.N`. A `3.0.0` final só sai quando GAS e UI estiverem completos (decisão I24). O CHANGELOG tem a entrada da pré-release e o guia de migração curto da 0-M (decisão I1).

## 6. Fora do escopo

- **Teams, Cues e a família UI** (`SpellBook.UI`, `UI.UGUI`, `GAS.UI`): épicos próprios (B-011, B-013, B-015 a B-018).
- **Compatibilidade com o Odin** (decisão I47): sem adaptador, sem `#if ODIN_INSPECTOR`, sem cenário de teste com o Odin e sem instrução de instalação. Num projeto que tenha o Odin, o comportamento do Inspector do SpellBook não tem suporte.
- **Inspector rico do GAS** (perdas aceitas pela decisão I49): abas, grupos, edição inline (efeito de custo, efeito de recarga, fórmula), pré-visualização da fórmula, tabelas, miniatura do ícone, busca na lista de ações e `ShowIf` por expressão (por exemplo, `Period > 0`). Na 0-M, o GAS usa o Inspector padrão da Unity com o seletor de tipo (R44) e o atributo do R47; nenhum dado é perdido. O Inspector rico fica para as fases do GAS (B-002 em diante).
- Mudanças no `PlayCueAction` além de instanciar pelo `SpawnerAsset` (R25, decisão I37): ele fica como está até o módulo Cues (B-013).
- Payload dinâmico na `RaiseEventAction` (B-004, decisão I39).
- Correções do núcleo do GAS (B-002), exceto o que o R10 e o R25 exigem para a modularização funcionar.
- `AbilitySystemEventRelay`, canais com payload do GAS e `RaiseEventEffectAction` (B-004). A `RaiseEventAction` entra na 0-M (R25a, decisão I22).
- `OnAbilityGranted/Removed` e o GAS Debugger (B-003).
- `GameplayTagContainer`, `GameplayTagQuery` e `GetDefinition` público (B-006).
- `RuntimeAnchor<T>`, base `Condition<TContext>` e `TextRoleTheme` no Core: entram nas fases que os usam.
- Timer/Scheduler do módulo Time.
- Wizard "Create Event Type", wizard de templates, tabela de balanceamento, `Samples~` completos e referência de API em DocFX (B-019).
- `[MovedFrom]`, `[FormerlySerializedAs]` e fachadas `[Obsolete]` (decisão I1).
- Publicação da versão `3.0.0` final (decisão I24).
- Worktrees por ticket (decisão I34).
- Multiplayer e predição.
- Qualquer migração de jogo que use o pacote.

## 7. Critérios de aceite

Cada CA é um teste automatizado (T), um teste de compilação/estrutura (E) ou um cenário verificável num Sample ou no projeto host (S). CAs acrescentados depois da primeira versão recebem números novos e ficam no grupo do assunto.

**Módulos e dependências**
- CA1 (E). O pacote tem os assemblies `SpellBook.Core`, `.Time`, `.Events`, `.Pooling`, `.Tags`, `.GAS`, `.GAS.Bridges`, um `.Editor` por módulo que tenha código de Editor, um `SpellBook.<Módulo>.Tests.EditMode` por módulo de runtime e um `SpellBook.<Módulo>.Tests.PlayMode` por módulo com testes de ciclo de vida.
- CA2 (T). Um teste EditMode lê as referências de cada asmdef do pacote e falha se Core, Time, Events, Pooling ou Tags referenciarem `SpellBook.GAS*`, se `SpellBook.GAS` referenciar `SpellBook.Events` ou `SpellBook.Pooling`, ou se `SpellBook.GAS.Bridges` referenciar `SpellBook.Pooling`.
- CA3 (T). Um teste EditMode falha se algum assembly de runtime do SpellBook referenciar `UnityEditor`. (A parte do Odin desta verificação passou para o CA65.)
- CA4 (E). `package.json` tem `"unity": "6000.0"`, `"version"` no formato `3.0.0-pre.<n>` e `documentationUrl` igual a `https://gabrielctorres.github.io/SpellBook/`.
- CA5 (S). No projeto host com a versão da Spec (D5) e **sem** Odin nem outro plugin de terceiros, o pacote importa sem erros e sem avisos de compilação vindos do SpellBook (cenário C8, parte da compilação).
- CA6 (S). No projeto host, um script próprio que referencia só `SpellBook.Core` e `SpellBook.Pooling` usa um `PooledSpawnerAsset` e compila (cenário C1).
- CA43 (E). Não existe `Runtime/MathTools/Math.cs` com o `MonoBehaviour` `Math`; a classe estática de matemática está no `SpellBook.Core` e nenhum `MonoBehaviour` do pacote a embrulha.

**Time**
- CA7 (T). Com `ManualTimeSource` e `autoTick` desligado, um efeito de 2 s aplicado no ASC continua ativo após `Tick(1.9)` e é removido após mais `Tick(0.2)`.
- CA8 (T). Uma habilidade com recarga de 3 s não pode ser ativada após avançar 2,9 s pela fonte manual e pode após avançar 3,0 s.
- CA9 (T). Com uma fonte de tempo que não avança, nenhum efeito ou recarga muda de estado ao longo de N quadros de Play com `autoTick` ligado.
- CA10 (T). Um teste de reflexão (ou análise do código-fonte) confirma que nenhum tipo de `SpellBook.GAS` usa `UnityEngine.Time.time` nem `UnityEngine.Time.deltaTime`.
- CA11 (T). Duas execuções com a mesma sequência de `Tick` produzem os mesmos valores de atributo e a mesma lista de efeitos ativos.
- CA44 (E). O menu `Create > SpellBook > Time` tem as variantes escalado, não escalado e grupo pausável de `TimeSourceAsset`.
- CA45 (T, PlayMode). Com `Time.timeScale = 0` durante N quadros, um `TimeSourceAsset` escalado não acumula tempo e um não escalado acumula.
- CA46 (T, PlayMode). Dois ASCs ligados ao mesmo grupo pausável e um terceiro ligado a uma fonte não escalada, todos com a mesma recarga em andamento: com o grupo pausado por N quadros, a recarga dos dois primeiros não muda e a do terceiro diminui; ao retomar, os dois primeiros voltam a contar do valor em que pararam (cenário C6).

**Events**
- CA12 (E). O menu `Create > SpellBook > Events` tem exatamente os canais Void, Int, Float, Bool, String, Vector3 e GameObject, e nenhum canal de `Component`.
- CA13 (T). Um `EventListener` com resposta ligada a um `FloatEventChannel` recebe o valor exato passado em `Raise(3.5f)`, executa as reações na ordem da lista e depois o `UnityEvent`.
- CA14 (T). Um ouvinte desabilitado não recebe disparos; reabilitado, volta a receber.
- CA15 (T). Durante um disparo, um ouvinte que desinscreve outro ainda não chamado e inscreve um terceiro não lança exceção, e cada ouvinte é chamado no máximo uma vez naquele disparo.
- CA16 (T). No Editor, com três ouvintes, se o segundo lança exceção, o primeiro e o terceiro recebem o evento e o console registra a exceção.
- CA17 (T). Um struct de teste `X : IEvent` disparado por `EventBus<X>.Raise` chega a todos os inscritos, e o Performance Testing (ou `GC.GetAllocatedBytesForCurrentThread`) mede 0 byte alocado em 1.000 disparos após o aquecimento.
- CA18 (T). `EventChannel<T>.Raise` com ouvintes C# inscritos e sem `UnityEvent` aloca 0 byte em 1.000 disparos após o aquecimento.
- CA19 (T). Após simular o reset de `SubsystemRegistration`, nenhum canal nem `EventBus<T>` mantém ouvintes inscritos antes dele.
- CA20 (T). Um payload de teste criado com um struct e as duas classes de uma linha funciona de ponta a ponta: emissor → canal → ouvinte → reação.
- CA21 (T, Editor). O seletor de canal de uma resposta `EventResponse<float>` lista só assets `EventChannel<float>`.
- CA22 (T). `EventEmitter.Raise()` (chamado como faria um `UnityEvent` ou evento de animação) dispara o canal com o payload configurado no Inspector.
- CA23 (S). Cenário C2 num Sample ou no projeto host: botão com `EventEmitter` → canal "PlayerDied" → `EventListener` com a reação SetActive ativa um painel, sem código; o Event Debugger mostra o disparo com horário e o ouvinte que reagiu.
- CA24 (T, Editor). Para um canal referenciado por um prefab emissor e por um ScriptableObject ouvinte, o "Find references" devolve exatamente esses dois itens, cada um com o papel correto (emite / ouve). Um ouvinte do mesmo canal numa cena fechada não aparece no resultado.
- CA25 (E). Os caminhos `Runtime/Core/Events`, `Runtime/Core/NewEventSystem` e `Runtime/Systems/Events` não existem mais, e nenhum tipo do pacote se chama `GameEventListner`, `EventManager` ou `GameEvent`.
- CA26 (E). O CHANGELOG da pré-release tem a tabela de migração "tipo antigo → tipo novo" dos eventos e a nota da lacuna de eventos do GAS até o B-004 (R22a).
- CA47 (T). A reação SetActive, disparada pelo canal, ativa um GameObject inativo (e, configurada para desativar, desativa um ativo).
- CA48 (T). A reação RaiseChannel na forma "sem payload", disparada pelo canal A, dispara o canal Void B uma vez, e um ouvinte de B recebe o evento.
- CA58 (T). A reação RaiseChannel na forma "repassar", numa resposta de `FloatEventChannel` A, ao receber `Raise(3.5f)` dispara o `FloatEventChannel` B uma vez com 3.5f; o seletor dessa forma só oferece canais do mesmo tipo da resposta.
- CA49 (T). A reação Log, disparada pelo canal, escreve no console exatamente a mensagem configurada.
- CA50 (T). A reação Toggle de componente, disparada pelo canal, muda o `enabled` do componente configurado para o valor esperado.
- CA51 (T). A reação Invoke com delay de 1 s, ligada a um `TimeSourceAsset` avançado manualmente (ou a uma fonte de teste equivalente), não roda a lista de reações após 0,9 s e a roda uma única vez, na ordem, após mais 0,2 s.
- CA59 (T). Se o ouvinte é desabilitado durante a espera de um Invoke com delay, a lista de reações não roda, nem depois do atraso nem quando o ouvinte é reabilitado.
- CA60 (T, PlayMode). Um Invoke com delay sem `TimeSourceAsset` escolhido conta o atraso pelo tempo escalado: com `Time.timeScale = 0`, a lista não roda.

**Spawn, Pooling e Bridges**
- CA27 (T). A implementação padrão de `SpawnerAsset` cria uma instância na posição e rotação pedidas e a destrói ao devolver.
- CA28 (T). `PooledSpawnerAsset` numa cena sem `PoolManager` cria a instância sem exceção; ao devolver e pedir de novo, recebe a mesma instância (reaproveitada).
- CA29 (T). A ação de spawn de projétil, no `SpellBook.GAS`, sem spawner configurado e numa cena sem `PoolManager`, cria o projétil sem NullReferenceException.
- CA30 (E). Nenhum arquivo fora do módulo Pooling contém `PoolManager.Instance`.
- CA52 (T). Uma habilidade com `RaiseEventAction` ligada a um `VoidEventChannel`, ao ser ativada, dispara o canal exatamente uma vez, e um `EventListener` desse canal reage (cenário C10).
- CA61 (T). Uma `RaiseEventAction` ligada a um `IntEventChannel` com payload fixo 7 no Inspector, ao executar, dispara o canal com 7.
- CA62 (T). O `PlayCueAction` sem spawner configurado, numa cena sem `PoolManager`, instancia o VFX sem NullReferenceException.

**Tags**
- CA31 (E). `GameplayTag` e `GameplayTagDefinition` estão no assembly e no namespace `SpellBook.Tags`, e os testes de match hierárquico de tags (escritos nesta fase) passam.

**Testes e factories**
- CA32 (E). Existe ao menos um assembly `SpellBook.<Módulo>.Tests.EditMode` por módulo de runtime (Core, Time, Events, Pooling, Tags, GAS, GAS.Bridges) e a suíte inteira passa no Test Runner e em batchmode.
- CA33 (T). As factories criam ASC, efeito, habilidade, tag, canal, fonte de tempo e spawner sem nenhum asset em disco, e são usadas por testes de pelo menos dois módulos diferentes.

**Validação**
- CA34 (S). `SpellBook > Validar projeto` num projeto com um `EventListener` sem canal mostra um erro que, ao ser clicado, seleciona o objeto.
- CA35 (T). O teste EditMode de validação falha quando existe no projeto de teste um asset com cada um dos erros do R35, e passa quando os erros são corrigidos.
- CA36 (T). Um teste EditMode falha se algum `MonoBehaviour` ou `ScriptableObject` público dos assemblies do SpellBook não tiver `[HelpURL]`, ou se a URL não começar pela constante base do Core.
- CA64 (T). Um asset com uma referência `[SerializeReference]` a um tipo que não existe faz o validador do projeto acusar erro com o assembly e a classe que faltam, e depois da validação os dados da referência continuam no arquivo do asset (R45).
- CA66 (S). Num `GameObject` com um `EventListener` cuja resposta não tem canal, o erro aparece no topo do Inspector, desenhado pelo Inspector base do pacote, com o mesmo texto que a janela de `SpellBook > Validar projeto` mostra; ao ligar o canal, o erro some do Inspector (R32).

**Seletor de tipo e Inspector**
- CA63 (T). Para um tipo base de teste, o seletor lista exatamente os tipos válidos do R44, inclusive um tipo concreto declarado em outro assembly, e exclui os abstratos, genéricos, sem `[Serializable]`, sem construtor sem parâmetros e derivados de `UnityEngine.Object`; cada tipo aparece na categoria declarada nele (ou na raiz, sem categoria); a busca por parte do nome ou da categoria, em qualquer caixa, devolve só os tipos correspondentes; escolher um tipo cria a instância e "(Nenhum)" deixa o campo vazio. Todo campo `[SerializeReference]` do GAS editável pelo designer usa o seletor.

**Documentação e dependências**
- CA37 (E). Existe exatamente uma constante de URL base, com valor `https://gabrielctorres.github.io/SpellBook/`, e todo `[HelpURL]` do pacote a usa.
- CA38 (S). Clicar no ícone de ajuda de `EventListener` abre a página do módulo Eventos no site público, sem login (cenário C9).
- CA39 (S). Um push na `main` que altera `Documentation~/manual/` dispara a Action, e o site público tem as páginas do R37. O site não contém nenhuma página de `prd/`, `spec/`, `adr/`, `planos/`, `backlog.md` ou `TIME.md`.
- CA40 (E). `Runtime/GAS/GAS_Documentation.md` e seu `.meta` não existem, e o conteúdo está numa página do manual.
- ~~CA41 (S). No projeto host sem Odin, o diálogo do R40 aparece uma vez com o link de instalação.~~ **Cancelado** pela I47 (2026-10-29): não há aviso de Odin ausente (R40 cancelado). A compilação sem Odin é coberta pelo CA5.
- CA42 (E). O README e a página de instalação dizem que só a Unity 6000.0 ou mais nova é necessária e não citam o Odin.
- ~~CA53 (T). Todo asmdef, exceto `SpellBook.Setup.Editor`, tem `ODIN_INSPECTOR` em `defineConstraints`.~~ **Cancelado** pela I47 (2026-10-29): não existe constraint do Odin nem `SpellBook.Setup.Editor`. Substituído pelo CA65.
- CA65 (T). Um teste EditMode falha se algum asmdef do pacote citar `Sirenix` ou `ODIN_INSPECTOR` (em referências, DLLs pré-compiladas, constraints ou version defines), ou se algum arquivo de código, asmdef, UXML, USS ou o `package.json` do pacote contiver `Sirenix` ou `ODIN_INSPECTOR`.
- CA54 (E). Para cada `[HelpURL]` do pacote existe a página (ou âncora) correspondente em `Documentation~/manual/`, e nenhuma página do manual é só título ou texto de "em breve".

**Host, repositório e CI**
- CA55 (E). O `HostProject~/Packages/manifest.json` referencia o pacote por `file:../..` e o lista em `testables`; o `.gitignore` ignora os artefatos gerados do host e não tem entrada do Sirenix; a raiz do repositório não tem `Assets/`, `Library/` nem `ProjectSettings/`.
- CA56 (E). Nenhum arquivo do Odin está versionado no repositório; os workflows do CI não citam o Odin, não baixam nada de local privado e só usam os secrets da licença da Unity, sem credencial escrita no arquivo.
- CA57 (S). Um pull request dispara o CI, que compila e roda as suítes EditMode e PlayMode na 6000.0 e na última 6000.x, e o resultado de cada versão aparece separado.

## 8. Perguntas em aberto

### Respondidas (2026-10-29)

- ~~**P1. Fontes de tempo prontas.**~~ Respondida pela decisão I21: Time completo na 0-M. Ver R7a, R8, C6, CA44 a CA46.
- ~~**P2. O que vai para `GAS.Bridges` nesta fase.**~~ Respondida pela decisão I22: as ações de spawn ficam no GAS e usam o `SpawnerAsset`; o `PlayCueAction` fica como está até o Cues; o Bridges só ganha a `RaiseEventAction`. Ver R1, R25, R25a. Gerou a P15 e a P16.
- ~~**P3. Aviso sem Odin.**~~ Respondida pela decisão I25: `defineConstraints: ["ODIN_INSPECTOR"]` e asmdef de Setup com diálogo. Ver R40, CA41, CA53. **Sem objeto** depois da I47: R40, CA41 e CA53 cancelados.
- ~~**P4. Lacuna de eventos do GAS.**~~ Respondida pela decisão I23: lacuna aceita; eventos C# do ASC continuam; relay no B-004. Ver R22, R22a, CA26.
- ~~**P5. Exceção em ouvinte no build de release.**~~ Respondida pela decisão I36 (padrão do Controlador): `try/catch` só no Editor e em `DEVELOPMENT_BUILD`. Ver R15, CA16.
- ~~**P6. Reações prontas.**~~ Respondida pela decisão I28: SetActive, RaiseChannel, Log, Toggle de componente e Invoke com delay. Ver R18a, CA47 a CA51. Gerou a P17 e a P18.
- ~~**P7. Canais concretos.**~~ Respondida pela decisão I36 (padrão do Controlador): Void, Int, Float, Bool, String, Vector3 e GameObject, sem `Component`. Ver R13, CA12.
- ~~**P8. Find references em cenas fechadas.**~~ Respondida pela decisão I36 (padrão do Controlador): só assets e cenas abertas. Ver R21, CA24.
- ~~**P9. Versão do pacote ao fim da 0-M.**~~ Respondida pela decisão I24: `3.0.0-pre.N` por épico. Ver R4, N6, CA4.
- ~~**P10. `Math.cs`.**~~ Respondida pela decisão I31: só a classe estática, no Core. Ver R1, R5a, CA43.
- ~~**P11. Domínio e visibilidade do site.**~~ Respondida pela decisão I29: público, em `gabrielctorres.github.io/SpellBook`. Ver R4, R36, R38, CA4, CA37 a CA39.
- ~~**P12. Páginas exigidas nesta fase.**~~ Respondida pela decisão I26: conteúdo real desde a 0-M, sem stubs. Ver R37, CA54.
- ~~**P13. Asmdefs dos testes.**~~ Respondida pela decisão I27: `SpellBook.<Módulo>.Tests.EditMode` / `.PlayMode`. Ver R1, R30, CA1, CA32.
- ~~**P14. Matriz de versões no CI.**~~ Respondida pela decisão I30: 6000.0 e última 6000.x, com GameCI. Ver R43, N1, CA57.
- ~~**P15. `PlayCueAction` e a regra R2.**~~ Respondida pela decisão I37: fica no GAS e instancia pelo `SpawnerAsset` do Core; o GAS não referencia o Pooling. Ver R25, CA30, CA62.
- ~~**P16. Escopo da `RaiseEventAction`.**~~ Respondida pela decisão I39: canais sem payload e `EventChannel<T>` com payload fixo no Inspector; payload dinâmico no B-004. Ver R25a, CA52, CA61.
- ~~**P17. Reação Invoke com delay.**~~ Respondida pela decisão I38: o Events referencia Core e Time; atraso por `TimeSourceAsset` no Inspector (padrão: escalado); roda uma lista de reações; é cancelada se o ouvinte for desabilitado. Ver R1, R18a, N4, CA51, CA59, CA60.
- ~~**P18. Reação RaiseChannel.**~~ Respondida pela decisão I40: duas formas no Inspector, repassar o payload para canal do mesmo tipo ou disparar canal sem payload. Ver R18a, CA48, CA58.

- ~~**P21. Licença do Odin no CI.**~~ Respondida pela decisão I45: o pacote não depende do Odin, então o CI não precisa baixá-lo. Ver R42, CA56.
- ~~**P22. Mensagens de validação no Inspector.**~~ Respondida pela decisão I48: as mensagens aparecem no topo do Inspector, pelo Inspector base `SpellBookEditor` em UI Toolkit, sem exceção IMGUI, e também na janela do validador. Ver R1, R32, R46, CA66.
- ~~**P23. Perdas no Inspector do GAS.**~~ Respondida pela decisão I49: as perdas (abas, grupos, edição inline, pré-visualização da fórmula, tabelas, miniatura do ícone, busca de ações, `ShowIf` por expressão) são aceitas até os épicos do GAS, sem perda de dados. Ver seção 6.

### Com o Arquiteto (decisão técnica da Spec)

- **P19. Base do grupo pausável.** O `TimeSourceAsset` de grupo pausável segue o tempo escalado ou o não escalado da Unity, ou deixa o designer escolher? Ele tem só pausa, ou também uma escala própria? A Spec decide; se a resposta mudar o comportamento visível ao designer, ela volta para este PRD.
- **P20. Estado das fontes de tempo sem domain reload.** Ao entrar em Play com o domain reload desligado, o estado de um `TimeSourceAsset` (pausado, tempo acumulado) volta ao inicial, como os ouvintes no R17? A Spec decide.

### Abertas com o usuário

Nenhuma pergunta aberta com o usuário nesta revisão.

## 9. Diário de decisões e ajustes

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Origem | Decisão ou observação | Efeito neste PRD | Status |
|---|---|---|---|---|
| 2026-10-29 | usuário | O SpellBook é planejado sem foco em nenhum jogo. As menções ao PlaygroundRumble no plano são histórico (plano, seção I) | Requisitos genéricos; sem migração de jogo, sem fixtures de assets de jogo | Aplicado |
| 2026-10-29 | usuário | I1: versão 3.0 com quebras livres, sem `[MovedFrom]` nem `[Obsolete]`; guia de migração curto no CHANGELOG | R22, N6, CA26; fora de escopo | Aplicado |
| 2026-10-29 | usuário | I2: Unity mínima 6000.0 | R4, N1, CA4, CA5 | Aplicado |
| 2026-10-29 | usuário | I3: Odin obrigatório; no runtime só atributos e serialização nativa; proibidos `SerializedScriptableObject`, `SerializedMonoBehaviour` e `Sirenix.Serialization`; Editor livre | R28, R29, R32, CA3, CA42 | Substituído pela I45 |
| 2026-10-29 | usuário | I4: issues no repositório do SpellBook, uma por ticket, só depois do gate | Cabeçalho (issue criada depois do gate) | Aplicado |
| 2026-10-29 | usuário | I5: módulos da 3.0 são Events, Time, Pooling, Tags, Teams, Cues, GAS, GAS.Bridges, UI, UI.UGUI e GAS.UI | R1 cobre os da 0-M; Teams, Cues e UI na seção 6 | Aplicado |
| 2026-10-29 | usuário | I14: doc em MkDocs Material ou Zensical no GitHub Pages; Python só como ferramenta da doc | R37, R38, CA39 | Aplicado |
| 2026-10-29 | usuário | I15: o `/revisao` reprova ticket sem página; o validador cobra o `[HelpURL]` | R35, R36, CA36 | Aplicado |
| 2026-10-29 | usuário | I16: documentos internos em `Documentation~/`; manual público em `Documentation~/manual/` | R37, R38, R39, CA39 | Aplicado |
| 2026-10-29 | usuário | I19: PRD na visão de quem usa o pacote; CAs viram testes ou cenários de Sample; seção 9 é o diário | Formato deste PRD | Aplicado |
| 2026-10-29 | usuário | I20: um épico por vez, começando pela 0-M | Seção 1 | Aplicado |
| 2026-10-29 | Controlador | Teams, Cues e UI não entram na 0-M; são épicos próprios | Seção 6 | Aplicado |
| 2026-10-29 | usuário | I21 (P1): Time completo na 0-M: `ITimeSource`, `TimeSourceAsset` (escalado, não escalado, grupo pausável) e `ManualTimeSource` | Objetivo 3, R1, R7a, R8, R31, C6, CA33, CA44 a CA46; abriu P19 e P20 | Aplicado |
| 2026-10-29 | usuário | I22 (P2): ações de spawn ficam no GAS e usam só o `SpawnerAsset` do Core; `PlayCueAction` fica como está até o Cues; o Bridges só ganha a `RaiseEventAction` | R1, R2, R25, R25a, R35, C10, CA2, CA29, CA52; seção 6 (a `RaiseEventAction` saiu do B-004); abriu P15 e P16 | Aplicado |
| 2026-10-29 | usuário | I23 (P4): lacuna de eventos aceita; os eventos C# do ASC continuam; o relay vem no B-004 | R22, R22a, CA26 | Aplicado |
| 2026-10-29 | usuário | I24 (P9): versão `3.0.0-pre.N` por épico; a `3.0.0` final sai quando GAS e UI estiverem completos | Objetivo, R4, N6, CA4, CA26; seção 6 | Aplicado |
| 2026-10-29 | usuário | I25 (P3): `defineConstraints: ["ODIN_INSPECTOR"]` nos asmdefs que usam Odin, mais asmdef de Setup sem Odin com diálogo e link de instalação | R40, C8, CA41, CA53 | Substituído pela I45 |
| 2026-10-29 | usuário | I26 (P12): páginas da doc com conteúdo real desde a 0-M, curtas se preciso, sem stubs | R37, CA54 | Aplicado |
| 2026-10-29 | usuário | I27 (P13): assemblies de teste por módulo, `SpellBook.<Módulo>.Tests.EditMode` / `.PlayMode` | R1, R30, CA1, CA32 | Aplicado |
| 2026-10-29 | usuário | I28 (P6): reações prontas mínimas: SetActive, RaiseChannel, Log, Toggle de componente, Invoke com delay | R1, R18a, R35, C2, CA23, CA47 a CA51; abriu P17 e P18 | Aplicado |
| 2026-10-29 | usuário | I29 (P11): site público em `gabrielctorres.github.io/SpellBook` | R4, R36, R38, CA4, CA37 a CA39 | Aplicado |
| 2026-10-29 | usuário | I30 (P14): CI compila e testa na 6000.0 e na última 6000.x, com GameCI | R34, R43, N1, CA57 | Aplicado |
| 2026-10-29 | usuário | I31 (P10): `Math.cs` vira só a classe estática, no Core | R1, R5a, CA43 | Aplicado |
| 2026-10-29 | usuário | I32: host de testes na raiz atual; manifest com `file:` e `testables`; Odin importado localmente e `Assets/Plugins/Sirenix` no `.gitignore` | R41, CA55; "projeto host" nos CAs (S) é este projeto | Substituído pela I41 |
| 2026-10-29 | usuário | I33: repositório público; o CI baixa o Odin de um local privado via secret (falta confirmar a licença do Odin) | R42, CA56; abriu P21 | Substituído pela I45 (o repositório continua público; sai a parte do Odin no CI) |
| 2026-10-29 | usuário | I34: worktrees adiados (ADR 0006 Adiada); uma branch por ticket no mesmo checkout | Seção 6 (fora do escopo); nenhum requisito de produto | Aplicado |
| 2026-10-29 | usuário | I35: pasta `spec/`; o Examinador cria e remove a trava; gate parcial permitido; páginas do manual e fixtures não contam nas ~400 linhas, mas contam nos 10 arquivos | Processo, não produto. R38 já usa `spec/`. O gate parcial permite aprovar este PRD com as P15 a P21 abertas, se o usuário quiser. A regra de linhas vale para o Arquiteto ao fatiar os tickets (as páginas do R37 e do CA54 não pesam nas ~400 linhas) | Aplicado |
| 2026-10-29 | Controlador | I36 (P5, P7, P8), padrão que o usuário pode corrigir: `try/catch` por ouvinte só no Editor e em `DEVELOPMENT_BUILD`; canais Void, Int, Float, Bool, String, Vector3 e GameObject, sem `Component`; Find references varre assets e cenas abertas, não cenas fechadas | R13, R15, R21, CA12, CA16, CA24 | Aplicado (padrão; aguarda eventual correção do usuário) |
| 2026-10-29 | usuário | I37 (P15): o `PlayCueAction` fica no GAS e instancia pelo `SpawnerAsset` do Core; o GAS não referencia o Pooling | R25, seção 6, CA62; R2 e CA30 continuam valendo | Aplicado |
| 2026-10-29 | usuário | I38 (P17): o Events referencia Core e Time; o Invoke com delay usa um `TimeSourceAsset` do Inspector (padrão: escalado), roda uma lista de reações e é cancelado se o ouvinte for desabilitado | R1, R18a, N4, CA51, CA59, CA60 | Aplicado |
| 2026-10-29 | usuário | I39 (P16): a `RaiseEventAction` aceita canais sem payload e `EventChannel<T>` com payload fixo no Inspector; payload dinâmico no B-004 | R25a, seção 6, CA61 | Aplicado |
| 2026-10-29 | usuário | I40 (P18): a RaiseChannel tem duas formas no Inspector: repassar o payload recebido para canal do mesmo tipo, ou disparar canal sem payload | R18a, CA48, CA58 | Aplicado |
| 2026-10-29 | Controlador | P19 e P20 ficam com o Arquiteto, como decisão técnica da Spec | R7a, seção 8 | Aplicado |
| 2026-10-29 | usuário | I41 (substitui a I32): host de testes em `HostProject~/`, com `file:../` e `testables`, não na raiz. Motivo: a raiz é o pacote e o projeto ao mesmo tempo, e o `file:..` importaria `Assets/`, `Library/` e `ProjectSettings/` como parte do pacote | R41, CA55; "projeto host" nos CAs (S) é o `HostProject~/` | Aplicado |
| 2026-10-29 | usuário | I45 (substitui I3, I25 e I33 na parte do Odin): o SpellBook não depende do Odin. O pacote ganha editor próprio com seletor de tipo para `[SerializeReference]`, atributos próprios só sob demanda e validação por interface própria; a serialização continua nativa (ADR 0026) | Status volta a Rascunho; seção 1 (problema 4), Objetivo 1, C8, R1, R18, R28, R29, R32, R41, R42; R44, R45 e R47 novos; CA1, CA3, CA5, CA42, CA55, CA56; CA63, CA64 e CA65 novos; P21 respondida; abriu P22 e P23 | Aplicado (PRD em rascunho, aguarda gate) |
| 2026-10-29 | usuário | I46: drawers, Inspectors e janelas do pacote em UI Toolkit | R1, R46 novo | Aplicado (PRD em rascunho, aguarda gate) |
| 2026-10-29 | usuário | I47: nenhuma compatibilidade com o Odin: sem adaptador, sem `#if ODIN_INSPECTOR`, sem cenário de teste e sem instrução de instalação | R40, CA41 e CA53 cancelados; P3 sem objeto; seção 6 (compatibilidade com o Odin e Inspector rico do GAS fora do escopo) | Aplicado (PRD em rascunho, aguarda gate) |
| 2026-10-29 | usuário | I48 (P22): mensagens de validação no topo do Inspector, pelo Inspector base `SpellBookEditor` em UI Toolkit, sem exceção IMGUI, e também na janela do validador. Um `.Editor` de módulo pode referenciar o `SpellBook.Core.Editor` | R1 (linha do `SpellBook.Core.Editor` e regra dos `.Editor` de módulo), R32, R46, CA66 novo | Aplicado (PRD em rascunho, aguarda gate) |
| 2026-10-29 | usuário | I49 (P23): perdas no Inspector do GAS aceitas até os épicos do GAS | Seção 6 (Inspector rico do GAS) | Aplicado (PRD em rascunho, aguarda gate) |
| 2026-10-29 | usuário | I50: o canal fica dentro da `EventEmission` do emissor | Decisão técnica (ADR 0016, emenda); nenhum texto muda. R19, CA22 e CA24 continuam valendo: o emissor dispara o canal configurado no Inspector, e o Find references o mostra como "emite" | Aplicado |
| 2026-10-29 | Arquiteto | Caminho do pacote no manifest do host é `file:../..`, porque a Unity resolve o caminho a partir de `HostProject~/Packages/` (Spec, D1) | R41, CA55; confirmação no 0M-06 | Aplicado (confirmar no 0M-06) |
| 2026-10-29 | usuário | D5 da Spec: host local na 6000.3.13f1, não na 6000.0; o mínimo declarado e a matriz do CI não mudam | CA5 ("host com a versão da Spec, D5") | Aplicado |
