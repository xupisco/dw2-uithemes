# DW2 UI Themes: plano e status

Uso em [README.md](README.md); tudo o que sabemos do jogo em [FINDINGS.md](FINDINGS.md).

Atualizado em 2026-11-25.

## Status

| Fase | Estado |
|---|---|
| 0. Projeto (csproj, carga pelo loader e pelo launcher, `install.ps1`) | feito |
| 1. Diagnóstico (dumps dos 7 esquemas, cores de teste, troca ao vivo) | feito |
| 2. Temas (formato JSON, 5 temas, launcher, atalhos, mensagem na tela) | feito |
| 3. Cores personalizadas | substituída pelo editor (`tools\theme-editor.html`) |
| 4. Troca ao vivo completa, tooltips, janela do objeto selecionado | feito e testado no jogo, com uma limitação aceita (abaixo) |
| Limpeza antes de publicar (0.3.0) | feito |
| 5. Publicação | em andamento: metadados, descrição e editor em inglês prontos; faltam imagens e repositório |

**Temas:** Amethyst, Basalt (criado no editor pelo usuário), Graphite, Obsidian e Synthwave.

### Confirmado no jogo

- Tema escolhido no launcher, aplicado desde a abertura, com botões, abas e seleção certos.
- Troca por `Ctrl+Alt+±`, com o nome na tela e `Ctrl+Alt+0`.
- Degradê dos painéis removido (`OverlayTintColor`).
- Tooltips de texto com o fundo do tema e opacos (`opaqueTooltips`).
- Tooltips da janela do objeto selecionado com o fundo do tema (`SelectionPanel.HotspotBackColor`).
- Pasta de temas relida a cada troca (JSON editado aparece sem reiniciar).
- Na 0.3.0 (2026-11-25), testado pelo usuário: troca ao vivo pelo atalho (`LiveRefresh`) com aba ativa, item selecionado e botões ligados seguindo o tema; **Off** voltando ao jogo original; hover de lista seguindo o tema. Nenhuma lentidão notada (sem medição numa partida grande).

### Limitações aceitas

- Degradê lateral da janela do objeto selecionado: não vem de nenhuma cor (seis sondas). O usuário decidiu deixar como está em 2026-11-24. Caminho, se um dia voltar: registrar as chamadas `SpriteBatch.Draw` dentro de `SelectionPanel.RenderToTexture`, achar a do degradê e pular ou recolorir essa chamada.
- Painel do sistema embaixo ao centro e elementos do mapa (moldura de seleção, ícones, barra de população) não seguem o tema.

## Próximos passos

### Limpeza antes de publicar

- [x] Versão 0.3.0 (`UIThemes.csproj`, `mod.json`) e `UIThemes\changelog.txt`, que vai no pacote. (2026-11-25)
- [x] Tema padrão: Basalt (`settings.schema.json` e `Entry.ApplyOptions`). (2026-11-25)
- [x] Diagnóstico no pacote: flags e `DumpColors` ficam, porque são baratos e úteis para criadores de temas. As sondas continuam fora.
- [x] Log do `SetColorScheme` em uma linha; a pilha de chamadas só sai com `DumpColors` ligado. (2026-11-25)

### Fase 5: publicação

- [x] `dw2modlauncher.json` com `uid` novo, `author` (Leandro) e `releaseRepo` (`xupisco/dw2-uithemes`). (2026-11-25)
- [x] `dw2modlauncher.hooks.json`: **não é necessário**. Ele não lista os patches; registra conflitos revisados com outro mod. Nosso único alvo compartilhado é `DWGame.Update` com o Refreshed Galaxy View, e os dois só observam. Ver FINDINGS. (2026-11-25)
- [x] `description.bbcode` (em inglês, sem imagens ainda). (2026-11-25)
- [x] Imagem de preview: `UIThemes\preview.jpg` (2560×1440), gerada de `tools\thumbnail.html` com `tools\build-thumbnail.ps1`. Mostra os 5 temas em mini painéis com as cores reais. (2026-11-25)
- [x] `mod.json` com `previewImage`, `descriptionFile` e `shortDescription`, como os mods do Refreshed, e `preview.jpg` no pacote. É daí que o launcher tira a imagem e o texto da lista. (2026-11-25)
- [x] Capturas no jogo: Basalt, Graphite e Obsidian na mesma cena, preparadas em `workshop\` (3 JPG e `compare.jpg`) com `tools\build-screenshots.ps1`. Vão para a galeria da Workshop depois de publicar. (2026-11-25)
- [ ] Opcional: capturas de Amethyst e Synthwave, os temas mais coloridos.
- [x] Testado com todos os mods Refreshed ativos (log do launcher de 2026-11-25), sem problemas relatados.
- [x] Editor vai no pacote como `theme-editor.html`. (2026-11-25)
- [x] Editor traduzido para inglês (`tools\theme-editor.template.html`), conferido no navegador. (2026-11-25)
- [ ] Criar o repositório `xupisco/dw2-uithemes` e a primeira release, se o launcher for buscar atualizações por ele.

## Perguntas em aberto

- Como o launcher encontra a classe `Entry`? (Funciona, mas não sabemos a regra.)
- O `enum` do launcher aceita uma lista gerada na hora? Hoje cada tema novo precisa ser adicionado à mão em `options`. Para jogadores, isso significa que um tema próprio só entra pelo atalho (por sessão) ou pelo `options.json` sem o launcher.
- Quanto custa o `LiveRefresh` por quadro numa partida grande?

## Decisões

- **2026-11-24:** sem "esquema base" nos temas. O tema vai por cima do esquema escolhido e define os 24 campos do esquema, para ficar igual com qualquer escolha e preservar o Colorblind.
- **2026-11-24:** atalhos com `Ctrl+Alt` e `+`, `-`, `0`, porque o jogo ignora modificadores nas F-keys.
- **2026-11-24:** cores personalizadas pelo editor HTML, em vez de sliders no launcher (o launcher não tem seletor de cor).
- **2026-11-24:** degradê lateral da janela do objeto selecionado fica como está.
- **2026-11-25:** Basalt é o tema padrão do launcher e do `options.json`.
