# 0009 — Versão 3.0 com quebras livres

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O pacote está em `2.0.0`, com um único assembly (`Runtime/SpellBook.Core.asmdef`) que junta GAS, eventos e pooling. O plano de evolução exige mudanças que quebram código e assets: divisão em asmdefs (0012), troca de namespaces (`SpellBook.Tags`), remoção dos três sistemas de eventos atuais (0016), Unity mínima 6000.0 (0010), hierarquia única de ações (0018) e de condições (0019), saída estruturada de texto (0023).

Manter compatibilidade exigiria `[MovedFrom]` em cada tipo usado por `[SerializeReference]`, fachadas `[Obsolete]` e testes com fixtures de assets antigos. A decisão I.1 do usuário é não pagar esse custo.

## Decisão

1. A próxima versão MAJOR é a **3.0.0**, e ela aceita quebras sem camada de compatibilidade:
   - sem `[MovedFrom]`;
   - sem fachadas ou membros `[Obsolete]` mantidos para migração;
   - sem pontes para os sistemas removidos.
2. Toda quebra é registrada na seção "Quebras de compatibilidade" da Spec do ticket e chega ao CHANGELOG com um passo de migração curto (0008).
3. O manual ganha uma página "Migração 2.x → 3.0" com a tabela de tipos antigos → novos (nome, namespace, assembly), para quem precisar corrigir assets à mão.
4. A liberdade vale só até o `3.0.0` ser publicado. Depois disso, quebras voltam a exigir uma nova MAJOR e, para tipos serializados, a migração passa a ser obrigatória.

## Motivo

- Corrigir a arquitetura uma vez, sem carregar dívida de compatibilidade para a 3.x.
- O pacote não é planejado para nenhum jogo específico, e o custo de migração cai em quem atualiza, guiado pelo CHANGELOG.

## Consequências

- Assets criados na 2.x com `[SerializeReference]` (ações, elementos de descrição) perdem a referência de tipo ao atualizar, porque o assembly `SpellBook.Core` deixa de conter esses tipos. A Unity marca essas entradas como tipo ausente; o usuário precisa refazê-las ou corrigir o YAML com a tabela da página de migração.
- O `.meta` do `SpellBook.Core.asmdef` é mantido mesmo assim, porque o `Editor/SpellBook.Editor.asmdef` o referencia por GUID (`7834e8f3d0902ad45bfb6fab58f7b919`).
- Testes de migração com fixtures antigas não são necessários.

## O que os agentes NÃO podem fazer

- Adicionar `[MovedFrom]`, fachadas `[Obsolete]` ou adaptadores de compatibilidade "por segurança" durante o ciclo da 3.0.
- Quebrar sem registrar a quebra e a migração na Spec.
- Depois do `3.0.0` publicado, quebrar API ou formato serializado fora de uma nova MAJOR.

## Decisões incorporadas (2026-10-29)

- **`[FormerlySerializedAs]` também é proibido** durante o ciclo da 3.0 (o PRD da 0-M o lista fora do escopo junto com `[MovedFrom]`). O que existe em `Runtime/GAS/Abilities/GameplayAbility.cs:42` é removido pelo ticket que já edita esse arquivo (0M-15), e a migração de `_cooldownReductionAttribute` entra na página "Migração 2.x → 3.0".
- **Versões publicadas no ciclo** são `3.0.0-pre.N` (0008). A liberdade de quebra vale entre pré-versões; o item 4 passa a contar a partir da `3.0.0` final.
- **O `.meta` do `SpellBook.Core.asmdef` é mantido** mesmo quando o arquivo muda de pasta (`Runtime/` → `Runtime/Core/`, ticket 0M-40): mover o arquivo junto com o `.meta` preserva o GUID. O `SpellBook.Editor.asmdef` é renomeado para `SpellBook.Core.Editor.asmdef` mantendo o `.meta` dele.
- **Assets da 2.x de eventos não são preservados:** o `.meta` de `VoidEvent.cs` é apagado junto com o arquivo (0016).

- **2.x congelada** (decisão I43, 0008): sem correções em paralelo e sem `release/2.x`. Quem precisa de correção migra para a 3.0 pelo guia de migração.

## Em aberto

Nada.

## Relacionadas

- 0008 (SemVer), 0010 (Unity mínima), 0012 (módulos), 0016 (eventos), 0018 (ações), 0019 (condições).
