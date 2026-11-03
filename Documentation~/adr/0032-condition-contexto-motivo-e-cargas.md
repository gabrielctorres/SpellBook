# 0032 — `Condition`: contexto por módulo, motivo no ponto de uso e cargas na ativação; respostas aos "Em aberto" da 0019 e da 0022

Status: Aceita (gate 2026-10-31)
Data: 2026-10-31

## Contexto

O B-009 (PRD `gas-ativacao-avancada.md`, Spec `gas-ativacao-avancada.md`) entrega a base `Condition` (0019) e a ordem das checagens com cargas (0022). As duas ADRs deixaram itens em aberto que o B-009 precisa fechar, e o B-012 (ações condicionais, gatilhos) e o B-011 (condições de time no Bridges) vão usar a mesma base. Restrições do código:

- O `SpellBook.Tags` referencia o `SpellBook.Core` (`SpellBook.Tags.asmdef`); o Core não pode referenciar Tags sem ciclo (0012).
- O seletor de tipo (`TypePickerCatalog.cs:56` e `:133`) lista os tipos derivados do tipo do campo por `TypeCache.GetTypesDerivedFrom` e **exclui tipos genéricos**. Um campo `[SerializeReference]` de tipo base genérico fechado funciona (o Sample já serializa `EventEmission<Void>`), mas cada tipo concreto escolhido no seletor precisa ser não genérico.
- Condições precisam avaliar sem alocar (0019, item 3) e sem efeito colateral (0022).

## Decisão

1. **Base genérica no Core, contexto por módulo.** `SpellBook.Core.Conditions.Condition<TContext> where TContext : struct`, com `public abstract bool Evaluate(in TContext context)`. O Core também traz a lógica pronta de composição como bases genéricas abstratas: `AndConditionBase<TContext>` (lista vazia é verdadeira), `OrConditionBase<TContext>` (lista vazia é falsa) e `NotConditionBase<TContext>`. Cada módulo que usa condições define **um** contexto (`struct`) e fecha as três composições com tipos concretos não genéricos (no GAS: `AndCondition`, `OrCondition`, `NotCondition` sobre `ConditionContext`). É a opção (a) da 0019, com a ressalva do seletor.
2. **Um contexto só no GAS.** `SpellBook.GAS.Conditions.ConditionContext` (`readonly struct`) serve à ativação (B-009), à execução de ações e aos gatilhos (B-012) e aos filtros de alvo (B-011, no Bridges). Campos no B-009: `Owner` (ASC que avalia), `Ability`, `Level`, `Spec` (pode ser nulo), `Target` (`AbilityTarget`) e `Targets` (`IReadOnlyList<AbilitySystemComponent>`, nunca nulo). O B-012 acrescenta, de forma aditiva, a execução em curso. Não há contexto de ativação separado do contexto de ação.
3. **O filtro do seletor é o tipo do campo.** Um campo `[SerializeReference] Condition<ConditionContext>` só oferece condições do GAS e as composições fechadas sobre `ConditionContext`; nenhum atributo ou mecanismo extra de filtro (responde "O seletor do Inspector filtra condições pelo contexto aceito?").
4. **O motivo de falha é do ponto de uso, não da condição.** A condição é um predicado puro e não guarda motivo. Onde a falha precisa de motivo (lista de condições de ativação), cada entrada é uma `ActivationRule` com a condição, uma tag de motivo (`GameplayTag`) e uma chave de texto (ADR 0023). Sem motivo configurado na regra, vale a chave padrão da condição (`virtual string DefaultFailureKey`, no Core, só texto). Assim o Core não depende de Tags (responde "O motivo de falha é uma tag ou uma chave de texto?": os dois, no GAS; só a chave, no Core). Composições não precisam de motivo próprio.
5. **Cargas substituem a recarga na checagem 2 da 0022.** Com cargas habilitadas, a checagem é "tem carga?" (`NoCharges`); o efeito `Cooldown`, quando existe, continua valendo como tempo mínimo entre usos (`OnCooldown`). Decisão do usuário no PRD do B-009 (P2 e P3).
6. **As regras de cargas ficam na Spec do B-009**, sem ADR própria: estado no `AbilitySpec` (não é efeito ativo), recarga sequencial, tempo pelo `ITimeSource` via `ASC.Tick`, gasto no commit, duração da recarga pela mesma conta de redução da habilidade.
7. **Modo de depuração que coleta todas as falhas:** fora da 3.0. A checagem para na primeira falha; o GAS Debugger pode ganhar a coleta depois, por item próprio.
8. **Alvo da ativação.** `AbilityTarget` (`readonly struct`) nasce no B-009 com o ponto; o B-012 acrescenta a unidade sem mudar a assinatura da consulta nem da ativação (0022, item 4).

## Motivo

- Contexto por módulo mantém o Core sem domínio (0012) e deixa o Inspector verificável pelo tipo do campo, sem consulta por tipo em runtime (descarta a opção (b) da 0019).
- Um contexto só no GAS evita duas famílias de And/Or/Not dentro do mesmo módulo, que seria a "lista paralela" proibida pela 0019.
- Motivo no ponto de uso mantém a condição pura e reutilizável (a mesma `AttributeThresholdCondition` vale na ativação e numa ação condicional, onde motivo não faz sentido).

## Consequências

- Cada módulo que adotar condições paga três classes de composição de poucas linhas.
- `Condition<ConditionContext>` é o tipo que o projeto herda para criar uma condição do GAS; o manual mostra a subclasse mínima.
- A lista de condições da habilidade é uma lista de `ActivationRule`, não de condições soltas; o Inspector mostra o motivo na linha da regra.
- A 0019 e a 0022 ganham, ao ser aceita esta ADR, uma emenda datada de uma linha apontando para cá.

## O que os agentes NÃO podem fazer

- Criar um segundo contexto de condição no GAS ou um And/Or/Not paralelo para outro uso do GAS.
- Pôr `GameplayTag` (ou qualquer tipo do Tags) no `SpellBook.Core`.
- Guardar motivo de falha, estado ou cache mutável dentro de uma condição.
- Fazer a checagem de cargas consumir carga antes do commit.

## Em aberto

Nada.

## Relacionadas

- 0012 (módulos), 0013 (costura), 0019 (condições), 0022 (ordem), 0023 (texto do motivo), 0026 (seletor de tipo), 0030 (`ScalableFloat` nos campos de cargas e alcance).
