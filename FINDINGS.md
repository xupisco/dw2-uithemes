# Achados: cores da interface do Distant Worlds 2

O que sabemos sobre como o DW2 carrega mods e pinta a interface, levantado entre 2026-11-24 e 2026-11-25 com a instalação local, descompilação (ilspy), dumps em jogo e temas de sonda.

**Legenda de confiança:**
- **confirmado**: visto no jogo.
- **medido**: pixel de captura de tela ou valor no log.
- **provável**: deduzido, sem teste direto.

## Ambiente

| O quê | Onde / valor |
|---|---|
| Jogo | `D:\games\steam\steamapps\common\Distant Worlds 2` |
| Mods locais | `<jogo>\mods\<Nome>\`. A ordem de carga fica em `mods\mods.json`; o launcher mantém uma cópia por perfil (`mods.<perfil>.json`, perfil em `currentProfile.txt`) |
| Mods da Workshop | `D:\games\steam\steamapps\workshop\content\1531540\<id>` |
| Textos | `<jogo>\data\GameText.txt` |
| Log do nosso mod | `<jogo>\data\Logs\DW2UIThemes\DW2UIThemes.log` |
| Runtime | Stride, .NET 8 (`DistantWorlds2.Windows.runtimeconfig.json`) |
| Harmony | `0Harmony.dll` 2.3.3 na raiz do jogo |
| Assemblies | `DistantWorlds2.dll` (`DWGame`, `ScaledRenderer`), `DistantWorlds.Types.dll` (`ColorHelper`, `GameSettings`, `HoverTip`, `Galaxy`), `DistantWorlds.UI.dll` (`DWControl`, `DWButton`, `SelectionPanel`, `UserInterfaceController`), `Stride.Core.Mathematics.dll` (`Color`), `Stride.Input.dll`, `Stride.Engine.dll`, `Stride.Games.dll` |

### Código protegido

- O ilspy mostra os métodos do jogo vazios e marcados com `[MethodImpl(NoInlining)]`. O conteúdo real deve vir de `data\dat1..3` em tempo de execução.
- Nomes, campos, propriedades e assinaturas aparecem normalmente. Construtores estáticos aparecem com conteúdo (ex.: os valores padrão de `UserInterfaceController`).
- O Harmony funciona (prefix e postfix testados), e os getters não são inlined. **confirmado**
- Valores internos só dá para ler com o jogo rodando (dumps e log).

## Carregamento de mods

### Loader nativo do jogo

O `mod.json` aponta a DLL e a classe; o jogo cria a classe passando o `DWGame`:

```json
{ "name": "X", "displayName": "X", "mainModule": "X.dll", "mainClass": "X.Mod", "version": "1.0.0", "dependencies": [] }
```

```csharp
public class Mod { public Mod(DistantWorlds2.DWGame game) { } }
```

### DW2 Mod Launcher (lucky-wolf)

- O launcher carrega a DLL e chama `public static void InitWithOptions(string json)` (ou `Init()`) numa classe estática `Entry`.
- Ele chamou a nossa `Entry` mesmo com `mainModule` no `mod.json`, e o loader nativo não iniciou o mod uma segunda vez (temos uma proteção contra isso). **confirmado**
- Como o launcher encontra a `Entry` (convenção ou configuração) ainda não sabemos.
- O JSON de opções traz as chaves do `settings.schema.json` do mod, mais `Language`.
- Arquivos do mod que o launcher lê:
  - `dw2modlauncher.json`: `uid`, `author`, `releaseRepo`, `dependencies`.
  - `settings.schema.json`: grupos → `{key, type, label, description, default, options?, hidden?}`. Tipos: `bool`, `enum` (com `options`), `float`, `folder`. Não há seletor de cor.
  - `dw2modlauncher.hooks.json`: **não** é a lista de patches do mod. É `{"schema":1,"claims":[...]}`, e cada claim registra um alvo compartilhado com um mod parceiro (`target`, `partner`, `self`/`other` com `hash` e `facts`, `reviewed`, `note`): um conflito já revisado. Mods sem alvo em conflito não têm o arquivo. **confirmado** (Workshop `3838171190`, `3840104109`)
  - O `dw2modlauncher.json` também aceita `hooksCoordinatedWith` (`["Mod@versão"]`) e, nas dependências, `mod`, `uid`, `workshopId`, `required`, `loadAfter`, `steamLink`.
- O launcher grava os patches que observou em `%APPDATA%\DW2ModLauncher\hooks-observed.json` (`hooks[]`: `target` e `patches[]` com `owner`, `assembly`, `harmonyId`, `kind`, `effect` como `observes`/`changes-result`/`changes-args`/`skips-original`/`rewrites`). Serve para ver quem mais mexe num método. **confirmado**
  - Em 2026-11-25, com todos os Refreshed: só `DWGame.Update` é compartilhado (com DW2R-Galaxy-View), e todos os patches nele são `observes`.
- Status opcional por reflexão: `DW2ModLauncher.Loader.ModStatus` (`Register`, `SetLevel`, `Detail`, `Error`, `Installed`, `SetLog`).
- **Release no GitHub (formato do Refreshed):** tag `<id>-v<versão>` com `<tag>.zip` (pasta do mod dentro), `<tag>.zip.sha256`, `<tag>.manifest.json` (`schema`, `id`, `uid`, `name`, `kinds`, `version`, `tag`, `author`, `shortDescription`, `workshopId`, `minLauncherVersion`, `tags`, `dependencies`, `zip`, `size`, `sha256`, `publishedUtc`) e `<tag>.preview.jpg`. O `mod.json` deles tem `descriptionFile`, `shortDescription`, `previewImage` e `workshopId`. **confirmado** (`DW2R-Galaxy-View-v1.0.5`)
- **Publicar na Workshop pelo launcher:** ele usa o Steamworks.NET e precisa da `steam_api64.dll` de um SDK recente (com `SteamInternal_SteamAPI_Init`) na pasta do launcher. A 1.0.52 baixada em `D:\apps\` veio sem ela (`DllNotFoundException`). A do DW2 é antiga demais (SDK com `SteamUGC_v016`). Em 2026-11-25 copiamos a do Factorio (v10.87, `SteamUGC_v021`, assinada pela Valve). Para desfazer, é só apagar o arquivo. **confirmado**: a publicação funcionou sem reabrir o launcher (ele tenta carregar a DLL de novo a cada publicação).
- **Depois de publicar, o launcher grava na pasta instalada do mod:** `workshopId` (e `bundles: []`) no `mod.json`, e o `author` das configurações dele no `dw2modlauncher.json`. Esses arquivos precisam voltar para o repositório, senão o próximo `install.ps1` apaga o id. **confirmado**
- Referência de mod bem feito: Refreshed Shipset Colors (`3833408745`, repositório `salemonz/DW2-Refreshed`).

### Teclado

- Os atalhos do jogo ignoram modificadores: `Ctrl+Shift+F9` e `Ctrl+Alt+F9` também abrem o Research (F9). **confirmado**
- `Ctrl+Alt` com `+`, `-` e `0` não conflita com nada que tenhamos visto.

### Mensagem na tela

`ScaledRenderer.SetBriefMessage(string, float duração)` mostra uma mensagem curta do próprio jogo. Para pegar o renderer: `(Galaxy.Game as DWGame).Renderer as ScaledRenderer`. **confirmado**

## Esquemas de cor do jogo

- A opção fica em `GameSettings.ColorScheme` (`int`) e é aplicada por `ColorHelper.SetColorScheme(int)`. Ela é chamada:
  - na inicialização: `ScaledRenderer.Initialize ← DWGame.UpdateGameSettings ← DWGame.BeginRun`;
  - no OK da tela de opções: `ScaledRenderer.GameSettingsDialogOk`.
- Na hora dessa chamada, `DWSettings.ColorScheme` ainda tem o valor anterior. Use o argumento, não a configuração. **confirmado**
- O número é a posição no menu: **confirmado**

| # | Nome no jogo | Chave no GameText | Dump |
|---|---|---|---|
| 0 | Eclipse (padrão) | Color Scheme Gold | `reference\scheme-0-eclipse.json` |
| 1 | Kryptonite | Color Scheme Green | `reference\scheme-1-kryptonite.json` |
| 2 | Molten Lava | Color Scheme Orange | `reference\scheme-2-molten-lava.json` |
| 3 | Crimson Constellation | Color Scheme Red | `reference\scheme-3-crimson-constellation.json` |
| 4 | Aquamarine | Color Scheme Aqua Blue | `reference\scheme-4-aquamarine.json` |
| 5 | Naval Blue | Color Scheme Electric Blue | `reference\scheme-5-naval-blue.json` |
| 6 | Colorblind Assist | Color Scheme Color Blind | `reference\scheme-6-colorblind-assist.json` |

- O `ColorHelper` tem 186 campos `public static Color`. Só 35 mudam entre os esquemas:
  - **24 campos de UI** (esquemas 0 a 5):
    - `DialogBackColor`, `MainViewHeaderBackColor`;
    - `BackColorLightTransparent(/Less/More)`: mesmo RGB, alpha F0/E0/F8;
    - `HeaderBackColor`, `HeaderForeColor`, `HeaderDecorationColor`;
    - `AccentColor`, `AccentColorLight/Mid/Dark/VeryDark`, `AccentColorMainViewBright`;
    - `ButtonColorToggle(/Hover)`, `ButtonColorText(/Hover/Disabled)`, `ButtonColorDropDownSelector(/Hover)`;
    - `PopupListCurrentItemColor`, `PopupListCurrentItemTextColor`, `PopupListHighlightColor`.
  - **11 campos de significado**, só no Colorblind (que usa a UI do Eclipse):
    - `Positive/Negative(Dark)Color`, `WarningColor`, `WarningIntenseColor`;
    - `ComponentStatusNormal/DestroyedColor`, `ComponentBayColorDefense/Sensor/Weapon`.
- **Regra do destaque, em todos os esquemas:** `AccentColorMid` = `AccentColor` × 0,7, `Dark` × 0,5, `MidDark` × 0,25 e `VeryDark` × 0,125, por canal RGB. O resto foi escolhido à mão. **medido**
- O `SetColorScheme` do jogo só redefine os 24 campos do esquema. Quem escreve em outros campos precisa restaurá-los por conta própria. **confirmado** (era um bug nosso)
- Cerca de metade dos 186 campos é cor de UI; o resto tem significado no jogo (energia, recursos, ícones, avisos, gráficos, status) e deve ficar fora dos temas.

## Como a interface lê as cores

Há quatro fontes, e cada uma se comporta de um jeito ao trocar o tema com o jogo aberto:

| Fonte | Exemplos | Na troca ao vivo |
|---|---|---|
| Campos estáticos do `ColorHelper` | `AccentColor`, `BackColorLightTransparent`, `OverlayTintColor` | Lidos a cada desenho: mudam na hora. **confirmado** |
| Campos estáticos de outras classes | `UserInterfaceController.HoverTipBackColorDefault`, `ModalOverlayColor`, `EmpireMessage.MessageColor*` | Lidos no desenho. **confirmado** para o tooltip |
| Cópias por controle | `DWButton.ButtonColorToggle…`, `DWControl.ListSelectionColor…` | Copiados do `ColorHelper` quando o controle é criado; os que já existem **não mudam**. **confirmado**. O mod contorna com `LiveRefresh` (**confirmado** na 0.3.0) |
| Cores próprias de um controle | `SelectionPanel.SectionBackColor`, `HotspotBackColor` | Não vêm do `ColorHelper`. Só mudam com override no getter `...Resolved` |

### Controles que guardam cópia (lidas por propriedades `...Resolved`)

- **`DWButton`:**
  - `ButtonColor`, `ButtonColorHover`, `ButtonColorDisabled`, `ButtonColorToggle`, `ButtonColorToggleHover`;
  - `ForeColorHover`, `DropDownSelectorBackgroundColor(Hover)`, `DropDownSelectorArrowColor(Hover)`;
  - `ImageTintColor(Hover/Disabled)`, `ImageTintColor2(...)`, `ButtonColorGradient`, `OscillateButtonColor`.
- **`DWControl`:** `ForeColor`, `DarkColor`, `ShadowColor`, `BackColor`, `ListSelectionColor`, `BorderColor`.
- Abrir o jogo já com o tema certo resolve tudo isso. **confirmado** O problema é só a troca com o jogo aberto.

### Tooltips

- **`HoverTip`:** nasce com as cores zeradas. Quem decide no desenho são os padrões estáticos `UserInterfaceController.HoverTip{Back,Title,Text,Shadow}ColorDefault`. Valores do jogo: fundo `(32,32,32,248)`, título e texto `(212,212,212)`, sombra preta.
- **Transparência extra:** `DWControl.DrawHoverTip` desenha o tooltip com a opacidade do controle dono. Com fundo `#161616F8` a tela mostrou `#0F0F0F`, com o texto de trás vazando. **medido**
  - A opção `opaqueTooltips` põe `Opacity = 1` durante o desenho; no Basalt ficou certo. **confirmado**
