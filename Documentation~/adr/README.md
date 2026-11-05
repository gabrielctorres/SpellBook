# ADRs do SpellBook

Decisões que valem para o pacote inteiro. Regras de formato e de status (`Proposta`, `Aceita`, `Adiada`, `Rejeitada`, `Substituída por NNNN`) estão na [0001](0001-documentation-como-pasta-de-docs-internas-e-adrs.md):

- Uma ADR `Proposta` não vale como regra até o usuário aceitá-la no gate.
- Uma ADR `Aceita` não é reescrita: ganha emenda datada ou é substituída por outra.
- Números nunca são reusados.

Formato de cada ADR: Status, Data, Contexto, Decisão, Motivo, Consequências, O que os agentes NÃO podem fazer, Em aberto (quando houver), Relacionadas.

## Processo

| Nº | Título | Status | Data |
|---|---|---|---|
| [0001](0001-documentation-como-pasta-de-docs-internas-e-adrs.md) | `Documentation~/` como pasta de documentos internos e ADRs como registro de decisão | Aceita | 2026-10-29 |
| [0002](0002-fluxo-backlog-prd-spec-gate-unico-issues.md) | Fluxo backlog → PRD → Spec → gate único → issues no repositório do SpellBook (emenda de 2026-11-03, aceita: Spec "just-in-time", com detalhe do ticket na entrada da fila; emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: design doc por épico no lugar de PRD + Spec, Detalhe pela Fábrica, papéis Fábrica, Examinador e Desenhista, DevOps como passo do script, `CLAUDE.md` como fonte única e `decisoes.md`; emenda de 2026-11-03, aceita: Codex e Antigravity como papéis externos; emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: `decisoes.md` só no modo autônomo e `AGENTS.md` como fonte única; emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: gate do Examinador só para ticket com código, scripts inclusos e migração do CHANGELOG conferida, ticket só de texto conferido pelo Controlador e no PR) | Aceita | 2026-10-29 |
| [0003](0003-um-ticket-uma-issue-um-pr.md) | Um ticket = uma issue = um PR (histórico das emendas até 2026-11-03) | Substituída por [0042](0042-tamanho-do-ticket-contagem-e-excecoes.md) | 2026-10-29 |
| [0004](0004-testes-primeiro-red-green.md) | Testes primeiro (red → green) | Aceita | 2026-10-29 |
| [0005](0005-editor-unico-com-trava.md) | Editor único com trava (editor-lock) (emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: uma Unity e uma trava por checkout, até 2 Unity na máquina, dois tickets com Unity em paralelo em worktrees sem arquivo em comum) | Aceita | 2026-10-29 |
| [0006](0006-worktrees-por-ticket.md) | Worktrees por ticket (o worktree do paralelo com Unity vale pela emenda de 2026-11-03 da 0005) | Adiada (decisão I34) | 2026-10-29 |
| [0007](0007-documentacao-como-codigo-e-gate-de-doc.md) | Documentação como código e gate de doc no `/revisao` (emenda de 2026-11-01, aceita: molde de página, regras de escrita, `nav` explícito e constante por tipo; emenda de 2026-11-03, aceita: manual, referência e guardas só no passe de documentação, CHANGELOG por ticket só com quebras; emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: texto do passe de documentação conferido pelo Controlador, guardas `Documentacao` rodadas pelo Examinador sem achado sobre o texto, Detalhe do passe pela Fábrica) | Aceita | 2026-10-29 |
| [0008](0008-conventional-commits-e-versionamento-semantico.md) | Conventional Commits e versionamento semântico | Aceita | 2026-10-29 |
| [0027](0027-estrutura-do-repositorio-projeto-unity-na-raiz.md) | Estrutura do repositório: projeto Unity na raiz e pacote embutido em `Packages/com.spellbook/` | Aceita | 2026-10-30 |
| [0028](0028-autoria-de-samples-por-renomeacao-temporaria.md) | Autoria de `Samples~` por renomeação temporária para `Samples` | Aceita | 2026-10-31 |
| [0042](0042-tamanho-do-ticket-contagem-e-excecoes.md) | Tamanho do ticket, contagem e exceções: ~500 linhas e 10 arquivos, até +30% com o motivo no Detalhe (no modo autônomo, também no `decisoes.md`), acima disso divide (substitui a 0003; aprovada pelo usuário em 2026-11-03; emenda de 2026-11-03, aprovada pelo usuário em 2026-11-03: exceção registrada no Detalhe) | Aceita | 2026-11-03 |

