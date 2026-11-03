# Documentação interna do SpellBook

`Documentation~/` fica na raiz do repositório, fora do pacote (`Packages/com.spellbook/`), e o til se mantém (ADR 0027). A Unity ignora a pasta: ela não gera `.meta`, e o usuário que instala o pacote não a baixa.

Os agentes do time leem estes arquivos em vez de depender de memória ou de conversa. Nenhuma linha de código é escrita sem que antes exista registro de três coisas: o que se quer (parte fixa do design doc), como fazer (Detalhe do ticket) e as regras que valem (ADR e DS).

| Documento | Onde | Dono | Quando |
|---|---|---|---|
| Backlog | [backlog.md](backlog.md) | Controlador | Porta de entrada de toda ideia, bug ou pendência |
| Visão do pacote | `gdd/` | Desenhista | Regras e visão dos sistemas, com selos implementado/parcial/proposta |
| Design doc | `spec/<épico>.md` (modelo em `spec/_modelo-design-doc.md`) | Desenhista (parte fixa, `/design-doc`) e Fábrica (Detalhe) | Problema, CA, cenário no Sandbox e tickets; um Detalhe por ticket na entrada da fila. Nos épicos antigos, a Spec faz esse papel |
| PRD | `prd/<slug>.md` | histórico | PRD dos épicos antigos, no papel da parte fixa; a seção 9 recebe o `/ajustar` deles |
| ADR | `adr/NNNN-titulo.md` (índice em `adr/README.md`) | Desenhista | Decisões que valem para mais de um épico. Uma ADR aceita não é reescrita: ganha emenda datada ou é substituída |
| DS | `ds/` | Desenhista | Regras do Inspector, da UI e de nomenclatura |
| Manual público | `manual/` | Fábrica, no passe de documentação | Site MkDocs (GitHub Pages), no estilo TopDown Engine |
| Planos e estudos | `planos/` | Desenhista | Estudos de origem, como o [gas-estudo-e-roadmap.md](planos/gas-estudo-e-roadmap.md) |
| Decisões | [decisoes.md](decisoes.md) | quem decide | Log das decisões tomadas sem o usuário e das exceções de tamanho |
| Time | [TIME.md](TIME.md) | Controlador | Ponteiro para o `CLAUDE.md`, seção "Time de desenvolvimento" |

## Fluxo

1. A ideia nasce no backlog.
2. Num épico novo, o Desenhista escreve a parte fixa do design doc e as ADRs que valem para mais de um épico.
3. O usuário aprova a parte fixa e as ADRs novas num **único gate**.
4. O Controlador cria uma issue por ticket no repositório do SpellBook, com objetivo, CA e links para o design doc e as ADRs.
5. Na entrada da fila, a Fábrica escreve o Detalhe, implementa e testa; o Examinador revisa; o passo de commit do script commita.

## Projeto de desenvolvimento

A raiz do repositório é o projeto Unity de desenvolvimento (ADR 0027), e o pacote fica embutido em `Packages/com.spellbook/`. O `README.md` da raiz é a apresentação do SpellBook - Unity Tools, com a instalação e o link do manual; as informações para quem desenvolve ficam aqui.

Layout do repositório:

- `Packages/com.spellbook/`: o pacote (`com.spellbook`), com `Runtime/`, `Editor/`, `Tests/`, `package.json` e o `CHANGELOG.md`. É só isso que chega a quem instala.
- Raiz (`Assets/`, `ProjectSettings/`, `Packages/manifest.json`): o projeto Unity de desenvolvimento, que embute o pacote e roda os testes dele.
- `Documentation~/`: manual, PRD, Spec, ADRs e documentos internos do time. Fica fora do pacote.

- Versão: a de `ProjectSettings/ProjectVersion.txt` (6000.3.13f1, decisão D5 da Spec da modularização).
- `Packages/manifest.json` traz o pacote embutido e em `testables`, para os testes do pacote rodarem aqui.
- `Assets/` vem versionada com um `.gitkeep`, então já existe num clone novo. Não apague o `.gitkeep`: a Unity não abre o projeto sem `Assets/`.

Para abrir:

1. Confira se `.claude/editor.lock` existe. Se existir, não abra: outro papel está usando a Unity (ADR 0005).
2. Crie a trava com uma linha `papel; ticket; horário ISO-8601; caminho do host`.
3. Abra a raiz do repositório pelo Unity Hub (Add project from disk) ou rode a Unity em batchmode com `-projectPath "$PWD"`.
4. Ao terminar, feche a Unity e apague a trava que você criou, também se algo falhar.

Em caso de conflito com o `CLAUDE.md`, vale a ADR.
