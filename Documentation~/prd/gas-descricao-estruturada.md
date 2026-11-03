# PRD: Descrição estruturada e texto localizável
Backlog: B-008 · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), seção C (itens 2 e 14), seção E (Fase 4), seção H.5 e decisão I.13. ADRs [0023](../adr/0023-texto-estruturado-e-itextprovider.md) (texto estruturado e `ITextProvider`), [0017](../adr/0017-ui-em-tres-camadas.md) (a view formata), [0019](../adr/0019-hierarquia-unica-de-condition.md) e [0022](../adr/0022-ordem-das-checagens-de-ativacao.md) (motivo de falha por chave), [0009](../adr/0009-versao-3-0-com-quebras-livres.md) (quebra aceita na 3.0) e [0012](../adr/0012-mapa-de-modulos-e-asmdefs.md) (módulos).
Depende de: **B-005** (`AttributeDisplay.Format`, `TextRoleTheme`, metadados do efeito), **B-007** (`GetValue(nome, nível)`, `ScalableFloat`, `EffectContext`), **B-009** (cargas, `ActivationFailure` com chave de texto) e **B-003** (política de redução de recarga e efeitos ativos). Este PRD usa o que eles entregam e não reescreve o escopo deles (seção 6). O consumo na tela (tooltip, slot, lista de buffs) é de **B-017**, **B-016** e **B-018**.
Este PRD não tem foco em nenhum jogo. A menção ao `EffectDescriptionFormatter` do jogo no plano é histórico.

---

## 1. Problema (contexto)

Hoje o pacote descreve uma habilidade com uma string pronta:

- **Texto com cultura e cor fixas.** `DescriptionElement.GetText(ability, source)` devolve `string` com rich text. Não recebe cultura nem nível; a view não escolhe cor, ícone nem fonte. O jogo reimplementou um formatador com cultura pt-BR fixa.
- **Não descreve efeitos.** Não há "+20 % de armadura por 5 s": cada jogo escreve o seu, sem usar o formato do atributo, o papel (benéfico, prejudicial) nem o número de pilhas.
- **Não descreve custo, recarga, duração nem cargas.** O designer digita esses números à mão no texto, e eles envelhecem quando o balanceamento muda.
- **Sem valor por nível no texto.** "Causa {dano}" não lê a tabela de valores por nível (B-007); o texto do nível 1 serve para todos.
- **Sem localização.** Texto visível está escrito no asset ou no código; não há provedor por chave, e o motivo de falha de ativação (B-009) é só um enum.

## 2. Objetivo

Ao fim deste item, quem usa o pacote consegue, sem script no caso comum:

1. obter de um efeito, de uma habilidade e de um motivo de falha **linhas estruturadas** (rótulo por chave, valor, operação, papel, atributo), numa lista **fornecida por quem chama**, sem alocar;
2. descrever **modificadores, custo, recarga, duração e cargas** por **elementos de descrição** prontos, que leem os dados reais do asset e o **nível** pedido;
3. escrever um **template** ("Causa {dano}, recarga {recarga}") que resolve `%nome` pelo nível;
4. **injetar a cultura** e trocar o formato de um modificador por um `IModifierTextFormatter` próprio;
5. obter todo texto visível por **chave** de um `ITextProvider`, com implementação padrão sem dependências e um **adaptador opcional** do Unity Localization;
6. ver, no Inspector, a **prévia da descrição por nível**, sem entrar no Play.

## 3. Usuários e cenários de uso

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Montar a descrição com elementos e template, ver a prévia por nível, trocar o balanceamento sem reescrever texto |
| **Desenvolvedor de UI** (B-016 a B-018) | Linhas estruturadas por chamada, sem alocar, com papel e atributo, para montar cor e ícone na view |
| **Desenvolvedor de gameplay** | `IModifierTextFormatter` e `DescriptionElement` próprios; cultura do jogador injetada |
| **Quem localiza** | Chaves estáveis para cada texto, um provedor por chave, e o Unity Localization ligado sem o pacote o exigir |
| **Mantenedor do pacote** | Nenhum texto, cor ou cultura fixos dentro do GAS (ADR 0023) |

**Cenários** (cada um vira teste automatizado, salvo quando indicado):

