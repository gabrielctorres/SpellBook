# 0010 — Unity mínima 6000.0

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O `package.json` declara `"unity": "2021.3"`, mas o pacote nunca foi verificado nessa versão, e o projeto host local usa a 6000.3.5f2 (`ProjectSettings/ProjectVersion.txt`). Declarar uma versão mínima que ninguém testa é prometer o que não se cumpre. A decisão I.2 do usuário fixa a mínima em 6000.0.

## Decisão

1. O `package.json` passa a declarar `"unity": "6000.0"`.
2. Todo código do pacote compila e passa nos testes na 6000.0.
3. APIs que só existem a partir de 6000.1 ou mais só podem ser usadas atrás de `#if UNITY_6000_X_OR_NEWER` (ou `versionDefines` no asmdef), com caminho equivalente para a 6000.0.
4. Aumentar a versão mínima é quebra (MAJOR, 0008) e exige ADR nova.

## Motivo

- Permite usar recursos atuais da Unity sem condicionais para versões antigas.
- Alinha a promessa do `package.json` com o que é desenvolvido e testado.

## Consequências

- Projetos em 2021.3, 2022 ou 2023 não instalam a 3.0.
- A verificação na 6000.0 precisa de um Editor dessa versão em algum lugar (local ou CI); hoje só existe a 6000.3.5f2 no host.

## O que os agentes NÃO podem fazer

- Usar API mais nova que a 6000.0 sem guarda de versão.
- Mudar o campo `unity` do `package.json` sem ADR.
- Afirmar compatibilidade com uma versão em que os testes não rodaram.

## Decisões incorporadas (2026-10-29)

- **Matriz do CI** (decisão I30): a suíte EditMode e PlayMode roda, com GameCI, na **6000.0** (último patch LTS disponível no GameCI na data do ticket de CI) e na **última 6000.x** disponível no GameCI. As duas versões ficam fixadas no workflow (string exata, por exemplo `6000.0.NNfN`) e são atualizadas por ticket próprio, nunca por `latest`, para o CI ser reprodutível.
- **Release só com as duas versões verdes.** O host local pode usar outra 6000.x; o que vale como prova de compatibilidade é o CI.
- **Sem `unityRelease`** (decisão técnica): o pacote promete qualquer patch da 6000.0. Motivo: o CI testa um patch só, e travar o patch mínimo sem um bug conhecido que o justifique só restringe quem instala. Se um bug de patch aparecer, entra `unityRelease` por emenda.
- O host de testes (0004) fixa a 6000.0 em `HostProject~/ProjectSettings/ProjectVersion.txt`, para que o desenvolvimento local aconteça na versão mínima.

## Em aberto

Nada.

## Relacionadas

- 0004 (testes), 0008 (SemVer), 0009 (3.0).
