# 0010 — Unity mínima 6000.0

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O `package.json` declara `"unity": "2021.3"`, mas o pacote nunca foi verificado nessa versão, e o projeto host local usa a 6000.3.5f2 (`ProjectSettings/ProjectVersion.txt`). Declarar uma versão mínima que ninguém testa é prometer o que não se cumpre. A decisão I.2 do usuário fixa a mínima em 6000.0.

## Decisão

1. O `package.json` passa a declarar `"unity": "6000.0"`.
2. ~~Todo código do pacote compila e passa nos testes na 6000.0.~~ (Revogado pela emenda "sem CI de testes": passa a ser "compila e passa nos testes no host local", na versão da Spec.)
3. APIs que só existem a partir de 6000.1 ou mais só podem ser usadas atrás de `#if UNITY_6000_X_OR_NEWER` (ou `versionDefines` no asmdef), com caminho equivalente para a 6000.0.
4. Aumentar a versão mínima é quebra (MAJOR, 0008) e exige ADR nova.

## Motivo

- Permite usar recursos atuais da Unity sem condicionais para versões antigas.
- Alinha a promessa do `package.json` com o que é desenvolvido e testado.

## Consequências

- Projetos em 2021.3, 2022 ou 2023 não instalam a 3.0.
- ~~A verificação na 6000.0 precisa de um Editor dessa versão em algum lugar (local ou CI); hoje só existe a 6000.3.5f2 no host.~~ (Histórico. Revogado pela emenda "sem CI de testes": a verificação é só no host, na versão da Spec, hoje 6000.3.13f1; a 6000.0 é o mínimo declarado, coberto pelo item 3.)

## O que os agentes NÃO podem fazer

- Usar API mais nova que a 6000.0 sem guarda de versão.
- Mudar o campo `unity` do `package.json` sem ADR.
- Afirmar compatibilidade com uma versão em que os testes não rodaram.

## Decisões incorporadas (2026-10-29)

- (Revogado pela emenda "sem CI de testes".) **Matriz do CI** (decisão I30): a suíte EditMode e PlayMode roda, com GameCI, na **6000.0** (último patch LTS disponível no GameCI na data do ticket de CI) e na **última 6000.x** disponível no GameCI. As duas versões ficam fixadas no workflow (string exata, por exemplo `6000.0.NNfN`) e são atualizadas por ticket próprio, nunca por `latest`, para o CI ser reprodutível.
- (Revogado pela emenda "sem CI de testes".) **Release só com as duas versões verdes.** O host local pode usar outra 6000.x; o que vale como prova de compatibilidade é o CI.
- **Sem `unityRelease`** (decisão técnica): o pacote promete qualquer patch da 6000.0. Motivo: ~~o CI testa um patch só~~ (histórico; motivo atual na emenda "sem CI de testes"), e travar o patch mínimo sem um bug conhecido que o justifique só restringe quem instala. Se um bug de patch aparecer, entra `unityRelease` por emenda.
- O host de testes (0004) fixa em `HostProject~/ProjectSettings/ProjectVersion.txt` a versão indicada na Spec (hoje 6000.3.13f1, decisão D5 da `spec/modularizacao.md`) ~~e volta à 6000.0 quando ela estiver instalada; a prova da 6000.0 é o CI~~ (revogado pela emenda "sem CI de testes": a volta à 6000.0 é opcional e não há CI). (Emendado em 2026-10-29; ver a emenda do host abaixo.)

## Emenda (2026-10-29): editor próprio em UI Toolkit, pela 0026

Esta emenda vale quando a 0026 for aceita.

- O editor próprio do pacote (seletor de tipo, drawers, Inspectors e janelas) é escrito em UI Toolkit (I46) e segue o item 3 da Decisão: só usa APIs que existem na 6000.0.
- As APIs-base são `PropertyDrawer.CreatePropertyGUI`, `Editor.CreateInspectorGUI`, `EditorWindow.CreateGUI`, `TypeCache` e `SerializationUtility` (tipos ausentes de `[SerializeReference]`). O primeiro ticket que usa cada uma confirma no red que ela existe e se comporta como esperado na 6000.0.
- ~~O CI na 6000.0 e na última 6000.x continua sendo a prova de compatibilidade, e agora roda sem o Odin.~~ (Revogado pela emenda "sem CI de testes".)