- **C1, modificador:** efeito com `Armadura +20 %` (`Percent`, benéfico). `Describe` devolve uma linha com o atributo, o valor 20, a operação, o papel `Beneficial` e o texto do valor "+20 %" pelo formato do atributo (B-005).
- **C2, pilhas e nível:** o mesmo efeito com 3 pilhas e nível 2 devolve o valor já composto (pilhas multiplicam, B-007) e marca a linha como variável quando o valor depende de nível ou de `SetByCaller`.
- **C3, cultura:** com a cultura `pt-BR` o valor 1,5 s sai "1,5 s"; com `en-US`, "1.5 s". Nenhum código fixa a cultura.
- **C4, formatador próprio:** um `IModifierTextFormatter` do teste troca o texto do valor de uma linha; o resto não muda.
- **C5, custo, recarga, duração e cargas:** os elementos de custo, recarga, duração e cargas devolvem uma linha cada, lendo o asset (custo do efeito de custo, `Cooldown`, duração, `MaxCharges` e `RechargeTime`) no nível pedido. Habilidade sem cargas não gera linha de cargas.
- **C6, recarga real:** com o ASC de origem informado, a linha de recarga mostra o valor **após** a política de redução (B-003); sem ASC, mostra o valor **base** (P3).
- **C7, template por nível:** "Causa {dano}" com `dano = 50 75 100`: nível 1 devolve 50; nível 3, 100. Nome inexistente devolve a linha marcada como inválida, sem exceção.
- **C8, sem rich text:** nenhuma saída do GAS contém `<color>`, `<b>` nem outra marcação (busca nas saídas dos elementos).
- **C9, provedor:** a chave existe na tabela e devolve o texto; a chave ausente devolve o texto padrão do campo (P2); sem provedor configurado, vale o texto padrão.
- **C10, adaptador do Localization (E):** o asmdef do adaptador só compila com `com.unity.localization` instalado e o `package.json` do SpellBook **não** o declara.
- **C11, motivo de falha:** um `ActivationFailure` (B-009) vira uma linha com a **chave** do motivo e, quando o motivo é uma tag, a tag; a view pede o texto ao provedor.
- **C12, sem lixo:** 10 000 chamadas de `Describe` e dos elementos, com a lista reaproveitada, não alocam memória gerenciada depois do aquecimento.
- **C13, prévia (S):** no host, o Inspector da descrição da habilidade mostra o resultado por nível com um controle de nível, e muda ao editar o asset.
- **C14, contrato novo (T):** o `GetText(ability, source)` com `string` não existe mais; um elemento próprio escrito no contrato novo funciona e o validador acusa elemento nulo.

## 4. Requisitos funcionais

### 4.1 Texto por chave (`ITextProvider`)

R1. **`ITextProvider`.** Interface mínima: dado uma **chave** e um **texto padrão**, devolve o texto na **cultura injetada**. Todo texto visível ao usuário final que sai do runtime (rótulos de linha, motivo de falha, nomes exibidos) é obtido por chave a partir dele (ADR 0023, item 3). Não aloca no caminho de leitura de uma chave já resolvida (N1).

R2. **Implementação padrão, sem dependências.** O pacote entrega uma implementação que **não** exige nenhum pacote da Unity. Ela devolve o texto padrão do campo e, se houver uma **tabela de textos** em asset (chave → texto, por cultura), usa a tabela primeiro (P2). Há um provedor padrão do projeto, escolhido por campo serializado; sem ele, vale o texto padrão. Sem singleton e sem `Find` (ADR 0015).

R3. **Adaptador do Unity Localization (opcional).** Um asmdef próprio, compilado **só** quando `com.unity.localization` está presente (`versionDefines`), implementa `ITextProvider` sobre a tabela do pacote da Unity. O `package.json` **não** declara a dependência e nenhum outro asmdef a referencia (ADR 0023, item 4; ADR 0026, isolamento de DLLs).

R4. **Chaves.** Cada texto do pacote que o usuário final vê tem uma **chave estável** com convenção de nomes (a Spec define o formato). Nomes exibidos de habilidade, efeito e atributo e os motivos de falha de ativação passam por chave; o campo guarda o texto padrão e a chave opcional.

R5. **Plural e gênero.** O contrato do provedor **não** modela plural nem gênero; o adaptador do Localization os resolve pelos recursos dele (P1).

### 4.2 Linhas estruturadas

R6. **`DescriptionLine`/`ModifierLine`.** Uma **struct** por linha, com: **chave do rótulo**, texto do rótulo resolvido, **valor numérico**, **texto do valor** formatado (sem marcação), **operação**, **papel** (`Beneficial`, `Harmful`, `Neutral`, `Value`, do `TextRoleTheme`, B-005), **referência ao atributo** (quando há), e as marcas "variável" e "inválida". Não guarda cor nem rich text (ADR 0023, item 2): a cor é da view, que lê o papel. A forma exata (uma struct só ou duas) é do Arquiteto (P4).

