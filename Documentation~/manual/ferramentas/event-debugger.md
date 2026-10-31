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
