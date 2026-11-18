<p align="center"><img src="docs/icone.png" width="96" alt="Volt"></p>

# Volt

**A bateria dos seus aparelhos sem fio (mouse, fone, teclado, controle) na bandeja do Windows**, num painel bonito no estilo do Windows 11, com previsão de duração, gráfico e avisos. Leve: fica parado entre uma leitura e outra.

## Recursos

- **Ícone com anel** perto do relógio, mostrando o aparelho mais fraco. Passou o mouse, lista todos.
- **Painel em lista com barras**: um cartão por aparelho com %, barra colorida (verde ok, amarelo atenção, vermelho fraco, azul carregando), de onde vem (Bluetooth, receptor) e a **previsão** ("dura ~3 dias", "cheio em ~40 min").
- O que **precisa de atenção sobe para o topo**, com uma barrinha lateral colorida.
- **Clique num aparelho** para abrir o **gráfico** das últimas horas/dias.
- **Avisos** de bateria fraca (limite padrão e por aparelho) e de **carga completa**. Durante jogos e vídeos em tela cheia eles esperam e aparecem quando você sai.
- Opcional: **a % de cada aparelho na barra** (um ícone com o número para cada um).
- Renomear, esconder ou esquecer aparelhos; tema claro/escuro igual ao Windows; iniciar com o Windows.
- **Atualiza sozinho** (desligável), conferindo a **assinatura digital** de cada versão antes de instalar.

## Aparelhos suportados

| Tipo | Como lê |
|---|---|
| **Bluetooth** (fones, mouses, teclados, controles) | A mesma bateria que aparece em *Configurações ▸ Bluetooth e dispositivos* |
| **Logitech** (receptores Unifying, Bolt, Lightspeed; cabo e Bluetooth; fones G) | Protocolo HID++ da Logitech (só leitura) |
| **Controles Xbox** | XInput (o Windows informa o nível aproximado) |
| **Aparelhos USB que seguem o padrão HID de bateria** | Uso "Battery Strength" do padrão HID |

O Volt **só faz perguntas de leitura** aos aparelhos: nunca grava nada neles. Outras marcas (que usam protocolos próprios) podem ser adicionadas depois; `Volt.exe --diagnostico arquivo.txt` lista o que o Volt enxerga no seu PC.

## Instalar

1. Baixe o **`Volt.exe`** na página de [Releases](../../releases/latest).
2. Coloque numa pasta fixa, por exemplo `%LOCALAPPDATA%\Programs\Volt\`.
3. Dê dois cliques. O ícone aparece perto do relógio e o Volt passa a iniciar com o Windows.

Requisitos: Windows 10 (2004+) ou 11. O .NET Framework 4.8 já vem no Windows.

## Privacidade

- Fica no seu PC: configurações em `%APPDATA%\Volt.ini` e histórico em `%APPDATA%\Volt\historico.txt` (14 dias).
- A única conexão com a internet é a consulta de atualização ao GitHub (a cada 12 h, desligável), que não envia nada sobre você.

## Consumo

Medido em uso real: em repouso ~1 MB de RAM em uso (chega a ~17 MB por um instante durante a leitura, a cada 2 min) e ~0,04% de um núcleo em média. Programa de ~130 KB, prioridade baixa, sem nada por cima de jogos.

## Linha de comando

| Comando | O que faz |
|---|---|
| `Volt.exe --listar arquivo.txt` | Lê todos os aparelhos e grava a bateria no arquivo |
| `Volt.exe --diagnostico arquivo.txt` | Lista tudo o que o Volt enxerga (para pedir suporte a um aparelho) |

## Compilar

Não precisa instalar nada: `build.bat` usa o compilador C# que já vem no Windows.

## Licença

MIT. O Volt foi escrito do zero; os protocolos vêm de documentação pública (como a do projeto Solaar para a Logitech).