R7. **`EffectDescriber`.** `Describe(GameplayEffect, in DescribeOptions, List<ModifierLine> resultado)` acrescenta à lista **fornecida por quem chama** uma linha por modificador visível (modificador de atributo oculto, `Hidden`, B-005, é pulado), **sem alocar** e sem limpar a lista. `DescribeOptions` traz: pilhas, **nível**, ASC de origem (opcional), **cultura** e o `IModifierTextFormatter`.

R8. **`IModifierTextFormatter`.** Interface para o projeto trocar o texto de uma linha (ordem, prefixo, unidade, "por segundo"). O padrão usa o formato do atributo (`AttributeDisplay.Format`, B-005) e o papel pelo valor e pelo `HigherIsBetter` do atributo. O formatador próprio é uma classe do projeto, escolhida por campo ou por opção.

R9. **`DescriptionContext`.** O contexto que cada elemento recebe: habilidade, efeito (quando há), **nível**, ASC de origem (opcional), **cultura**, `ITextProvider`, formatador e a lista de saída. É uma struct ou classe reaproveitada, sem alocação por chamada. **A cultura nunca é fixada pelo runtime** (ADR 0023, item 2).

### 4.3 Elementos de descrição

R10. **Contrato novo dos elementos.** `DescriptionElement` passa a produzir **linhas** pelo `DescriptionContext` (e não só `string`). Elementos próprios do projeto continuam por subclasse `[SerializeReference]` (ADR 0013). O `GetText(ability, source)` com `string` **deixa de existir** (quebra aceita na 3.0, ADR 0009; sem mecanismo paralelo de compatibilidade): os elementos do pacote e os fixtures migram para o contrato novo, e o CHANGELOG lista a quebra e a migração.

R11. **Elementos prontos** (cada um com `[HelpURL]`, `[TypeCategory]` e resumo de uma linha, ADR 0026):
- `TextElement` (texto fixo, por chave);
- `AttributeElement` (valor de um atributo do ASC de origem, com o formato do atributo);
- `TemplateElement` (texto com `{nome}` resolvido por `%nome` da tabela de valores, B-007, no nível do contexto);
- `EffectModifiersElement` (as linhas do `EffectDescriber` de um efeito);
- `CostElement`, `CooldownElement`, `DurationElement` e `ChargesElement` (custo, recarga, duração e cargas, lendo os dados reais, R12 a R14).

R12. **Custo e recarga.** `CostElement` lê o efeito de custo da habilidade (atributo e valor, por nível); `CooldownElement` lê o `Cooldown` e, com ASC de origem, a recarga **após** a `CooldownReductionPolicy` (B-003); sem ASC, o valor base (P3). Habilidade sem custo ou sem recarga não gera linha.

R13. **Duração.** `DurationElement` lê a duração do efeito (constante, `ScalableFloat` no nível, ou `SetByCaller` como "variável"); efeito infinito gera "permanente" por chave; efeito instantâneo não gera linha.

R14. **Cargas.** `ChargesElement` lê `MaxCharges` e `RechargeTime` (B-009) no nível, e gera "N cargas, uma a cada X s". Só existe com cargas habilitadas.

R15. **Template.** O `TemplateElement` aceita `{nome}`; o nome é resolvido na tabela do B-007 pelo nível do contexto. Nome inexistente gera a linha "inválida" e o validador acusa (R19). O template aceita também `{recarga}`, `{custo}` e `{duração}` como atalhos para os elementos acima (a Spec confirma a lista).

R16. **Motivo de falha.** Uma função de descrição converte o `ActivationFailure` (B-009) em linha com a **chave** do motivo, a tag (quando for tag) e o papel `Harmful`. O texto final vem do `ITextProvider`, escolhido pela view (B-016).

### 4.4 Editor, validação e documentação

R17. **Prévia por nível.** O Inspector da descrição da habilidade (UI Toolkit, ADR 0026, `SpellBookEditor`) mostra um **controle de nível** e o resultado em texto simples (sem cor), recalculado ao editar o asset ou o nível (decisão do plano, H.7: "prévia de descrição por nível"). A cultura da prévia é um campo do Inspector, padrão a do Editor (P4 do Arquiteto).

