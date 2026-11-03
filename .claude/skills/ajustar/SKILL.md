---
name: ajustar
description: Desenhista registra a validação de uso de um épico já implementado, na seção "Validação de uso" do design doc (num épico antigo, na seção 9 do PRD). Use quando o usuário trouxer feedback de uso.
argument-hint: <épico> <observação>
---

Registre o ajuste de uso: $ARGUMENTS

1. Encontre o design doc em `Documentation~/spec/`. Num épico antigo, sem design doc, use o PRD em `Documentation~/prd/`, seção 9. Se houver mais de um candidato, pergunte qual é.
2. Acrescente uma linha na tabela: data de hoje, observação (o que aconteceu), ajuste pedido (o que deve mudar) e status `Aberto`.
3. Não reescreva a parte fixa. Se o ajuste muda um CA, aponte isso como pergunta para o gate do usuário.
4. Devolva a linha adicionada e diga se ela vira ticket novo (com o Detalhe pela Fábrica na entrada da fila) ou se precisa de gate (CA mudado).