- **Janela do objeto selecionado:** os tooltips dela não passam por `DrawHoverTip`. O fundo é `SelectionPanel.HotspotBackColor` (`#404040E8`). **confirmado**

## Mapa da tela: elemento → cor

| Elemento | Campo(s) | Fonte | Confiança |
|---|---|---|---|
| Fundo dos painéis | `BackColorLightTransparent` | ColorHelper | provável (ficou verde no teste) |
| Degradê nas laterais dos painéis | `OverlayTintColor` (alpha 0 = sem degradê) | ColorHelper | confirmado (Probe C) |
| Título de painel e janela | `HeaderBackColor`, `HeaderForeColor`; listras em `HeaderDecorationColor` | ColorHelper | confirmado (teste) |
| Bordas de painel e botão | `BorderColor` | ColorHelper | confirmado (teste) |
| Barras de seção ("EXPLORATION SHIPS") | família do destaque (provável `AccentColorMid`) | ColorHelper | confirmado família / provável campo |
| Barras de progresso e de skill | família do destaque (provável `AccentColor`) | ColorHelper | confirmado família |
| Barras SKILLS/TRAITS das janelas | fundo da família Back/seção, listras da família Header, texto da família Accent | ColorHelper | confirmado famílias |
| Barra de recursos no topo | família Back (provável `MainViewHeaderBackColor`) | ColorHelper | confirmado família |
| Barra de play / tempo | provável `AccentColorMainViewBright` (`#F89038` no Eclipse) | ColorHelper | provável |
| Botão normal / hover / desativado | `ButtonColor`, `ButtonColorHover`, `ButtonColorDisabled` | cópia em DWButton | confirmado família |
| Texto de botão | `ButtonColorText(/Hover/Disabled)` | ColorHelper / cópia | confirmado família |
| Ícones dos botões | `ButtonImageTintColor*` | cópia em DWButton | confirmado (ícones laranja no teste) |
| Botão ligado, aba ativa do topo | `ButtonColorToggle`, `ButtonColorToggleHover` | cópia em DWButton | confirmado |
| Item selecionado nas listas | `ButtonColorToggle` (via `ListSelectionColor`) | cópia em DWControl | confirmado |
| Botões de "modo ativo" (barra inferior direita) | degradê `AccentColorMid` → `AccentColorDark` | ColorHelper | medido |
| Linha de lista em hover | `ButtonColorBright` (com brilho em cima) | ColorHelper | confirmado (0.3.0) |
| Setinha dos dropdowns | `ButtonColorDropDownSelector(/Hover)` | ColorHelper / cópia | provável |
| Lista de dropdown aberta | `PopupListBackColor/BorderColor/CurrentItemColor/CurrentItemTextColor/HighlightColor` | ColorHelper | provável |
| Caixa de info (planeta, estaleiro) | `HoverInfoBackColor`, `HoverInfoBorderColor` | ColorHelper | confirmado (teste) |
| Tooltips de texto | `UserInterfaceController.HoverTipBackColorDefault` (+ Title/Text/Shadow) | estático | confirmado |
| Rótulo "FILTER" | uma cor da família Back usada como texto | ColorHelper | confirmado família |
| Ícones de automação ao lado dos itens | família Back | ColorHelper | confirmado família |
| Janela do objeto selecionado: faixas alternadas | `SelectionPanel.SectionBackColor` (`#18181880`) | própria | confirmado (Probe E) |
| Janela do objeto selecionado: tooltips | `SelectionPanel.HotspotBackColor` (`#404040E8`) | própria | confirmado |
| Janela do objeto selecionado: texto / sombra do texto | `SelectionPanel.ForeColor` / `ShadowColor` (DWControl) | cópia | confirmado (Probe F) |
| Janela do objeto selecionado: degradê lateral | a mesma textura `1024×2` do degradê dos outros painéis, mas pintada com **branco fixo** (os outros usam `OverlayTintColor`). Por isso nenhuma sonda pegou. O mod pinta essa textura com o `OverlayTintColor` do tema enquanto a `SelectionPanel` desenha (`Render` e `RenderToTexture`) | fixo | confirmado (levantamento + jogo, 2026-11-25) |
| Divisórias das abas de seção (topo esquerdo) | `EmpireSectionList.ForeColor`: texturas 16×90 e 2×90 desenhadas pelo contêiner | cópia de `ButtonColorText` (`#CFCFCF` no Basalt) | confirmado (levantamento + tema de teste, 2026-11-25) |
| Divisórias dos botões do jogo (topo direito: Galactopedia, Menu, Vitória, Tela limpa) | `AngledButtonStrip.ForeColor`, mesmas texturas | cópia de `ButtonColorText` | confirmado |
| Divisórias dos botões do mapa (baixo direito) | `MapOverlayButtons.DarkColor`, mesmas texturas | cópia fixa `#606060` | confirmado (levantamento) |
| Contorno das sub-abas de uma seção (Exploration, Military…) | `AccentColor` lido na hora, linhas e preenchimentos de 1×1 desenhados pela `SectionHeaderPanel`; a borda das próprias abas é `#161616E5` (cópia de `HoverInfoBorderColor`) | ColorHelper | confirmado (`ButtonColorToggleHover` deslocado não mudou o contorno). O mod troca com `SectionHeaderPanel.OutlineColor` |
| Linha embaixo do título de um painel de seção (RESOURCES…) | `ForeColorDark` lido na hora, linhas de 1×1 desenhadas pela `SectionHeaderPanel` (4 `DrawLine` e 1 preenchimento por quadro) | ColorHelper | levantamento (2026-11-25). O mod troca com `SectionHeaderPanel.TitleLineColor` |
| Moldura e grade das tabelas em caixa (resumo embaixo do título de seção, caixas da janela de relação com império) | `#C0C0C0` fixo a 40% (sai `#C0C0C060` com o fade): `DrawBorder` com texturas 2×44, 44×44 e 44×596, mais linhas e preenchimentos de 1×1. Desenhada pela `SectionHeaderPanel` e pelo `ScrollablePanel` da `EmpireRelationPanel`, então é um helper comum | fixo | levantamento (2026-11-25). O mod troca com `TableFrameColor`, em qualquer lugar: só peças nessa cor, com alfa até 40% e nessas texturas |
| Painel do sistema embaixo ao centro | desconhecido | — | não segue os testes |
| Moldura de seleção, ícone "A", barra de 63% no mapa | não são cores da UI | — | não segue os testes |