R18. **Validação** (`IValidatable`): elemento nulo; template com `{nome}` inexistente; elemento de custo em habilidade sem custo, de recarga sem `Cooldown` e de cargas sem cargas (aviso); chave de texto repetida com textos padrão diferentes (aviso); `TextElement` sem texto. Mensagens clicáveis.

R19. **Documentação.** Páginas **Descrição de habilidades e efeitos** (elementos, template, prévia, como criar um elemento próprio e um formatador próprio) e **Texto e localização** (provedor, tabela, adaptador do Unity Localization, convenção de chaves). Os tipos novos têm `[HelpURL]` (decisão I15). O CHANGELOG cita `ITextProvider`, `EffectDescriber`, `IModifierTextFormatter`, `DescriptionContext`, os elementos novos e a mudança de contrato do `DescriptionElement` (quebra).

## 5. Requisitos não funcionais

N1. **Alocação:** `Describe` e os elementos não alocam depois do aquecimento, com a lista reaproveitada; ler uma chave já resolvida não aloca (C12). O texto final de uma linha pode ser montado pela view; este PRD não promete string sem alocação.
N2. **Módulos (ADR 0012):** `EffectDescriber`, elementos e `DescriptionContext` em `SpellBook.GAS`; o `ITextProvider` fica no Core **ou** num módulo de texto novo (decisão do Arquiteto, ADR 0023, "em aberto", P1 do Arquiteto), sem referenciar o GAS. O adaptador do Localization num asmdef à parte. O GAS não referencia Events, Pooling, Teams, UI nem Localization.
N3. **Sem texto fixo:** nenhum arquivo do runtime tem rich text, cor, nem cultura fixa (`pt-BR`, `InvariantCulture` para texto exibido) nem texto visível escrito no código (ADR 0023, "não podem fazer").
N4. **Serialização:** campos novos com padrão que reproduz o comportamento de hoje; os assets de descrição antigos abrem sem perda de dados (os elementos existentes continuam com o mesmo formato serializado).
N5. **Determinismo:** testes com cultura injetada, `GasFactory` e provedor de teste (factory), sem assets fixos.
N6. **Asmdefs (ADR 0012, emenda 0026):** a Spec cria os asmdefs e o do adaptador; este PRD não.

## 6. Fora do escopo

- **Formato do atributo e `TextRoleTheme`:** **B-005**. Aqui só são lidos.
- **Tabela de valores por nível, `%nome`, `GetValue`:** **B-007**. Cargas e `ActivationFailure`: **B-009**. Política de recarga: **B-003**.
- **A view:** cor, rich text, ícone, fonte, tooltip e slot: **B-015 a B-018** (ADR 0017, a view formata).
- **Descrição de ações** (o que `ApplyEffect` ou `SpawnProjectile` fazem, em texto): fora; o designer escreve o texto e usa o template. Dano com tipo e escudo (B-010) entram como modificador genérico; elemento próprio de dano fica para depois.
- **Plural e gênero no contrato** (P1) e **tradução de conteúdo**: do adaptador e do projeto.
- **Descrição do próximo nível, comparação entre níveis:** fora (B-017 decide o uso).
- **Importar ou exportar tabelas** (CSV) e ferramenta de tradução: fora.
- **Rede e replicação.**

## 7. Critérios de aceite

Tipos: T = teste automatizado EditMode ou PlayMode no host; E = verificação estrutural; S = cenário verificável no host.

- CA1 (T). Cenário C1: `Describe` devolve a linha do modificador com atributo, valor, operação, papel e texto do valor.
- CA2 (T). Cenário C2: pilhas e nível entram no valor; a marca "variável" aparece quando o valor depende de nível ou `SetByCaller`.
- CA3 (T). Cenário C3: o mesmo valor sai com separador decimal diferente em duas culturas injetadas; nenhuma cultura é fixa.
- CA4 (T). Cenário C4: um `IModifierTextFormatter` de teste troca o texto sem alterar o resto.
- CA5 (T). Cenário C5: custo, recarga, duração e cargas geram uma linha cada, nos dados reais do asset e no nível pedido; elementos sem dado não geram linha.
- CA6 (T). Cenário C6: a recarga reflete a `CooldownReductionPolicy` com ASC e o valor base sem ASC.
- CA7 (T). Cenário C7: `{nome}` resolve por nível; nome inexistente vira linha inválida sem exceção.
- CA8 (T). Cenário C8: nenhuma saída do GAS contém marcação de rich text.
- CA9 (T). Cenário C9: chave presente, chave ausente e provedor ausente seguem o R2.
- CA10 (E). Cenário C10: o adaptador do Localization compila só com o pacote; o `package.json` não o declara; nenhum outro asmdef o referencia.
- CA11 (T). Cenário C11: o motivo de falha vira linha com a chave e o papel `Harmful`.
- CA12 (T). Cenário C12: 10 000 chamadas alocam 0 bytes gerenciados depois do aquecimento.
- CA13 (S). Cenário C13: a prévia por nível aparece no Inspector e acompanha a edição.
- CA14 (T). Cenário C14: um elemento próprio no contrato novo funciona; o validador cobre os casos do R18, com o objeto clicável.
- CA15 (E). O teste de assemblies da 0-M passa (N2); nenhum arquivo do runtime tem `<color`, cultura fixa para texto exibido nem texto de usuário no código (busca de código).
- CA16 (E). O CHANGELOG cita os itens do R19; as duas páginas existem; os tipos novos têm `[HelpURL]`.
- CA17 (T). A suíte do GAS existente passa sem edição, exceto os testes do `DescriptionElement` que a mudança de contrato do R10 troca (o Arquiteto os lista na Spec).

