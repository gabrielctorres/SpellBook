# PRD: Wizard "Create Event Type" (B-019e)
Backlog: B-019e (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.3 (como criar um evento). ADRs [0013](../adr/) (o designer nunca escolhe um genérico) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md).
Depende de: nada além do módulo Events existente. **Não espera** o Lote B a D. Este PRD não reescreve o escopo de nenhum outro item.
Relacionado: [B-019f samples](samples-receitas.md) pode usar o wizard. Sem foco em nenhum jogo.

---

## 1. Problema

Um evento com payload próprio custa três arquivos de boilerplate: o struct, `XEventChannel : EventChannel<X>` e `XEventResponse : EventResponse<X>`. São classes de uma linha, mas é fácil errar o nome, o menu ou a ordem. O motivo delas é técnico (o designer nunca escolhe um genérico, ADR 0013).

## 2. Objetivo

Uma janela que, dado nome, namespace, pasta e payload, **gera o código** do evento, pronto para compilar, sem sobrescrever nada.

**Princípio (modular, sem remendos).** O texto sai de um **gerador puro** (função sem janela e sem disco); a janela só coleta entrada e escreve o resultado.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | Gerar `DanoRecebido` com payload sem boilerplate |

- **C1, gerar o evento:** nome `DanoRecebido`, namespace e campos (`float valor`, `GameObject origem`) geram o struct, `DanoRecebidoEventChannel` (com menu de criação) e `DanoRecebidoEventResponse`; compila e o canal aparece em `Create > SpellBook > Events`.
- **C2, só código:** a opção "só código" gera apenas o struct `IEvent` para o `EventBus<T>`.
- **C3, payload existente:** aceita um tipo que já existe (`Vector3`, struct do projeto) em vez de campos novos.
- **C4, recusa:** nome que não é identificador C# válido (ou é palavra reservada) ou tipo que já existe é recusado com mensagem; arquivo existente nunca é sobrescrito.
- **C5, gerador puro:** mesma entrada, mesmo texto.

## 4. Requisitos funcionais

R1. **Janela** (`Assets > Create > SpellBook > Events > Event Type (código)` ou `SpellBook/Novo tipo de evento`, UI Toolkit): nome, namespace, pasta e payload (campos novos de uma lista de tipos serializáveis comuns **ou** tipo existente).
R2. **O que gera:** struct do payload (se campos novos), `XEventChannel` (com `[CreateAssetMenu]` em `SpellBook/Events/` e `[HelpURL]`, como as do pacote) e `XEventResponse`. Modo "só código": `struct X : IEvent`. Cabeçalho diz que foi gerado e pode ser editado.
R3. **Nunca sobrescreve; tudo ou nada** (C4).
R4. **Gerador puro** devolve `string` (C5).
R5. **Compila no projeto de quem usa:** avisa se a pasta não alcança `SpellBook.Events`.
R6. `[HelpURL]` nos tipos do wizard; página em `Ferramentas`.

## 5. Requisitos não funcionais

N1. `.Editor` do módulo Events; sem referência cruzada de `.Editor`; nenhum runtime referencia `.Editor`.
N2. Testes sem janela e sem disco real (o gerador devolve `string`).

## 6. Fora do escopo

- Editar ou apagar eventos já gerados; migrar eventos existentes.
- Gerar listeners ou código de gameplay; payload com tipos genéricos.
- Wizard de outros tipos (ação, condição, reação).

## 7. Critérios de aceite

- CA1 (T). C1: o gerador devolve o texto de struct, canal e resposta; o código gerado compila no host e o canal aparece no menu.
- CA2 (T). C2, C3: "só código" e "payload existente" geram o conjunto correto.
- CA3 (T). C4, C5: nome inválido e tipo existente recusados; arquivo existente intacto; mesma entrada, mesmo texto.
- CA4 (E). Página, `[HelpURL]`, CHANGELOG; sem `OnGUI` com lógica nem `Sirenix`.

## 8. Perguntas em aberto

### Com o usuário
- **P1. Local do código gerado. RESPONDIDA (2026-10-31): E, aceito.** Código gerado na **pasta escolhida na janela**, **sem criar asmdef**, com aviso. Criar asmdef fica descartado.

### Com o Arquiteto
- **P1.** Em qual assembly o código cai quando a pasta não tem asmdef e como o wizard acha `SpellBook.Events`.
- **P2.** Como compilar o resultado dentro do teste.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco E do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