Lidos pela `SelectionPanel` mas sem efeito visível: `BackColor` (recebe o fundo do tema), `DarkColor` (`#606060`). Nunca lidos: `HighlightColor`, `GraphBackColor`, `BorderColor`.

## Sondas

Temas em `UIThemes\probes\`, que não vão no pacote. Cada uma pinta campos suspeitos com cores fortes.

| Sonda | Campos | Resultado |
|---|---|---|
| A | `LightColor`, `MidColor`, `DarkColor`, `SectionHeaderBackgroundColor`, `BackColorFaded`, `TextBackgroundLegibilityColor`, `ShadowColor`, `BackColorDarkHalfTransparent` | nada relevante relatado |
| B | `TintColorTransparent*`, `TintColorFaded*`, `HalfTransparentTint`, `QuarterTransparentTint` | nada relevante relatado |
| C | `MidDarkTint`, `DarkTint`, `HoverBackTintColor`, `OverlayTintColor`, `BackColorDark*` | `OverlayTintColor` = degradê dos painéis |
| D | fundos de painéis específicos (`SelectionPanelTextLegibilityBackColor`, `RaceSummaryBackColor`…) | nada na janela do objeto selecionado |
| E | `SelectionPanel.SectionBackColor/HotspotBackColor/HighlightColor/GraphBackColor` | faixas alternadas e tooltips da janela |
| F | `SelectionPanel.BackColor/ShadowColor/DarkColor/BorderColor/ForeColor` | texto e sombra do texto |

## Levantamento de bordas (`borders.flag`)

- **O que mostrou (2026-11-25):**
  - Os botões de cima são `DWButton` comuns, mas a moldura clara não é borda deles: são as divisórias que o contêiner desenha com uma cor copiada na criação (ver o mapa).
  - As cores de teste (`toggle.flag`) **não servem** para achar cores copiadas: elas mudam só o `ColorHelper`, e as cópias ficam como estavam. Um tema de teste com um campo deslocado em um passo (`#5076B4` → `#5177B5`) é que separa dois campos de mesmo valor.
  - `UserInterfaceController.StrideControls` está vazio: a interface do jogo não usa elementos de UI do Stride.
  - Os `ToggleButtonGroup` guardam cópias próprias (`ButtonColorToggled`, `ButtonColorHoveredToggled`, `ButtonBorderColor`…), feitas na criação.
