# DS: Ferramentas do Editor (assistente de criação e gerenciador do GAS)
Status: Rascunho · Data: 2026-11-05 · Dono: Desenhista
Origem: design doc [menu-de-editor-do-gas](../spec/menu-de-editor-do-gas.md) (gate de 2026-11-05), [artefato do menu de Editor](https://claude.ai/artifact/94QTsdexDe1eAtYyPzdDuA?sk=wUikJrY-YHgCHtBS16TfKw) (abas 2 e 3), [ADR 0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (UI Toolkit) e ADR 0043 (menus e famílias). Os tokens de cor vêm do [DS do Inspector de ações](inspector-de-acoes.md).

Este documento define como o designer vê e usa as duas janelas. A API e os arquivos ficam no Detalhe de cada ticket. Onde o DS e o artefato divergirem, vale o DS, e o artefato é atualizado.

## 1. Escopo
Vale para `SpellBook/Criar habilidade ou efeito…` (o assistente) e `SpellBook/Gerenciador do GAS` (o gerenciador), e para os diálogos que elas abrem: Duplicar, Renomear, Excluir e Nova tag. Fica fora o Inspector da habilidade e do efeito, que é de outro épico; o gerenciador só abre o asset nele.

## 2. Princípios
1. **Ver antes de mudar.** Nada é criado, renomeado ou apagado sem uma lista do que vai mudar.
2. **Um lugar de edição.** Campos se editam no Inspector. As janelas criam, duplicam, renomeiam, apagam e mostram.
3. **Sem cor sozinha.** Todo estado dado por cor também tem ícone ou texto.
4. **Teclado completo.** Tudo que o mouse faz tem tecla, e o foco é visível.
5. **Só UI Toolkit.** Sem IMGUI e sem Odin (0026).

## 3. Anatomia comum
As duas janelas têm as mesmas quatro faixas, de cima para baixo:

| Faixa | Assistente | Gerenciador |
|---|---|---|
| Barra de cima | Título do passo e indicador "Passo N de 4" | Busca por texto, filtro de tipo, filtro de tag e contagem por tipo |
| Lista (esquerda) | Modelos | Habilidades, efeitos, atributos e tags |
| Painel de detalhe (direita) | Campos do passo e lista do que será criado | "Usa" e "Usado por" do item selecionado |
| Rodapé | Voltar, Avançar, Criar, Desfazer criação | Abrir no Inspector, Duplicar, Renomear, Excluir (e Nova tag filha, na árvore de tags) |

- A divisão entre lista e painel é arrastável, e a janela lembra a largura na sessão.
- O botão principal do rodapé fica à direita. Um botão desligado mostra o motivo no tooltip e numa linha de texto acima do rodapé.

## 4. Assistente
### 4.1 Passos
| Passo | O que mostra | O que libera "Avançar" ou "Criar" |
|---|---|---|
| 1. Modelo | Uma linha por modelo: ícone da família, nome e a frase do que cria. O modelo selecionado tem contorno e marca ✓ | Um modelo selecionado |
| 2. Nome e pasta | Campo "Nome" (com foco ao entrar) e campo "Pasta" com o botão "Escolher…". Aberto pelo Create, a pasta vem preenchida | Nome válido e pasta dentro de `Assets/` |
| 3. Revisão | A lista do que será criado (4.2) | Nenhuma linha com "colisão" |
| 4. Resultado | "Conjunto criado: N assets e M tags", com a lista clicável, e o botão "Desfazer criação" | — |

- O nome é validado a cada tecla. A mensagem fica embaixo do campo: "Digite um nome.", "Use só letras, números e _.", "Já existe um asset GA_Fireball nesta pasta."
- "Criar" fica desligado enquanto a revisão tiver uma colisão, e a linha de motivo diz qual asset colide.
- "Desfazer criação" fica no passo 4 até a janela fechar ou outro conjunto ser criado. Ele pede confirmação ("Apagar os N assets e M tags criados?") e volta ao passo 2 com o mesmo nome.

### 4.2 Lista do que será criado
Uma linha por asset e uma por tag:

```
[ícone família]  GA_IceBolt          Habilidade    nova       ★
[ícone família]  GE_IceBolt_Cost     Efeito        nova
[ícone tag]      Ability.IceBolt     Tag (pai: Ability)   já existe
[ícone prefab]   Projectile          Prefab do pacote     referência
```

- Marcadores, sempre com texto e ícone: **nova** (+), **já existe** (↺, a tag é reutilizada), **colisão** (✖, `--spellbook-error`), **referência** (🔗, prefab do pacote que não é copiado).
- O asset principal tem ★ e o rótulo "Principal" no tooltip; é ele que fica selecionado depois de criar.
- A linha "referência" tem o tooltip: "Este prefab é do pacote e é compartilhado. Para mudar o visual só desta habilidade, duplique o prefab no projeto e troque a referência no Inspector." (Q6.)

### 4.3 Frases dos modelos
| Modelo | Frase |
|---|---|
| Projétil | Habilidade que lança um projétil e aplica dano no impacto, com custo e recarga. |
| Área | Habilidade que aplica dano em quem está numa área, com custo e recarga. |
| Buff | Habilidade que aplica um efeito com duração no próprio personagem, com custo e recarga. |
| Aura | Habilidade que cria uma área que pulsa um efeito em quem estiver dentro. |
| Em branco | Habilidade sem ações, com custo, recarga e as duas tags. |
| Só efeito | Um efeito sozinho, sem habilidade. |

Um modelo do projeto mostra a descrição do próprio asset; sem descrição, mostra "Modelo do projeto".

## 5. Gerenciador
### 5.1 Linha da lista
```
[ícone família]  GA_Fireball     Habilidade   Ability.Fireball · +1     usado por 3
```
- Ícone da família pela tabela da 0043 (item 4), nome do asset, tipo, até duas tags com "+N" para o resto, e a contagem "usado por N". A contagem só conta o que a busca do CA9 alcança.
- A lista é virtualizada (`ListView`) e ordenada por nome, com os grupos Habilidades, Efeitos, Atributos e Tags quando o filtro de tipo é "Todos".

### 5.2 Filtros
- A busca filtra pelo nome, sem diferenciar maiúscula. O filtro de tipo é uma fileira de botões com a contagem: "Habilidades 12 · Efeitos 30 · Atributos 6 · Tags 41".
- O filtro de tag é um seletor de tag em árvore. Escolher `Ability` inclui as filhas, e o chip do filtro diz isso: "Ability e filhas".
- Os filtros ativos aparecem como chips com ✕ acima da lista, e "Limpar filtros" tira todos.

### 5.3 Árvore de tags
Com o tipo "Tags", a lista vira árvore pelo pai. A linha da tag mostra o nome inteiro, a contagem de usos e um botão "+" que abre o diálogo Nova tag filha, já com o prefixo do pai preenchido (`Ability.`). Mudar o pai não é oferecido.

### 5.4 Painel de uso e referências
- Dois grupos: **Usa** (o que o item referencia) e **Usado por** (quem o referencia). Cada linha mostra o ícone, o nome e a localização: "Asset", "Prefab" ou "Cena aberta: Sandbox".
- Clicar numa linha destaca o item no Project window ou na Hierarchy.
- Uma linha fixa no fim do grupo "Usado por" diz: "Cenas fechadas não entram nesta busca."

## 6. Diálogos
Todos são modais da janela, com o botão principal à direita, Esc para cancelar e Enter para confirmar.

- **Duplicar.** Campo do nome novo, já preenchido sem colisão (`GA_Fireball_2`). Para habilidade, lista o que é copiado (habilidade, custo, recarga, tags novas) e o que fica compartilhado (os outros efeitos), em dois grupos com esses títulos. Para efeito, a lista tem só o efeito.
- **Renomear.** Campo do nome novo e, para uma habilidade ou uma tag com filhas, a lista dos derivados com caixa de marcação, todos marcados: `GE_Fireball_Cost → GE_Firebolt_Cost`. O botão diz quantos mudam: "Renomear 5". Nome que já existe desliga o botão e aparece na linha de motivo.
- **Excluir.** Título "Excluir GE_Fireball_Cost?". Lista de quem usa o item (a do painel 5.4; numa tag, também as tags filhas). Sem usos, diz "Nada no projeto usa este item." Com usos, avisa em `--spellbook-warning`, com ícone ⚠: "Estas referências vão ficar vazias, e o validador vai acusá-las." O botão é "Excluir", em `--spellbook-error`, e não é o padrão do Enter quando há usos.
- **Nova tag filha.** Campo do nome com o prefixo do pai, e a linha de motivo para nome que já existe.

## 7. Estados
| Estado | Comportamento |
|---|---|
| Vazio | O gerenciador sem nenhum asset do GAS mostra "Nenhuma habilidade ou efeito no projeto." e o botão "Criar habilidade ou efeito…" |
| Carregando | Enquanto o índice monta, a lista mostra "Lendo o projeto…" e os filtros ficam desligados |
| Sem resultado | "Nada com estes filtros." e o botão "Limpar filtros" |
| Modelo quebrado | Não aparece no passo 1. Uma linha no fim da lista diz "N modelos com asset ausente: veja SpellBook/Validar projeto" |
| Nada selecionado | O painel de detalhe diz "Selecione um item para ver o que ele usa e quem o usa." |

## 8. Teclado e foco
- A busca (gerenciador) e o campo Nome (assistente, passo 2) recebem o foco ao abrir.
- ↑ e ↓ andam na lista; Enter abre no Inspector (gerenciador) ou avança (assistente); F2 renomeia; Ctrl+D duplica; Delete abre Excluir; ← e → abrem e fecham um nó da árvore de tags.
- Foco visível em toda linha, chip e botão, com contorno de 2 px. Botão só com ícone tem tooltip e rótulo.

## 9. Cores
- Usa os tokens `--spellbook-warning` e `--spellbook-error` do [DS do Inspector de ações](inspector-de-acoes.md), com valor nos dois temas, e sempre junto de ícone e texto.
- Os marcadores "nova", "já existe" e "referência" usam a cor de texto secundária do Editor, e se distinguem pelo ícone e pela palavra.

## 10. Texto
Rótulos, mensagens e frases ficam em português, nas tabelas e exemplos acima. Mensagem de erro diz o que fazer: "Já existe um asset GA_Fireball nesta pasta. Escolha outro nome."

## 11. Verificação (para o `/revisao`)
- **S.** O cenário do Sandbox do design doc, passos 1 a 9, mostra cada tela e diálogo desta DS.
- **E.** As janelas estão em UI Toolkit, sem `OnGUI` e sem Odin, e as cores vêm de tokens USS.

## 12. Histórico
| Data | Mudança |
|---|---|
| 2026-11-05 | Primeira versão, depois do gate do design doc, com Excluir, Desfazer criação e a referência ao prefab do pacote |
