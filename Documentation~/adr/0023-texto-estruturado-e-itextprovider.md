# 0023 — Texto estruturado + `ITextProvider`

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

`DescriptionElement.GetText(GameplayAbility, AbilitySystemComponent)` (`Runtime/GAS/Abilities/AbilityDescription.cs:14`) devolve string com rich text pronta: sem cultura, sem nível, sem como a view decidir cor ou ícone. Não há formatação por atributo nem abstração de localização. A decisão I.13 do usuário: linhas estruturadas mais `ITextProvider`, com adaptador opcional para o Unity Localization, sem dependência obrigatória.

## Decisão

1. **Saída estruturada.** Quem descreve (efeitos, habilidades, motivos de falha) produz **linhas estruturadas** (structs com rótulo por chave, valor numérico, operação, papel benéfico/prejudicial/neutro e referência ao atributo) numa lista fornecida por quem chama, sem alocar.
2. **Formatação na ponta.** O texto final, a cor e o rich text são montados pela view (0017), com a cultura **injetada** (opções ou configuração), nunca fixa. O formato de cada atributo (tipo de valor, casas decimais, prefixo, sufixo) fica na definição do atributo.
3. **`ITextProvider`** (proposta): todo texto visível ao usuário final que sai do runtime é obtido por chave a partir de um provedor. O pacote traz uma implementação padrão sem dependências.
4. **Unity Localization:** adaptador opcional num asmdef próprio, compilado só quando o pacote `com.unity.localization` está presente (via `versionDefines`). O `package.json` não declara essa dependência.

## Motivo

- A mesma descrição serve a tooltip, lista de buffs e números flutuantes, cada um com o seu estilo.
- Cultura injetada e chaves de texto permitem localizar sem reescrever descrições.
- Adaptador opcional atende quem usa o Unity Localization sem obrigar quem não usa.

## Consequências

- `DescriptionElement` muda de contrato (quebra aceita pela 0009).
- Motivos de falha (0019, 0022) e nomes exibidos de atributos e habilidades passam por chave.

## O que os agentes NÃO podem fazer

- Devolver string com rich text ou cor de dentro do GAS.
- Fixar cultura (por exemplo, `pt-BR` ou `InvariantCulture` para texto exibido) no runtime.
- Escrever texto visível ao usuário final direto no código.
- Declarar `com.unity.localization` como dependência obrigatória ou referenciá-lo fora do asmdef do adaptador.

## Em aberto

- Em que módulo fica o `ITextProvider`: no `SpellBook.Core` ou num módulo `SpellBook.Text` novo (que exigiria emenda na 0012)?
- Forma da implementação padrão: tabela em asset (chave → texto) ou devolver a própria chave/valor padrão do campo?
- Nome do asmdef do adaptador e convenção de nomes das chaves.
- Plural e gênero entram no contrato do provedor ou ficam para o adaptador?

## Relacionadas

- 0009 (quebra), 0012 (módulos), 0017 (view formata), 0019 e 0022 (motivo de falha), 0020 (valores na descrição).