- **Detalhes técnicos:** no Stride 4.2 do jogo, todo `SpriteBatch.Draw` termina no interno `DrawSprite`, com a cor em `Color4`. Os controles desenham por `DWControl.OnRender`, e alguns botões por `DWButton.Render` direto.
- **Fade dos painéis:** os painéis aplicam a própria opacidade às cores (o contorno saiu `#5076B4F2`, não `FF`). Por isso o mod compara só o RGB.

## Compatibilidade com outros mods

- **Settings Keeper** (`3824847512`): põe 9 postfixes em `ColorHelper.SetColorScheme` (o launcher classifica como `observes`, mas isso não pega escrita em campos estáticos). Carregado depois do UI Themes, ele desfazia parte do tema a cada troca pelo atalho (`ButtonColorToggle` ficou `#434343C5`); os botões de baixo à direita ganharam moldura, e um resize (Window Fit) ou o OK das opções escondia o problema ao recriá-los. Correção: o postfix do UI Themes roda com `Priority.Last`. **confirmado** (troca de temas pelo atalho sem perder cores, 2026-11-25)
- **Music Box** (`3828075248`), **Positronic Matrix** (`3833406787`): sem conflito visto.

## Outros detalhes

- O `HoverTip` tem cores por instância (`TitleColor`, `TextColor`, `ShadowColor`, `BackColor`), mas elas ficam zeradas. Um postfix no construtor não muda nada.
- `UserInterfaceController` tem outras cores estáticas: `ModalOverlayColor`. Também tem `PreviewWindowOpacity` (0,8) e `PreviewWindowHoverOpacity` (0,9).
- `EmpireMessage` tem `MessageColorDefault/UnderAttack/Military/Construction/Exploration/Research/AdvisorSuggestion/News`.
- Cores do mapa e de efeitos ficam em `LocationEffectGroupDefinition.*Color` e `VictoryCondition.*Color`, fora do escopo dos temas.
