# 0021 — Magnitude configurável (snapshot ou ao vivo) e pilhas

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

Em efeitos com duração, o valor de cada modificador é calculado uma vez, na aplicação (snapshot), sem opção de valor ao vivo. O empilhamento (`GameplayEffect.CanStack`/`MaxStacks`, `GameplayEffect.cs:82-85`; `ActiveEffectInstance.cs:66`) incrementa `CurrentStacks`, mas não muda a magnitude. A decisão I.11 do usuário: magnitude configurável por modificador, e pilhas multiplicando a magnitude por padrão, com opção de desligar.

## Decisão

1. Cada modificador de efeito com duração tem um **modo de magnitude**:
   - **Snapshot**: calculado na aplicação e fixo enquanto o efeito existir (comportamento atual);
   - **Ao vivo**: recalculado quando muda algo de que o valor depende (atributo da origem ou do alvo, nível, pilhas).
2. **Pilhas multiplicam a magnitude** por padrão: com N pilhas, o modificador vale N vezes o valor de uma pilha.
3. Existe uma opção para desligar a multiplicação, para efeitos em que a pilha só conta (por exemplo, para renovar duração ou alimentar outra regra).
4. Mudanças de magnitude e de pilhas recalculam o atributo afetado e disparam os eventos C# já existentes do atributo.

## Motivo

- Snapshot e ao vivo são os dois comportamentos esperados por quem vem dos modelos de referência; escolher por modificador cobre os dois sem duplicar efeitos.
- Pilhas que não mudam a magnitude obrigam quem usa o pacote a compensar com fórmula própria.

## Consequências

- Efeitos empilháveis existentes passam a ter magnitude multiplicada ao atualizar para a 3.0: é quebra de comportamento e vai ao CHANGELOG (0009).
- O modo "ao vivo" exige assinar mudanças das fontes e cuidar de dependência circular entre atributos.

## O que os agentes NÃO podem fazer

- Recalcular modificadores ao vivo por polling a cada frame.
- Mudar o padrão (multiplicar pilhas) sem ADR.
- Permitir dependência circular ao vivo sem detecção e erro do validador.

## Em aberto

- A opção de desligar a multiplicação fica no efeito (vale para todos os modificadores) ou em cada modificador?
- Ticks periódicos (`PeriodicModifiers`) usam o modo do modificador ou sempre avaliam no tick?
- Como detectar e reportar ciclos no modo ao vivo (A depende de B que depende de A)?
- Políticas de pilha além desta (agregação por origem, expiração por pilha) entram nesta 3.0 ou ficam para outra ADR?

## Relacionadas

- 0009 (quebra de comportamento), 0020 (valores por nível), 0024 (dano).
