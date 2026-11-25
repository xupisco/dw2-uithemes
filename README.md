# DW2 UI Themes

Mod para Distant Worlds 2 que troca as cores da interface por temas. Os temas são arquivos JSON simples, e o editor visual em `tools\` gera esses arquivos.

- Achados sobre o jogo e o mapa de cores: [FINDINGS.md](FINDINGS.md)
- Status e próximos passos: [PLAN.md](PLAN.md)

## Instalar (desenvolvimento)

Requisitos: .NET SDK (8 ou mais novo) e o jogo instalado.

```powershell
.\install.ps1
```

O script:
1. compila `UIThemes\` em Release;
2. copia a DLL, o `mod.json`, o `settings.schema.json`, o `dw2modlauncher.json`, o `changelog.txt`, o `description.bbcode`, o `preview.jpg`, o `theme-editor.html` e a pasta `themes\` para `<jogo>\mods\DW2UIThemes\`;
3. adiciona `mods/DW2UIThemes` ao `mods.json` e ao `mods.<perfil>.json` do launcher, com backup `*.uithemes_backup`.

Opções:
- `-NoRegister`: só copia, sem mexer nas listas de mods.
- `-GameDir "..."`: outra pasta do jogo.

Com o jogo aberto a DLL fica bloqueada: feche o jogo para instalar uma DLL nova. Os JSON de tema podem ser copiados para `<jogo>\mods\DW2UIThemes\themes\` a qualquer momento.

## Usar

- **Escolher o tema:** no DW2 Mod Launcher, opção **UI theme** (Off, Amethyst, Basalt, Graphite, Obsidian, Synthwave; padrão Basalt). Sem o launcher, use `options.json` na pasta do mod: `{"Theme": "Basalt"}`. O tema vale desde a abertura do jogo.
- **Com o jogo aberto:**

  | Atalho | Ação |
  |---|---|
  | `Ctrl+Alt++` | próximo tema (Off incluído); o nome aparece na tela |
  | `Ctrl+Alt+-` | tema anterior |
  | `Ctrl+Alt+0` | mostra o tema atual |

  A troca pelo atalho vale só para a sessão. A pasta `themes\` é relida a cada troca, então um JSON editado aparece trocando de tema e voltando.
- **O tema e o esquema do jogo:** o tema é aplicado por cima do esquema de cor escolhido nas opções do jogo. Se você usa Colorblind Assist, as cores de aviso e status dele continuam valendo.
- **Log:** `<jogo>\data\Logs\DW2UIThemes\DW2UIThemes.log`.

## Criar um tema

O caminho mais fácil é o editor:

1. Abra `tools\theme-editor.html` no navegador (funciona offline).
2. Escolha um ponto de partida, ajuste as cores olhando o mockup e clique em **Copy JSON** (a interface do editor é em inglês, porque ele vai no pacote para os jogadores).
3. Salve como `UIThemes\themes\<nome>.json` e adicione o nome em `options` no `UIThemes\settings.schema.json`.
4. Rode `.\tools\build-editor.ps1` para o editor incluir o tema novo como preset.

### Formato

```jsonc
{
  "name": "Basalt",          // nome mostrado no jogo e usado no launcher
  "opaqueTooltips": true,    // opcional: tooltips sem a transparência do painel
  "colors": {
    "AccentColor": "#5076B4",                                      // campo do ColorHelper
    "BackColorLightTransparent": "#121212F0",                      // com alpha
    "UserInterfaceController.HoverTipBackColorDefault": "#141414FF", // estático de outra classe
    "SelectionPanel.HotspotBackColor": "#141414FF"                 // cor própria de um controle
  }
}
```

- **Cores:** `#RRGGBB` mantém a transparência original do campo, e `#RRGGBBAA` define a transparência também. Comentários `//` e vírgula sobrando no fim são aceitos.
- **Chaves aceitas:**
  - qualquer campo `Color` do `DistantWorlds.Types.ColorHelper`, pelo nome (lista nos dumps em `reference\`);
  - `UserInterfaceController.<campo>` e `EmpireMessage.<campo>` (cores estáticas);
  - `SelectionPanel.<cor>`, onde a cor é `SectionBackColor`, `HotspotBackColor`, `HighlightColor`, `GraphBackColor`, `BackColor`, `ShadowColor`, `DarkColor`, `BorderColor` ou `ForeColor`.
- **Tons do destaque:** se `AccentColorMid`, `AccentColorDark`, `AccentColorMidDark` e `AccentColorVeryDark` não estiverem no tema, o mod calcula a partir do `AccentColor` (× 0,7, × 0,5, × 0,25, × 0,125), como o jogo faz.
- **Campos do esquema:** defina os 24 campos que os esquemas do jogo mudam (o editor sempre inclui), para o tema ficar igual com qualquer esquema. O log avisa quais faltam.
- Chaves desconhecidas e cores inválidas geram um aviso no log, uma vez por arquivo.

O que cada campo pinta está no [mapa da tela, em FINDINGS.md](FINDINGS.md#mapa-da-tela-elemento--cor).

## Ferramentas de desenvolvimento

- **Sondas:** `UIThemes\probes\*.json` pintam campos suspeitos com cores fortes, para descobrir o que cada um pinta. Para usar, copie para a pasta `themes\` do jogo. Elas não vão no pacote.
- **Flags:** na pasta do log, criar `toggle.flag` liga ou desliga cores de teste fortes, e `dump.flag` grava todas as cores atuais em `current-*.json`. O mod confere a cada segundo.
- **Dumps por esquema:** a opção oculta `DumpColors` grava `scheme-<n>.json` a cada troca de esquema.
- **Referência:** `reference\scheme-<n>-<nome>.json` são os 7 esquemas do jogo, gravados em jogo.
- **Imagem da Workshop:** `.\tools\build-thumbnail.ps1` renderiza `tools\thumbnail.html` com o Edge sem janela e grava `UIThemes\preview.jpg` (2560×1440). As cores dos temas estão copiadas no HTML: ao mudar um tema, atualize a lista `THEMES` lá.
- **Capturas da Workshop:** salve as capturas do jogo como `ss_<tema>.png` na raiz (mesma cena em todas) e rode `.\tools\build-screenshots.ps1`. Ele grava em `workshop\` um JPG de cada captura e o `compare.jpg`, com o painel da esquerda dos temas lado a lado (área ajustável com `-Crop x,y,largura,altura`). A pasta `workshop\` não vai no pacote.
- **Release no GitHub:** `.\tools\build-release.ps1` compila e grava em `release\` os arquivos que o launcher lê (mesmo formato do Refreshed): `DW2UIThemes-v<versão>.zip` (pasta `DW2UIThemes/` dentro), `.zip.sha256`, `.manifest.json` e `.preview.jpg`. Depois:
  ```powershell
  gh release create DW2UIThemes-v<versão> release\DW2UIThemes-v<versão>.* --target master --title "UI Themes <versão>" --notes-file <notas>
  ```
  Antes, suba a versão no `UIThemes.csproj` e no `mod.json` e escreva a entrada no `changelog.txt`. Quando o mod tiver `workshopId` no `mod.json`, o manifesto inclui o id.

## Código (`UIThemes\src\`)

| Arquivo | Papel |
|---|---|
| `Entry.cs` | Entradas do mod (`Mod` para o loader do jogo, `Entry` para o launcher), leitura das opções e instalação dos patches |
| `Themes.cs` | Carrega os temas, aplica depois de cada `SetColorScheme`, restaura campos extras, atalhos, mensagem na tela, fundo dos tooltips e `opaqueTooltips` |
| `LiveRefresh.cs` | Guarda o histórico das cores e troca, nas propriedades `...Resolved` de `DWButton` e `DWControl`, as cópias antigas pelas cores atuais (troca de tema com o jogo aberto) |
| `Overrides.cs` | Cores próprias de controles (`SelectionPanel.*`) via postfix nas propriedades `...Resolved`; registra os valores do jogo no log |
| `Diagnostics.cs` | Dumps, cores de teste e flags |
| `Log.cs` | Log em arquivo |

Os patches são instalados por recurso, cada um com seu id de Harmony. Se um falhar, os outros continuam funcionando, e a falha vai para o log.