## Emenda (2026-10-29): host local fora da 6000.0

Decisão do usuário: a 6000.0 não está instalada na máquina, então o host usa a 6000.3.13f1 (D5 da Spec). O texto da consequência sobre o host (seção "Decisões incorporadas") foi trocado. Onde esta ADR pede que um red confirme algo "na 6000.0", o red confirma no host, ~~e a confirmação na 6000.0 vem do job 6000.0 do CI~~ (revogado pela emenda "sem CI de testes"). O mínimo declarado (`"unity": "6000.0"`) ~~e a matriz do CI~~ não mudam (a matriz foi revogada pela emenda "sem CI de testes").

## Emenda (2026-10-29): sem CI de testes, verificação só no host

Decisão do usuário (2026-10-29): "tira essa regra. A ideia é que o sistema funciona a partir do 6000, o que já vai funcionar". Os secrets da licença da Unity (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) não serão cadastrados, então não existe CI de testes com GameCI. Esta emenda substitui a emenda "host local fora da 6000.0" na parte do CI e muda o seguinte:

- **Continua:** o mínimo declarado `"unity": "6000.0"` (item 1), o item 3 (API mais nova que a 6000.0 só com guarda de versão), o item 4 e a regra "sem `unityRelease`". Motivo atual dessa regra: nenhum patch da 6000.0 é testado, e a cobertura dela vem do item 3, que vale para a 6000.0 inteira. Travar o patch mínimo sem um bug conhecido que o justifique só restringe quem instala.
- **Item 2 da Decisão:** passa a ser "todo código do pacote compila e passa nos testes no host local (`HostProject~/`, na versão da Spec, hoje 6000.3.13f1)". A 6000.0 é o mínimo declarado, não uma versão verificada por teste. A cobertura dela é o item 3, cobrado pelo Examinador na revisão.
- **Deixam de valer** (cada trecho está marcado no lugar de origem):
  - em "Consequências": "A verificação na 6000.0 precisa de um Editor dessa versão em algum lugar (local ou CI); hoje só existe a 6000.3.5f2 no host";
  - em "Decisões incorporadas": "Matriz do CI", "Release só com as duas versões verdes", o motivo "o CI testa um patch só" (a regra "sem `unityRelease`" continua, com o motivo acima) e, no item do host, "a prova da 6000.0 é o CI";
  - na emenda "editor próprio em UI Toolkit": "O CI na 6000.0 e na última 6000.x continua sendo a prova de compatibilidade, e agora roda sem o Odin";
  - na emenda "host local fora da 6000.0": "a confirmação na 6000.0 vem do job 6000.0 do CI" e "a matriz do CI não muda".

  Release passa a exigir a suíte inteira verde no host.
- **Reds "na 6000.0"** (emenda do editor em UI Toolkit): o red confirma a API no host. Se a API não existir na 6000.0 segundo a documentação da Unity daquela versão, vale o item 3.
- **"Afirmar compatibilidade com uma versão em que os testes não rodaram"** continua proibido. O manual e o README dizem "Unity 6000.0 ou mais nova" como requisito declarado e citam como testada só a versão do host. Nenhum texto diz que a 6000.0 ou a última 6000.x foram testadas.
- O host continua fora da 6000.0 (D5 da Spec), agora como decisão e não como desvio à espera do CI. Se a 6000.0 for instalada um dia, trocar o host para ela é opcional, por ticket `chore(host)`.

Um CI de testes futuro precisa de ADR nova ou emenda, e de decisão do usuário sobre os secrets.

## Em aberto

Nada.

## Relacionadas

- 0004 (testes), 0008 (SemVer), 0009 (3.0).