## Arquitetura

| Nº | Título | Status | Data |
|---|---|---|---|
| [0009](0009-versao-3-0-com-quebras-livres.md) | Versão 3.0 com quebras livres | Aceita | 2026-10-29 |
| [0010](0010-unity-minima-6000-0.md) | Unity mínima 6000.0 | Aceita | 2026-10-29 |
| [0011](0011-odin-obrigatorio-so-atributos-no-runtime.md) | Odin obrigatório: só atributos no runtime, serialização nativa | Substituída por [0026](0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) | 2026-10-29 |
| [0012](0012-mapa-de-modulos-e-asmdefs.md) | Mapa de módulos e asmdefs sem ciclos | Aceita | 2026-10-29 |
| [0013](0013-serializereference-como-costura-entre-modulos.md) | `[SerializeReference]` como costura entre módulos | Aceita | 2026-10-29 |
| [0014](0014-tempo-injetavel-itimesource.md) | Tempo injetável (`ITimeSource`) | Aceita | 2026-10-29 |
| [0015](0015-spawnerasset-como-seam-de-spawn-e-pool.md) | `SpawnerAsset` como ponto único de spawn e pool | Aceita | 2026-10-29 |
| [0016](0016-sistema-de-eventos-unificado.md) | Sistema de eventos unificado | Aceita | 2026-10-29 |
| [0017](0017-ui-em-tres-camadas.md) | UI em três camadas (fonte → binder → view) | Aceita como direção | 2026-10-29 |
| [0018](0018-hierarquia-unica-de-acoes.md) | Hierarquia única de ações (evento → ações → alvos) | Aceita como direção | 2026-10-29 |
| [0019](0019-hierarquia-unica-de-condition.md) | Hierarquia única de `Condition` (And/Or/Not) | Aceita como direção | 2026-10-29 |
| [0020](0020-valores-por-nivel-scalablefloat.md) | Valores por nível (`ScalableFloat` em array ou curva) | Aceita como direção | 2026-10-29 |
| [0021](0021-magnitude-configuravel-e-pilhas.md) | Magnitude configurável (snapshot ou ao vivo) e pilhas | Aceita como direção | 2026-10-29 |
| [0022](0022-ordem-das-checagens-de-ativacao.md) | Ordem das checagens de ativação | Aceita como direção | 2026-10-29 |
| [0023](0023-texto-estruturado-e-itextprovider.md) | Texto estruturado + `ITextProvider` | Aceita como direção | 2026-10-29 |
| [0024](0024-pipeline-generico-de-dano.md) | Pipeline genérico de dano | Aceita como direção | 2026-10-29 |
| [0025](0025-documentacao-em-mkdocs-ou-zensical-no-github-pages.md) | Documentação em MkDocs Material ou Zensical, publicada no GitHub Pages (emenda de 2026-11-01, aceita: Action de documentação pela 0040; emenda de 2026-11-04, recusada pelo usuário em 2026-11-04: a proposta de manter o MkDocs caiu, e o usuário decidiu migrar para o GitBook; a 0044, Aceita, a substitui, e o status muda no GB11) | Aceita | 2026-10-29 |
| [0026](0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) | SpellBook sem Odin: editor próprio em UI Toolkit (substitui a 0011) | Aceita | 2026-10-29 |
| [0029](0029-ordem-unica-de-composicao-dos-modificadores.md) | Ordem única de composição dos modificadores | Aceita | 2026-10-31 |
| [0030](0030-effectspec-e-effectcontext.md) | `EffectSpec` e `EffectContext` por valor; respostas aos "Em aberto" da 0020 e da 0021 (emenda: `%nome` como modo do `ScalableFloat`) | Aceita | 2026-10-31 |
| [0031](0031-relay-do-gas-e-gas-bridges-editor.md) | Forma do relay do GAS e o asmdef `SpellBook.GAS.Bridges.Editor` | Aceita | 2026-10-31 |
| [0032](0032-condition-contexto-motivo-e-cargas.md) | `Condition`: contexto por módulo, motivo no ponto de uso e cargas na ativação (respostas à 0019 e à 0022) | Aceita | 2026-10-31 |
| [0033](0033-pipeline-de-dano-na-definicao-do-atributo.md) | Pipeline de dano: processadores na definição do atributo, meta-atributo e relatório (respostas à 0024) | Aceita | 2026-10-31 |
| [0034](0034-modelo-de-execucao-das-acoes.md) | Modelo de execução das ações: executor no `Tick`, relógio lógico e dois contextos (respostas à 0018) | Aceita | 2026-10-31 |
| [0035](0035-filtro-de-candidato-a-alvo-por-condition.md) | Filtro de candidato a alvo: uma `Condition<ConditionContext>` com sujeito, no seletor de área, no impacto do projétil e na aura | Aceita | 2026-10-31 |
| [0036](0036-thinker-com-asc-proprio-e-execucao-que-segue-o-dono.md) | Thinker com ASC próprio e execução que segue o dono (`ExecutionLink`) | Aceita | 2026-10-31 |
| [0037](0037-cues-roteador-por-ancora-e-integracao-por-acoes.md) | Cues: tempo no módulo, roteador por âncora, objetos rastreados pelo roteador e integração com o GAS só por ações | Aceita | 2026-10-31 |
| [0038](0038-texto-no-core-tabela-por-cultura-e-adaptador-opcional.md) | Texto no Core: `ITextProvider`, `LocalizedText`, `TextBuffer`, tabela por cultura e adaptador opcional do Unity Localization (respostas à 0023) | Aceita | 2026-10-31 |
| [0039](0039-ui-ligacao-pela-fonte-ancora-nao-generica-e-ugui-declarada.md) | UI: ligação criada pela fonte, fontes fechadas por modelo, âncora não genérica, listas por chave e uGUI declarada (respostas à 0017 e à 0012) | Aceita | 2026-10-31 |
| [0040](0040-catalogo-e-api-gerados-pela-unity-na-action-de-documentacao.md) | Catálogo e API gerados pela Unity na máquina e commitados: o catálogo lê o seletor de tipo, o DocFX lê as fontes que a Unity compila, e a Action monta o site sem a Unity (emenda de 2026-11-01, aceita: mapa da Unity e gatilhos dos exemplos, pela 0041; emenda de 2026-11-02, aceita: link do .NET pelo `href` do DocFX; emenda de 2026-11-03, aceita: geração só no passe de documentação) | Aceita | 2026-11-01 |
| [0041](0041-links-para-a-unity-e-exemplos-incluidos-do-codigo.md) | Links da API para a documentação mais recente da Unity por mapa gerado (tipos do .NET com o link que o DocFX grava, emenda de 2026-11-02, aceita), e exemplos do manual incluídos de testes | Aceita | 2026-11-01 |
| [0043](0043-menu-create-nome-padrao-e-icone-dos-scriptableobjects.md) | Menu Create, nome padrão e ícone dos ScriptableObjects do pacote: grupo por módulo com `Text` e `Spawn` por função, `fileName` `New <Nome>` sem prefixo e um ícone por família, com guarda (B-038 e B-039; gate parcial de 2026-11-04: Q1 e Q2 respondidas, estilo dos ícones em aberto) | Proposta | 2026-11-04 |
| [0044](0044-manual-no-gitbook-por-git-sync-sem-referencia-da-api.md) | Manual no GitBook por Git Sync, editado pelo MCP, sem referência da API gerada: espaço público ligado à `main` e espaço de rascunho ligado à branch do lote, onde a Fábrica escreve por change request; `SUMMARY.md` no lugar do `nav`, exemplos expandidos na máquina com guarda e guardas `Documentacao` no lugar do build estrito (B-042; aceita no gate de 2026-11-04, Q1 a Q6; substitui a 0025 e partes da 0002, 0007, 0040, 0041 e 0042, com as emendas no ticket GB11; fatos a confirmar no spike GB1) | Aceita | 2026-11-04 |