## 8. Perguntas em aberto

### Com o usuário

- **P1. Plural e gênero.** *Respondida (usuário, 2026-10-31): aceita. Fora do contrato do `ITextProvider`; o adaptador do Localization resolve (R5).*
- **P2. Provedor padrão.** *Respondida (usuário, 2026-10-31): aceita. Tabela de textos em asset, com o texto padrão do campo quando a chave não existe (R2).*
- **P3. Recarga na descrição.** *Respondida (usuário, 2026-10-31): aceita. Valor após a redução com ASC de origem; base sem ASC (R12).*
- **P4. Prévia no Inspector.** *Respondida (usuário, 2026-10-31): aceita. Controle de nível e prévia no Inspector, texto simples, sem janela própria (R17).*

### Com o Arquiteto (decisão técnica da Spec)

- **P1. Onde mora o `ITextProvider`** (ADR 0023, "em aberto"): `SpellBook.Core` ou módulo `SpellBook.Text` novo (exige emenda na ADR 0012); nome do asmdef do adaptador.
- **P2. Convenção das chaves** e a forma de guardar texto padrão e chave nos campos (`DisplayName` de habilidade, efeito e atributo hoje são `string`), sem quebrar assets.
- **P3. Migração do `DescriptionElement`** (R10): a forma do contrato novo (método que acrescenta linhas ao contexto) e a lista de assets, fixtures e testes que migram, sem manter o `GetText` antigo (o plano propunha um overload de compatibilidade; o PRD o descarta).
- **P4. Uma struct de linha ou duas** (`ModifierLine` e `DescriptionLine`), e o formato do campo "variável" e "inválida" sem alocar.
- **P5. `DescriptionContext`** como struct passada por `in` ou classe reaproveitada, e a relação com o `EffectContext` (B-007) e o contexto único do B-012.
- **P6. Cultura da prévia no Editor** e como o Editor a injeta sem tocar o runtime.
- **P7. Ordem entre tickets:** texto e provedor primeiro; `EffectDescriber` e elementos depois (dependem de B-005 e B-007); `CostElement`, `CooldownElement` e `ChargesElement` por último (dependem de B-003 e B-009); prévia no Inspector no fim.

## 9. Ajustes de playtest

Preenchido via `/ajustar`. "Playtest" aqui é validação de uso num projeto host ou Sample.

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | PRD escrito pelo Criativo a partir do B-008, do plano (itens 2 e 14, Fase 4, H.5) e das ADRs 0023, 0017, 0019, 0022 e 0026; o Controlador pediu o PRD inteiro e a Spec divide em tickets | Todo o documento | Rascunho |
| 2026-10-31 | Dependências registradas sem puxar escopo: B-005, B-007, B-009, B-003; o consumo em tela fica em B-016 a B-018 | Seção 6 | Aplicado |
| 2026-10-31 | usuário (via Controlador) P1 a P4: propostas aceitas (plural fora do contrato; tabela de textos em asset; recarga com redução do ASC; prévia no Inspector). Critério de design: sistema modular, sem remendos | Seção 8 (sem mudança de texto nos requisitos) | Aplicado |
| 2026-10-31 | usuário (via Controlador) B-018 P5: o `DisplayName` e o `Icon` do `GameplayEffect` entram no B-005 (AR7); o nome do efeito passa por chave (R4) a partir desse campo | R4 (sem mudança de texto) | Aplicado |
