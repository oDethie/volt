<p align="center"><img src="docs/icone.png" width="96" alt="Volt"></p>

# Volt

**A bateria dos seus aparelhos sem fio (mouse, fone, teclado, controle) direto na barra de tarefas do Windows**, com um painel bonito no estilo do Windows 11, previsão de duração, gráfico, saúde da bateria e avisos. Leve: fica parado entre uma leitura e outra.

## Recursos

- **Na barra de tarefas, ao lado do relógio:** o ícone do aparelho e a bateria (ex.: mouse 52%), em todos os monitores. Aparecem só os aparelhos que você escolher e os que estão com a bateria acabando.
- **Passe o mouse** num aparelho da barra para ver um cartão com nome, bateria, previsão e de onde vem.
- **Painel em lista com barras:** um cartão por aparelho com %, barra colorida (verde ok, amarelo atenção, vermelho fraco, azul carregando), origem e **previsão** ("dura ~3 dias", "cheio em ~40 min"). O que precisa de atenção sobe para o topo.
- **Clique num aparelho** para ver o **gráfico** das últimas horas/dias e a **saúde da bateria** (se ela está durando menos que no começo).
- **Avisos:** bateria fraca (limite padrão e por aparelho), **aviso inteligente** quando o aparelho não deve durar as próximas 12 horas, e **carga completa**. Durante jogos e vídeos em tela cheia eles esperam.
- **Galaxy Buds:** fone esquerdo, direito e estojo separados.
- O Volt em si fica guardado no **^** perto do relógio.
- **Instala, atualiza e desinstala sozinho:** abra o `Volt.exe` baixado e ele se instala (sem administrador). Aparece no menu Iniciar e em *Configurações ▸ Aplicativos*.
- **Atualiza sozinho** (desligável), conferindo a **assinatura digital** de cada versão antes de instalar.
- Tema claro/escuro igual ao Windows, renomear e esconder aparelhos, iniciar com o Windows.

## Aparelhos suportados

| Tipo | Como lê |
|---|---|
| **Bluetooth** (fones, mouses, teclados, controles) | A mesma bateria de *Configurações ▸ Bluetooth e dispositivos* |
| **Samsung Galaxy Buds** | Esquerdo, direito e estojo, pela mensagem de estado que os fones enviam (o app Galaxy Buds precisa estar fechado) |
| **Logitech** (receptores Unifying, Bolt, Lightspeed; cabo e Bluetooth; fones G) | Protocolo HID++ da Logitech |
| **Controles Xbox** | % de verdade pelo Windows (Windows.Gaming.Input); nível aproximado se não houver |
| **Controles PlayStation** (DualSense, DualSense Edge, DualShock 4), USB ou Bluetooth | Bateria do relatório do próprio controle |
| **Celular Android** | Pelo app *Vincular ao Celular* do Windows (lê o cartão do celular que o Windows guarda) |
| **Aparelhos USB que seguem o padrão HID de bateria** | Uso "Battery Strength" do padrão HID |

O Volt **só faz perguntas de leitura** aos aparelhos: nunca grava nada neles. `Volt.exe --diagnostico arquivo.txt` lista o que o Volt enxerga no seu PC (útil para pedir suporte a um aparelho novo).

## Instalar

1. Baixe o **`Volt.exe`** na página de [Releases](../../releases/latest).
2. Abra. Ele pergunta se quer instalar: clique em **Instalar**. Pronto: ele fica em `%LOCALAPPDATA%\Programs\Volt`, abre com o Windows e aparece no menu Iniciar.

> **SmartScreen:** como o programa não tem assinatura paga, o Windows pode mostrar *"O Windows protegeu o computador"*. Clique em **Mais informações ▸ Executar assim mesmo**.

Para desinstalar: *Configurações ▸ Aplicativos ▸ Volt ▸ Desinstalar* (ou nas configurações do próprio Volt).

Requisitos: Windows 10 (2004+) ou 11. O .NET Framework 4.8 já vem no Windows.

## Privacidade

- Fica no seu PC: configurações em `%APPDATA%\Volt.ini`, histórico em `%APPDATA%\Volt\historico.txt` (14 dias) e ciclos de carga em `%APPDATA%\Volt\ciclos.txt`.
- A única conexão com a internet é a consulta de atualização ao GitHub (a cada 12 h, desligável), que não envia nada sobre você.

## Consumo

Medido em uso real: em repouso ~1 MB de RAM em uso (chega a ~17 MB por um instante durante a leitura, a cada 2 min) e ~0,04% de um núcleo em média. Prioridade baixa, nada por cima de jogos.

## Linha de comando

| Comando | O que faz |
|---|---|
| `Volt.exe --listar arquivo.txt` | Lê todos os aparelhos e grava a bateria no arquivo |
| `Volt.exe --diagnostico arquivo.txt` | Lista tudo o que o Volt enxerga (para pedir suporte a um aparelho) |
| `Volt.exe --desinstalar` | Desinstala o Volt |

## Compilar

Não precisa instalar nada: `build.bat` usa o compilador C# que já vem no Windows.

## Licença

MIT. O Volt foi escrito do zero; os protocolos vêm de documentação pública (Solaar para a Logitech, GalaxyBudsClient para os Galaxy Buds).
