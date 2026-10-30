Para **instalar** o pacote, siga o [README do pacote](Packages/com.spellbook/README.md) ou o [manual](https://gabrielctorres.github.io/SpellBook/).

# 📖 SpellBook - Unity Tools

Coleção de ferramentas reutilizáveis para projetos Unity.

## Layout do repositório

- `Packages/com.spellbook/`: o pacote (`com.spellbook`), com `Runtime/`, `Editor/`, `Tests/`, `package.json` e o `CHANGELOG.md`. É só isso que chega a quem instala.
- Raiz (`Assets/`, `ProjectSettings/`, `Packages/manifest.json`): o projeto Unity de desenvolvimento, que embute o pacote e roda os testes dele.
- `Documentation~/`: manual, PRD, Spec, ADRs e documentos internos do time. Fica fora do pacote.

## Abrir o projeto de desenvolvimento

Abra a raiz do repositório pelo Unity Hub (Add project from disk), na Unity 6000.3.13f1. Antes de abrir a Unity, confira se `.claude/editor.lock` existe: se existir, outro papel está usando o Editor (ADR 0005).

## Mais

- [Documentation~/README.md](Documentation~/README.md): como a documentação interna se organiza e o projeto de desenvolvimento.
- [Documentation~/TIME.md](Documentation~/TIME.md): papéis e fluxo do time.
