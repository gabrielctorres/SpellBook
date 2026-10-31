# Event Debugger {#event-debugger}

O Event Debugger é uma janela do Editor que mostra os [canais](../modulos/eventos.md#canais) do
projeto, quem está ouvindo cada um e o histórico dos disparos. Serve para responder, em Play, "o
evento foi disparado?", "quando?", "com qual payload?", "quem disparou?" e "quem ouviu?".

A janela fica no assembly `SpellBook.Events.Editor` e só existe no Editor.

## Abrir {#abrir}

Menu **Window > SpellBook > Event Debugger**. A janela pode ficar encaixada ao lado do Inspector,
como qualquer outra.

O histórico só é gravado **com a janela aberta**. Abra a janela antes de entrar em Play, ou antes
do disparo que você quer ver. Fechar a janela para a gravação, e com ela fechada os canais não
têm custo extra.

## Canais {#canais}

A parte de cima lista todos os assets de canal do projeto, em ordem de nome, e também os canais
criados em memória durante o Play. Cada linha mostra:

- o nome do canal e o tipo do payload, por exemplo `PlayerDied (Void)`;
- quantos ouvintes estão inscritos agora;
- abaixo, quem são esses ouvintes. A resposta de um `EventListener` aparece com o nome do
  objeto e o tipo do dono, como `Painel (EventListener)`, e um componente que ouve por código
  aparece do mesmo jeito, como `Painel (MeuComponente)`. Um objeto destruído aparece como
  `(destruído)`, e um ouvinte que não é objeto da Unity aparece pelo texto dele.

A lista se atualiza sozinha. Fora do Play os canais normalmente não têm ouvintes, porque o
`EventListener` só se inscreve quando está ativo na cena em Play.

Clique numa linha para destacar o asset do canal na janela Project.

## Histórico de disparos {#historico}

A parte de baixo mostra os disparos dos canais, **o mais recente primeiro**. Cada linha tem:

| Parte | O que é |
|---|---|
| Horário | hora do relógio do computador, com milissegundos (`14:30:05.250`) |
| Quadro | o `Time.frameCount` da Unity no disparo |
| Canal | o nome do canal |
| Payload | o valor enviado, como texto. Canais Void não mostram payload |
| Emissor | o objeto que disparou, quando ele é conhecido |

O emissor é conhecido quando o disparo vem de um [`EventEmitter`](../modulos/eventos.md#eventemitter),
de um `Raise(payload, sender)` por código ou de uma reação que repassa o disparo. Um `Raise(payload)`
sem emissor e o botão **Raise** do Inspector do canal aparecem sem emissor.

Clique num disparo para destacar o emissor (ou o canal, se não houver emissor) no Project ou na
Hierarchy.

O histórico guarda os **últimos 256 disparos**. Quando chega o 257º, o mais antigo sai. Ele
recomeça a cada entrada no Play, e continua visível depois de sair do Play, até a próxima entrada
ou até você limpar.

O histórico mostra só disparos de canais. Eventos de código pelo
[`EventBus<T>`](../modulos/eventos.md#eventbus) não aparecem.

## Busca {#busca}

O campo de busca da barra de cima filtra pelo **nome do canal**, sem diferenciar maiúsculas de
minúsculas. Ele filtra as duas listas ao mesmo tempo: digitar `player` deixa só os canais com
"player" no nome e os disparos desses canais. Com o campo vazio, tudo aparece.

## Limpar {#limpar}

O botão **Limpar** apaga o histórico de disparos. A lista de canais não muda. O botão **?** abre
esta página.

## Find references {#find-references}

O **Find references** responde "quem emite e quem ouve este canal?" fora do Play, lendo o que está
configurado nos objetos. Há três jeitos de usar:

- no Event Debugger, selecione um canal na lista e clique em **Find References** na barra de cima;
- no Inspector do canal, abra o menu da engrenagem e escolha **Find References**;
- na janela Project, clique com o botão direito no asset do canal e escolha
  **SpellBook > Find References**.

O resultado aparece na parte de baixo do Event Debugger, em **Referências**, com uma linha por
objeto e papel:

| Papel | Quem é |
|---|---|
| `emite` | o canal de uma emissão (`EventEmitter`, `RaiseEventAction`) ou de uma reação que dispara outro canal |
| `ouve` | o canal de uma resposta (`EventListener` ou outro objeto com uma resposta) |
| `referencia` | um campo comum com o canal, que não emite nem ouve pelo Inspector |

Cada linha mostra também o objeto, como `Botao (EventEmitter)`, e onde ele está: o caminho do
prefab ou do asset, ou o caminho da cena. Clique numa linha para selecionar o objeto, que aparece no
Inspector e fica destacado no Project ou na Hierarchy.

O escopo é **os assets do projeto (prefabs e assets `.asset`) e as cenas abertas**. Cenas fechadas
não são abertas nem lidas: abra a cena (inclusive de forma aditiva) antes de procurar, se quiser
vê-la no resultado. Um canal criado em memória durante o Play não tem asset, e por isso só mostra as
referências das cenas abertas. A lista não se atualiza sozinha: clique de novo depois de mudar algo.

## Exemplo: o jogador morreu {#exemplo}

1. Crie um canal em **Create > SpellBook > Events > Void Channel** com o nome `PlayerDied`.
2. Num botão de UI, adicione um `EventEmitter` com o canal `PlayerDied` e ligue o `OnClick` do
   botão a **EventEmitter > Raise ()**.
3. Num objeto da cena, adicione um `EventListener` com uma resposta ao canal `PlayerDied` e a
   reação **SetActive**, que ativa um painel.
4. Abra o Event Debugger e entre em Play.
5. Na lista de canais, `PlayerDied (Void)` mostra 1 ouvinte, `Painel (EventListener)`, se o
   `EventListener` estiver num objeto chamado `Painel`.
6. Clique no botão. O painel aparece, e o histórico ganha uma linha com o horário, o quadro,
   `PlayerDied` e o botão como emissor.
