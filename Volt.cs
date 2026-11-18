// Volt - bateria dos aparelhos sem fio na bandeja do Windows
// Compilar: build.bat (usa o csc do .NET Framework 4.x que ja vem no Windows)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Volt")]
[assembly: AssemblyProduct("Volt")]
[assembly: AssemblyDescription("Bateria dos aparelhos sem fio na bandeja do Windows")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    public static uint MsgAbrir;
    public static bool Atualizado;

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }
        catch { try { SetProcessDPIAware(); } catch { } }

        if (args.Length == 2 && args[0] == "--listar") { Listar(args[1]); return; }
        if (args.Length == 2 && args[0] == "--diagnostico") { Diagnostico(args[1]); return; }
        if (args.Length == 2 && args[0] == "--ico") { SalvarIco(args[1]); return; }
        if (args.Length == 2 && args[0] == "--logo") { using (var b = Desenho.Logo(256)) b.Save(args[1]); return; }
        if (args.Length == 2 && args[0] == "--assinar") { File.WriteAllText(args[1] + ".pub.txt", Atualizador.Assinar(args[1])); return; }
        if (args.Length == 2 && args[0] == "--testeatualizar") { File.WriteAllLines(args[1], Atualizador.Teste()); return; }
        if (args.Length == 2 && args[0] == "--icones") { Volt.TesteIcones(args[1]); return; }
        if (args.Length == 2 && args[0] == "--testefaixa") { TesteFaixa(args[1]); return; }
        if (args.Length == 2 && args[0] == "--testebuds") { File.WriteAllLines(args[1], GalaxyBuds.Teste()); return; }
        if (args.Length == 2 && args[0] == "--testedica")
        {
            float e = 1.75f;
            var ap = new Estado { Tipo = Tipo.Fone, Pct = 100, Via = "Bluetooth", Visto = DateTime.Now.AddMinutes(-1), Partes = new[] { 100, 96, 18 } };
            using (var b = new Bitmap((int)(290 * e), (int)(156 * e)))
            using (var g = Graphics.FromImage(b)) { Dica.Desenhar(g, b.Size, ap, "Buds FE de Thiago", 20, "dura ~5 h", true, e); b.Save(args[1]); }
            return;
        }
        if (args.Length >= 1 && args[0] == "--atualizado") Atualizado = true;
        if (args.Length == 1 && args[0] == "--desinstalar") { Application.EnableVisualStyles(); Instalador.Desinstalar(TemaEscuroSistema()); return; }
        // aberto de fora da pasta de programas (ex.: Downloads): oferece instalar
        if (args.Length == 0 && !Instalador.EstaInstalado)
        {
            Application.EnableVisualStyles();
            if (!Instalador.Oferecer(TemaEscuroSistema())) return;
        }

        MsgAbrir = RegisterWindowMessage("Volt_AbrirPainel");
        bool novo;
        using (var m = new Mutex(true, "Volt_Unico", out novo))
        {
            if (!novo && Atualizado)
            {
                try { novo = m.WaitOne(20000); } catch (AbandonedMutexException) { novo = true; }
            }
            if (!novo)
            {
                AllowSetForegroundWindow(-1);
                PostMessage(new IntPtr(0xFFFF), MsgAbrir, IntPtr.Zero, IntPtr.Zero);
                return;
            }
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Registro.Erro(e.Exception);
            Application.Run(new App());
        }
    }

    static bool TemaEscuroSistema()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            { object v = k == null ? null : k.GetValue("AppsUseLightTheme"); if (v is int) return (int)v == 0; }
        }
        catch { }
        return false;
    }

    static void Listar(string arq)
    {
        var l = new List<string>();
        var t = Stopwatch.StartNew();
        foreach (var a in Leitor.LerTudo())
            l.Add(a.Nome + " | " + a.Via + " | " + a.Tipo + " | " + a.Pct + "%" + (a.Carregando ? " carregando" : "") + (a.Aproximado ? " (aprox.)" : "") + (a.Detalhe != null ? " | " + a.Detalhe : "") + " | " + a.Chave);
        l.Add("leitura: " + t.ElapsedMilliseconds + " ms");
        File.WriteAllLines(arq, l);
    }

    // tudo o que o Volt enxerga (para pedir suporte a um aparelho novo); nao muda nada
    static void Diagnostico(string arq)
    {
        var l = new List<string>();
        foreach (var c in Hid.Listar())
            l.Add(string.Format("HID {0:X4}:{1:X4} pagina {2:X4} uso {3:X4} entrada {4} saida {5} recurso {6} '{7}'", c.Vid, c.Pid, c.Pagina, c.Uso, c.TamEntrada, c.TamSaida, c.TamRecurso, c.Produto));
        var ap = new List<Aparelho>();
        Bluetooth.Ler(ap);
        l.AddRange(Bluetooth.Diagnostico);
        foreach (var a in Leitor.LerTudo()) l.Add("LIDO " + a.Nome + " | " + a.Via + " | " + a.Pct + "%" + (a.Carregando ? " carregando" : ""));
        File.WriteAllLines(arq, l);
    }

    // previa da faixa da barra (fundo escuro e claro, 175%)
    static void TesteFaixa(string arq)
    {
        var l = new List<Estado> {
            new Estado { Tipo = Tipo.Mouse, Pct = 52 },
            new Estado { Tipo = Tipo.Fone, Pct = 12 },
            new Estado { Tipo = Tipo.Controle, Pct = 80, Carregando = true } };
        float e = 1.75f; int h = 84;
        using (var esc = Barra.Renderizar(l, false, e, h, x => 20))
        using (var cla = Barra.Renderizar(l, true, e, h, x => 20))
        using (var b = new Bitmap(Math.Max(esc.Width, cla.Width) + 40, h * 2))
        using (var g = Graphics.FromImage(b))
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(28, 28, 28)), 0, 0, b.Width, h);
            g.FillRectangle(new SolidBrush(Color.FromArgb(238, 238, 238)), 0, h, b.Width, h);
            g.DrawImage(esc, 20, 0); g.DrawImage(cla, 20, h);
            b.Save(arq);
        }
    }

    static void SalvarIco(string caminho)
    {
        int[] tams = { 16, 20, 24, 32, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (var t in tams)
            using (var bmp = Desenho.Logo(t))
            using (var ms = new MemoryStream()) { bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (var w = new BinaryWriter(File.Create(caminho)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)tams.Length);
            int ofs = 6 + 16 * tams.Length;
            for (int i = 0; i < tams.Length; i++)
            {
                w.Write((byte)(tams[i] >= 256 ? 0 : tams[i])); w.Write((byte)(tams[i] >= 256 ? 0 : tams[i]));
                w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(ofs); ofs += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
    }
}

// =====================================================================
//  Estado de cada aparelho (o que foi lido + o que a pessoa escolheu)
// =====================================================================
class Estado
{
    public string Chave, NomeOrig, Nome, Via;     // Nome: escolhido pela pessoa (null = original)
    public Tipo Tipo;
    public int Pct = -1;
    public bool Carregando, Aproximado, Presente, Dormindo, Oculto;
    public bool NaBarra;                           // sempre mostrar na barra de tarefas
    public int Limite = -1;                        // -1 = limite padrao
    public DateTime Visto;
    public bool AvisouFraca, AvisouCritica, AvisouCheio, AvisouPrevisao;
    public string Detalhe;
    public int[] Partes;                           // fones: esquerdo, direito, estojo (-1 = sem informacao)
    public int Prazo = 0;                          // segundos para manter o ultimo valor se sumir (receptor: aparelho dormindo)
}

static class Volt
{
    public static char Glifo(Tipo t)
    {
        switch (t)
        {
            case Tipo.Mouse: return '';
            case Tipo.Fone: return '';
            case Tipo.Teclado: return '';
            case Tipo.Controle: return '';
            default: return '';
        }
    }

    // fones: tres mini barras lado a lado (Esquerdo, Direito, Estojo), no estilo da barra principal
    public static void Partes(Graphics g, Rectangle r, int[] p, Font f, Color texto, Color fraco, Color trilho, bool escuro, int limite, float esc)
    {
        string[] nomes = { "Esquerdo", "Direito", "Estojo" };
        int gap = (int)Math.Round(14 * esc), cw = (r.Width - 2 * gap) / 3, bh = Math.Max(2, (int)Math.Round(4 * esc));
        var tf = System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPadding | System.Windows.Forms.TextFormatFlags.SingleLine;
        var td = System.Windows.Forms.TextFormatFlags.Right | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPadding | System.Windows.Forms.TextFormatFlags.SingleLine;
        // pouco espaco: os tres com rotulo curto (nunca misturado)
        bool curto = false;
        for (int i = 0; i < 3; i++)
        {
            int v = i < p.Length ? p[i] : -1;
            int w = System.Windows.Forms.TextRenderer.MeasureText(g, nomes[i], f, Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding).Width +
                    System.Windows.Forms.TextRenderer.MeasureText(g, v < 0 ? "–" : v + "%", f, Size.Empty, System.Windows.Forms.TextFormatFlags.NoPadding).Width;
            if (w + (int)Math.Round(8 * esc) > cw) curto = true;
        }
        for (int i = 0; i < 3; i++)
        {
            int v = i < p.Length ? p[i] : -1;
            var rc = new Rectangle(r.X + i * (cw + gap), r.Y, cw, r.Height);
            var rt = new Rectangle(rc.X, rc.Y, rc.Width, rc.Height - bh - (int)Math.Round(4 * esc));
            Color cor = v >= 0 && v <= limite ? Vermelho(escuro) : v >= 0 && v <= Math.Max(limite + 15, 35) ? Amarelo(escuro) : Verde(escuro);
            string nome = (curto ? new[] { "Esq.", "Dir.", "Estojo" } : nomes)[i];
            System.Windows.Forms.TextRenderer.DrawText(g, nome, f, rt, fraco, tf);
            System.Windows.Forms.TextRenderer.DrawText(g, v < 0 ? "–" : v + "%", f, rt, v >= 0 && v <= limite ? cor : texto, td);
            var rb = new Rectangle(rc.X, rc.Bottom - bh, rc.Width, bh);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pth = Desenho.Arred(rb, bh / 2f)) using (var b = new SolidBrush(trilho)) g.FillPath(b, pth);
            if (v > 0) using (var pth = Desenho.Arred(new RectangleF(rb.X, rb.Y, Math.Max(bh, rb.Width * v / 100f), rb.Height), bh / 2f)) using (var b = new SolidBrush(cor)) g.FillPath(b, pth);
            g.SmoothingMode = SmoothingMode.None;
        }
    }

    public static string Ha(DateTime t)
    {
        if (t == DateTime.MinValue) return "";
        var d = DateTime.Now - t;
        if (d.TotalSeconds < 60) return "agora";
        if (d.TotalMinutes < 60) return "há " + (int)d.TotalMinutes + " min";
        if (d.TotalHours < 24) return "há " + (int)d.TotalHours + " h";
        return "há " + (int)d.TotalDays + (d.TotalDays < 2 ? " dia" : " dias");
    }

    public static string Duracao(double horas)
    {
        if (horas < 1) return "~" + Math.Max(5, (int)Math.Round(horas * 60 / 5) * 5) + " min";
        if (horas < 10) { int h = (int)horas, m = (int)Math.Round((horas - h) * 60 / 10) * 10; if (m == 60) { h++; m = 0; } return "~" + h + " h" + (m > 0 ? " " + m : ""); }
        if (horas < 48) return "~" + (int)Math.Round(horas) + " h";
        return "~" + (int)Math.Round(horas / 24) + " dias";
    }

    // cores dos estados (iguais no claro e no escuro, so ajustadas)
    public static Color Verde(bool escuro) { return escuro ? Color.FromArgb(93, 202, 165) : Color.FromArgb(15, 138, 96); }
    public static Color Amarelo(bool escuro) { return escuro ? Color.FromArgb(239, 159, 39) : Color.FromArgb(196, 120, 0); }
    public static Color Vermelho(bool escuro) { return escuro ? Color.FromArgb(240, 100, 90) : Color.FromArgb(196, 43, 28); }
    public static Color Azul(bool escuro) { return escuro ? Color.FromArgb(96, 180, 255) : Color.FromArgb(0, 103, 192); }

    public static int Nivel(Estado a, int limite)   // 0 ok, 1 atencao, 2 fraco, 3 carregando
    {
        if (a.Carregando) return 3;
        if (a.Pct < 0) return 0;
        if (a.Pct <= limite) return 2;
        if (a.Pct <= Math.Max(limite + 15, 35)) return 1;
        return 0;
    }
    public static Color CorNivel(int nivel, bool escuro)
    {
        return nivel == 3 ? Azul(escuro) : nivel == 2 ? Vermelho(escuro) : nivel == 1 ? Amarelo(escuro) : Verde(escuro);
    }

    // ---------- icones da bandeja ----------
    // icone principal: anel com o nivel do aparelho mais fraco (e o numero, quando cabe)
    public static Bitmap IconeAnel(int n, int pct, Color arco, Color trilho, Color texto)
    {
        var b = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float esp = Math.Max(1.6f, n * 0.13f), m = esp / 2 + n * 0.04f;
            var r = new RectangleF(m, m, n - 2 * m, n - 2 * m);
            using (var p = new Pen(trilho, esp)) g.DrawEllipse(p, r);
            if (pct > 0) using (var p = new Pen(arco, esp) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, r, -90, Math.Max(8, 360f * pct / 100f));
            if (pct < 0)
            {
                using (var gl = Desenho.Glifo('', n, texto, 0.48f)) g.DrawImage(gl, 0, 0);   // raio: nada lido ainda
            }
            else if (n >= 20)
            {
                string t = pct >= 100 ? "99" : pct.ToString();
                using (var f = new Font("Segoe UI Semibold", n * (t.Length > 1 ? 0.36f : 0.42f), GraphicsUnit.Pixel))
                using (var sb = new SolidBrush(texto))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(t, f, sb, new RectangleF(0, n * 0.03f, n, n), fmt);
                }
            }
        }
        return b;
    }

    // icone de um aparelho (modo "numeros na barra"): o numero grande e uma barrinha da cor do nivel
    public static Bitmap IconeNumero(int n, int pct, Color cor, Color texto, bool carregando)
    {
        var b = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            string t = pct < 0 ? "?" : pct >= 100 ? "F" : pct.ToString();
            using (var f = new Font("Segoe UI Semibold", n * (t.Length > 1 ? 0.62f : 0.72f), GraphicsUnit.Pixel))
            using (var sb = new SolidBrush(texto))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(t, f, sb, new RectangleF(-n * 0.1f, -n * 0.08f, n * 1.2f, n * 0.92f), fmt);
            float h = Math.Max(2f, n * 0.14f), y = n - h - n * 0.02f;
            using (var tr = new SolidBrush(Color.FromArgb(70, texto))) g.FillRectangle(tr, n * 0.08f, y, n * 0.84f, h);
            using (var c = new SolidBrush(cor)) g.FillRectangle(c, n * 0.08f, y, n * 0.84f * Math.Max(0.08f, Math.Max(0, pct) / 100f), h);
            if (carregando) using (var gl = Glifo2('', n, cor)) g.DrawImage(gl, n * 0.55f, -n * 0.05f, n * 0.5f, n * 0.5f);
        }
        return b;
    }
    static Bitmap Glifo2(char c, int n, Color cor) { return Desenho.Glifo(c, n, cor, 0.9f); }

    public static void TesteIcones(string arq)
    {
        using (var b = new Bitmap(560, 120))
        using (var g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(32, 32, 32));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            int x = 10;
            foreach (var v in new[] { 93, 52, 30, 12, -1 })
            {
                int nv = v < 0 ? 0 : v <= 20 ? 2 : v <= 35 ? 1 : 0;
                using (var i = IconeAnel(28, v, CorNivel(nv, true), Color.FromArgb(80, 255, 255, 255), Color.White)) { g.DrawImage(i, x, 10, 56, 56); g.DrawImage(i, x + 14, 80, 28, 28); }
                x += 70;
            }
            foreach (var v in new[] { 93, 12 })
            {
                using (var i = IconeNumero(28, v, CorNivel(v <= 20 ? 2 : 0, true), Color.White, v == 93)) { g.DrawImage(i, x, 10, 56, 56); g.DrawImage(i, x + 14, 80, 28, 28); }
                x += 70;
            }
            b.Save(arq);
        }
    }
}

// =====================================================================
//  Historico (para o grafico e a previsao): %APPDATA%\Volt\historico.txt
// =====================================================================
class Ponto { public DateTime T; public int Pct; public bool C; }

static class Historico
{
    static readonly string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Volt");
    static string Arq { get { return Path.Combine(pasta, "historico.txt"); } }
    static readonly Dictionary<string, List<Ponto>> pontos = new Dictionary<string, List<Ponto>>();
    const int Dias = 14;

    public static void Carregar()
    {
        try
        {
            if (!File.Exists(Arq)) return;
            var limite = DateTime.Now.AddDays(-Dias);
            int velhos = 0;
            foreach (var l in File.ReadAllLines(Arq))
            {
                var c = l.Split(';');
                if (c.Length < 4) continue;
                DateTime t;
                if (!DateTime.TryParseExact(c[0], "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out t)) continue;
                if (t < limite) { velhos++; continue; }
                int p; if (!int.TryParse(c[2], out p)) continue;
                Lista(c[1]).Add(new Ponto { T = t, Pct = p, C = c[3] == "1" });
            }
            if (velhos > 200) Regravar();
        }
        catch (Exception ex) { Registro.Erro(ex); }
    }

    static List<Ponto> Lista(string k)
    {
        List<Ponto> l;
        if (!pontos.TryGetValue(k, out l)) pontos[k] = l = new List<Ponto>();
        return l;
    }
    public static List<Ponto> De(string k) { List<Ponto> l; return pontos.TryGetValue(k, out l) ? l : new List<Ponto>(); }

    // guarda quando muda, ou a cada 30 min (para a previsao saber que o tempo passou)
    public static void Anotar(Estado a)
    {
        if (a.Pct < 0) return;
        var l = Lista(a.Chave);
        var u = l.Count > 0 ? l[l.Count - 1] : null;
        if (u != null && u.Pct == a.Pct && u.C == a.Carregando && (DateTime.Now - u.T).TotalMinutes < 30) return;
        var p = new Ponto { T = DateTime.Now, Pct = a.Pct, C = a.Carregando };
        l.Add(p);
        try
        {
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Arq, p.T.ToString("yyyyMMddHHmm") + ";" + a.Chave.Replace(";", "") + ";" + p.Pct + ";" + (p.C ? 1 : 0) + "\r\n");
        }
        catch { }
    }

    public static void Esquecer(string k) { pontos.Remove(k); Regravar(); }

    static void Regravar()
    {
        try
        {
            var linhas = new List<string>();
            foreach (var kv in pontos)
                foreach (var p in kv.Value) linhas.Add(p.T.ToString("yyyyMMddHHmm") + ";" + kv.Key + ";" + p.Pct + ";" + (p.C ? 1 : 0));
            linhas.Sort();
            Directory.CreateDirectory(pasta);
            File.WriteAllLines(Arq, linhas);
        }
        catch { }
    }

    // Previsao: inclinacao dos pontos desde a ultima mudanca (carregando ou descarregando)
    // devolve horas (descarregando: ate acabar; carregando: ate encher), ou -1 se ainda nao da para saber
    public static double Previsao(Estado a)
    {
        if (a.Pct < 0) return -1;
        var l = De(a.Chave);
        var seg = new List<Ponto>();
        for (int i = l.Count - 1; i >= 0; i--)
        {
            if (l[i].C != a.Carregando) break;
            if ((DateTime.Now - l[i].T).TotalDays > (a.Carregando ? 0.25 : 7)) break;
            seg.Insert(0, l[i]);
        }
        if (seg.Count < 2) return -1;
        double horas = (seg[seg.Count - 1].T - seg[0].T).TotalHours;
        int dif = seg[seg.Count - 1].Pct - seg[0].Pct;
        if (a.Carregando)
        {
            if (horas < 0.08 || dif < 2) return -1;
            return (100 - a.Pct) / (dif / horas);
        }
        if (horas < 0.5 || dif > -2) return -1;
        // regressao linear simples (pct x horas)
        double t0 = 0, sx = 0, sy = 0, sxx = 0, sxy = 0; int n = seg.Count;
        foreach (var p in seg)
        {
            double x = (p.T - seg[0].T).TotalHours, y = p.Pct;
            sx += x; sy += y; sxx += x * x; sxy += x * y;
        }
        double den = n * sxx - sx * sx;
        double incl = den > 1e-9 ? (n * sxy - sx * sy) / den : dif / horas;
        if (incl >= -0.05) return -1;
        // o tempo desde a ultima leitura tambem conta
        t0 = (DateTime.Now - seg[seg.Count - 1].T).TotalHours;
        return Math.Max(0, a.Pct / -incl - t0 * 0);
    }
}

// =====================================================================
//  Saude da bateria: cada descarga (do fim de uma carga ate a proxima) vira um "ciclo";
//  comparando os ciclos recentes com os primeiros da para ver se a bateria esta durando menos.
//  %APPDATA%\Volt\ciclos.txt (pequeno, guardado para sempre)
// =====================================================================
class Ciclo { public DateTime T; public double Taxa, Horas; public int Queda; }

static class Ciclos
{
    static readonly string arq = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Volt", "ciclos.txt");
    static readonly Dictionary<string, List<Ciclo>> ciclos = new Dictionary<string, List<Ciclo>>();
    static readonly System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;

    public static void Carregar()
    {
        try
        {
            if (!File.Exists(arq)) return;
            foreach (var l in File.ReadAllLines(arq))
            {
                var c = l.Split(';');
                DateTime t; double taxa, horas; int queda;
                if (c.Length < 5 || !DateTime.TryParseExact(c[0], "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out t)) continue;
                if (!double.TryParse(c[2], System.Globalization.NumberStyles.Float, inv, out taxa) || !double.TryParse(c[3], System.Globalization.NumberStyles.Float, inv, out horas) || !int.TryParse(c[4], out queda)) continue;
                De(c[1]).Add(new Ciclo { T = t, Taxa = taxa, Horas = horas, Queda = queda });
            }
        }
        catch (Exception ex) { Registro.Erro(ex); }
    }

    public static List<Ciclo> De(string k) { List<Ciclo> l; if (!ciclos.TryGetValue(k, out l)) ciclos[k] = l = new List<Ciclo>(); return l; }

    // o aparelho comecou a carregar: mede a descarga desde a ultima carga
    public static void Fechar(Estado a)
    {
        var pts = Historico.De(a.Chave);
        int fim = pts.Count - 1;
        if (fim < 1 || pts[fim].C) return;
        int ini = fim;
        while (ini > 0 && !pts[ini - 1].C) ini--;
        double horas = (pts[fim].T - pts[ini].T).TotalHours;
        int queda = pts[ini].Pct - pts[fim].Pct;
        if (horas < 2 || queda < 10) return;      // descarga curta demais para dizer algo
        var c = new Ciclo { T = DateTime.Now, Taxa = queda / horas, Horas = horas, Queda = queda };
        De(a.Chave).Add(c);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arq));
            File.AppendAllText(arq, c.T.ToString("yyyyMMddHHmm") + ";" + a.Chave.Replace(";", "") + ";" + c.Taxa.ToString("0.###", inv) + ";" +
                c.Horas.ToString("0.##", inv) + ";" + c.Queda + "\r\n");
        }
        catch { }
    }

    // texto curto da saude; "" se ainda nao ha nada
    public static string Saude(Estado a)
    {
        var l = De(a.Chave);
        if (l.Count == 0) return "";
        double media = 0; foreach (var c in l) media += c.Taxa; media /= l.Count;
        string carga = media > 0 ? " · uma carga dura " + Volt.Duracao(100 / media) : "";
        string n = l.Count + (l.Count == 1 ? " carga" : " cargas");
        if (l.Count < 6) return "Saúde: medindo (" + n + ")" + carga;
        double ini = 0, rec = 0;
        for (int i = 0; i < 3; i++) { ini += l[i].Taxa; rec += l[l.Count - 1 - i].Taxa; }
        double dif = rec / ini - 1;    // gastando mais rapido = durando menos
        string s = dif > 0.15 ? "Saúde: dura " + (int)Math.Round(dif / (1 + dif) * 100) + "% menos que no começo" : "Saúde: boa";
        return s + " · " + n + carga;
    }

    public static void Esquecer(string k)
    {
        ciclos.Remove(k);
        try
        {
            var linhas = new List<string>();
            foreach (var kv in ciclos)
                foreach (var c in kv.Value)
                    linhas.Add(c.T.ToString("yyyyMMddHHmm") + ";" + kv.Key + ";" + c.Taxa.ToString("0.###", inv) + ";" + c.Horas.ToString("0.##", inv) + ";" + c.Queda);
            File.WriteAllLines(arq, linhas);
        }
        catch { }
    }
}

// =====================================================================
//  Janela invisivel: avisos do Windows (aparelho plugado, PC acordou, barra recriada, outra copia)
// =====================================================================
class Mensageiro : NativeWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    readonly uint msgBarra = RegisterWindowMessage("TaskbarCreated");
    readonly uint msgSair = RegisterWindowMessage("Volt_Sair");
    public Action Mudou, Acordou, BarraRecriada, Abrir, Tema, Sair;

    public Mensageiro() { CreateHandle(new CreateParams { Caption = "Volt" }); }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0219 /*WM_DEVICECHANGE*/ && (m.WParam.ToInt32() == 0x8000 || m.WParam.ToInt32() == 0x8004 || m.WParam.ToInt32() == 0x0007)) { if (Mudou != null) Mudou(); }
        else if (m.Msg == 0x0218 /*WM_POWERBROADCAST*/ && m.WParam.ToInt32() == 0x12 /*RESUMEAUTOMATIC*/) { if (Acordou != null) Acordou(); }
        else if (m.Msg == msgBarra) { if (BarraRecriada != null) BarraRecriada(); }
        else if (m.Msg == Program.MsgAbrir) { if (Abrir != null) Abrir(); }
        else if (m.Msg == msgSair) { if (Sair != null) Sair(); }
        else if (m.Msg == 0x001A /*WM_SETTINGCHANGE*/) { if (Tema != null) Tema(); }
        base.WndProc(ref m);
    }
}

// =====================================================================
//  App
// =====================================================================
class App : ApplicationContext
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int t, out uint x, out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string classe, string nome);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int i, uint dpi);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, IntPtr min, IntPtr max);
    [DllImport("shell32.dll")] static extern uint SHAppBarMessage(uint msg, ref APPBARDATA d);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }

    // ---- configuracao ----
    public bool MostrarFracos = true, Animacoes = true, MenuLateralAberto = true, AtualizarSozinho = true;
    public bool AvisarFraca = true, AvisarCheio = true, AvisarPrevisao = true;
    public int LimitePadrao = 20, Intervalo = 2;
    string tema = "auto";
    string cfgVersao = "";
    readonly string arqConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Volt.ini");
    readonly Dictionary<string, Estado> aparelhos = new Dictionary<string, Estado>();

    // ---- estado ----
    readonly NotifyIcon icone;
    readonly Dictionary<string, NotifyIcon> numeros = new Dictionary<string, NotifyIcon>();
    readonly Mensageiro msg;
    readonly Painel painel;
    Barra faixa;
    readonly Aviso aviso = new Aviso();
    readonly ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
    readonly Control invocador = new Control();
    readonly AutoResetEvent pedido = new AutoResetEvent(false);
    readonly Timer tDebounce = new Timer();
    readonly Timer tFila = new Timer { Interval = 30000 };
    readonly List<KeyValuePair<char, string>> filaAvisos = new List<KeyValuePair<char, string>>();
    public DateTime UltimaLeitura = DateTime.MinValue;
    public bool Lendo;
    public bool MenuAberto;
    Configuracoes config;
    string chaveIcone;
    bool? inicioCache;
    volatile bool saindo;

    // atualizacao
    public string EstadoAtualizacao = "";
    Atualizador.Nova novaVersao; byte[] novaBin;
    readonly Timer tAtualizacao = new Timer { Interval = 120 * 1000 };
    bool procurandoAtualizacao;

    public App()
    {
        bool primeiraVez = !File.Exists(arqConfig);
        LerConfig();
        Historico.Carregar();
        Ciclos.Carregar();
        invocador.CreateControl();
        var h = invocador.Handle;

        painel = new Painel(this);
        faixa = new Barra(this);
        msg = new Mensageiro();
        msg.Mudou = () => LerEm(3000);
        msg.Acordou = () => LerEm(6000);
        msg.BarraRecriada = () => { icone.Visible = false; icone.Visible = true; foreach (var ni in numeros.Values) { ni.Visible = false; ni.Visible = true; } faixa.Recriar(); };
        msg.Abrir = () => { if (!painel.Visible) painel.Mostrar(); };
        msg.Sair = () => Sair();
        msg.Tema = () => { chaveIcone = null; Postar(AtualizarIcones); };

        // Identidade nova para o icone (o Windows lembra se cada icone fica na barra ou no ^ pela identidade;
        // a da primeira versao tinha ficado fixada na barra). Os registros antigos do Volt sao apagados.
        LimparRegistrosAntigos();
        for (int i = 0; i < 20; i++) new NotifyIcon().Dispose();
        icone = new NotifyIcon { Visible = false, Text = "Volt" };
        icone.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) painel.Alternar(); };
        icone.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) MenuEm(Cursor.Position); };
        menu.Opening += (s, e) => { MenuAberto = true; };
        menu.Closed += (s, e) => { MenuAberto = false; ConferirPainel(); };

        tDebounce.Tick += (s, e) => { tDebounce.Stop(); pedido.Set(); };
        tFila.Tick += (s, e) => MostrarFila();
        AtualizarIcones();
        icone.Visible = true;

        // leitura em segundo plano: parada entre uma leitura e outra (sem gastar CPU)
        new Thread(LoopLeitura) { IsBackground = true, Name = "Volt.Leitura", Priority = ThreadPriority.BelowNormal }.Start();

        // atualizacao automatica
        try { Atualizador.Limpar(Application.ExecutablePath); } catch { }
        tAtualizacao.Tick += (s, e) => { tAtualizacao.Interval = 12 * 3600 * 1000; ProcurarAtualizacao(false); };
        if (AtualizarSozinho) tAtualizacao.Start();
        if (Program.Atualizado) { var t = new Timer { Interval = 3000 }; t.Tick += (s, e) => { t.Stop(); t.Dispose(); Avisar('', "Volt atualizado para " + Atualizador.Atual.ToString(3)); }; t.Start(); }

        var tIni = new Timer { Interval = 2500 };
        tIni.Tick += (s, e) => { tIni.Stop(); tIni.Dispose(); GuardarNoTriangulo(); Aparar(); };
        tIni.Start();
        new Thread(() => { if (primeiraVez) { if (CriarTarefa()) inicioCache = true; } else ConferirInicio(); }) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
        if (primeiraVez) SalvarConfig();
        // instalado na pasta de programas: aparece em Configuracoes > Aplicativos e no menu Iniciar
        if (Instalador.EstaInstalado) new Thread(Instalador.GarantirRegistro) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
    }

    void Postar(Action a) { try { if (invocador.IsHandleCreated) invocador.BeginInvoke(a); } catch { } }

    public void Aparar()
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        try { SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, new IntPtr(-1), new IntPtr(-1)); } catch { }
    }

    // ---------------- leitura ----------------
    public void LerAgora() { pedido.Set(); }
    void LerEm(int ms) { tDebounce.Stop(); tDebounce.Interval = ms; tDebounce.Start(); }

    void LoopLeitura()
    {
        Thread.Sleep(1500);
        while (!saindo)
        {
            List<Aparelho> l = null;
            Postar(() => { Lendo = true; painel.Atualizar(); });
            try { l = Leitor.LerTudo(); } catch (Exception ex) { Registro.Erro(ex); }
            var r = l;
            Postar(() => { Lendo = false; Resultado(r); });
            pedido.WaitOne(Math.Max(1, Intervalo) * 60 * 1000);
        }
    }

    void Resultado(List<Aparelho> lidos)
    {
        UltimaLeitura = DateTime.Now;
        if (lidos == null) { painel.Atualizar(); return; }
        var vistos = new HashSet<string>();
        foreach (var a in lidos)
        {
            Estado e;
            if (!aparelhos.TryGetValue(a.Chave, out e)) aparelhos[a.Chave] = e = new Estado { Chave = a.Chave };
            bool estavaCarregando = e.Carregando;
            int antes = e.Pct;
            e.NomeOrig = a.Nome; e.Via = a.Via; e.Tipo = a.Tipo; e.Aproximado = a.Aproximado;
            if (a.Pct >= 0) e.Pct = a.Pct;
            if (!estavaCarregando && a.Carregando) Ciclos.Fechar(e);   // comecou a carregar: guarda como foi a descarga
            e.Carregando = a.Carregando;
            e.Detalhe = a.Detalhe;
            e.Partes = a.Partes;
            e.Presente = true; e.Dormindo = false; e.Visto = DateTime.Now;
            e.Prazo = a.Via.StartsWith("Receptor") ? 30 * 60 : 0;
            vistos.Add(a.Chave);
            Historico.Anotar(e);
            ConferirAvisos(e, antes, estavaCarregando);
        }
        foreach (var e in aparelhos.Values)
        {
            if (vistos.Contains(e.Chave) || !e.Presente) continue;
            if (e.Prazo > 0 && (DateTime.Now - e.Visto).TotalSeconds < e.Prazo) e.Dormindo = true;   // receptor: aparelho dormindo
            else { e.Presente = false; e.Dormindo = false; }
        }
        SalvarConfig();
        AtualizarIcones();
        painel.Atualizar();
        if (!painel.Visible && !MenuAberto) Aparar();   // devolve a memoria usada na leitura
    }

    // ---------------- avisos ----------------
    public int LimiteDe(Estado a) { return a.Limite >= 0 ? a.Limite : LimitePadrao; }

    void ConferirAvisos(Estado a, int antes, bool estavaCarregando)
    {
        int lim = LimiteDe(a);
        if (a.Carregando || a.Pct > lim + 3) { a.AvisouFraca = false; a.AvisouCritica = false; }
        if (!a.Carregando || a.Pct < 95) a.AvisouCheio = false;
        if (a.Carregando) a.AvisouPrevisao = false;
        if (a.Oculto || a.Pct < 0) return;
        string nome = NomeDe(a);
        // aviso inteligente: pela previsao, nao vai durar o dia
        if (AvisarPrevisao && !a.Carregando && !a.AvisouPrevisao && a.Pct > lim)
        {
            double h = Historico.Previsao(a);
            if (h > 0 && h < 12) { a.AvisouPrevisao = true; Avisar('\uE823', nome + " deve acabar em " + Volt.Duracao(h) + ": carregue hoje"); }
        }
        if (AvisarFraca && !a.Carregando)
        {
            if (a.Pct <= Math.Max(5, lim / 2) && !a.AvisouCritica) { a.AvisouCritica = a.AvisouFraca = true; Avisar('', nome + " com " + a.Pct + "%: carregue agora"); }
            else if (a.Pct <= lim && !a.AvisouFraca) { a.AvisouFraca = true; Avisar('', nome + " com bateria fraca (" + a.Pct + "%)"); }
        }
        if (AvisarCheio && a.Carregando && a.Pct >= 100 && !a.AvisouCheio && antes >= 0 && antes < 100)
        { a.AvisouCheio = true; Avisar('', nome + " carregou 100%: pode tirar do cabo"); }
    }

    // aviso discreto; com jogo/video em tela cheia espera
    public void Avisar(char gl, string txt)
    {
        if (TelaCheiaAtiva()) { filaAvisos.Add(new KeyValuePair<char, string>(gl, txt)); tFila.Start(); return; }
        MostrarAviso(gl, txt);
    }
    void MostrarFila()
    {
        if (TelaCheiaAtiva() || filaAvisos.Count == 0) { if (filaAvisos.Count == 0) tFila.Stop(); return; }
        tFila.Stop();
        var ult = filaAvisos[filaAvisos.Count - 1];
        string txt = filaAvisos.Count == 1 ? ult.Value : ult.Value + " (e mais " + (filaAvisos.Count - 1) + ")";
        filaAvisos.Clear();
        MostrarAviso(ult.Key, txt);
    }
    void MostrarAviso(char gl, string txt)
    {
        var p = Cursor.Position;
        int borda;
        aviso.Suave = Animacoes;
        aviso.Mostrar(gl, txt, TemaEscuro(), AreaUtil(Screen.FromPoint(p), out borda), EscalaEm(p));   // no monitor onde esta o mouse
    }

    static bool TelaCheiaAtiva()
    {
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero) return false;
        var sb = new System.Text.StringBuilder(64);
        GetClassName(h, sb, 64);
        string c = sb.ToString();
        if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
        if (IsZoomed(h) || (GetWindowLong(h, -16) & 0x00C00000) == 0x00C00000) return false;
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        var tela = Screen.FromHandle(h).Bounds;
        return r.L <= tela.Left && r.T <= tela.Top && r.R >= tela.Right && r.B >= tela.Bottom;
    }

    // ---------------- aparelhos ----------------
    public string NomeDe(Estado a) { return string.IsNullOrEmpty(a.Nome) ? a.NomeOrig ?? "Aparelho" : a.Nome; }

    // ordem do painel: o que precisa de atencao primeiro, depois do mais fraco ao mais cheio
    public List<Estado> ListaAparelhos(bool todos)
    {
        var l = new List<Estado>();
        foreach (var a in aparelhos.Values) if (todos || (a.Presente && !a.Oculto)) l.Add(a);
        l.Sort((x, y) =>
        {
            int nx = Volt.Nivel(x, LimiteDe(x)), ny = Volt.Nivel(y, LimiteDe(y));
            int px = nx == 2 ? 0 : nx == 1 ? 1 : 2, py = ny == 2 ? 0 : ny == 1 ? 1 : 2;
            if (px != py) return px.CompareTo(py);
            if (x.Presente != y.Presente) return x.Presente ? -1 : 1;
            return x.Pct.CompareTo(y.Pct);
        });
        return l;
    }

    public void Renomear(Estado a)
    {
        string n = PedirTexto("Renomear aparelho", "Um nome fácil (ex.: \"Mouse\", \"Fone\")", NomeDe(a));
        if (n == null) return;
        a.Nome = n == a.NomeOrig ? null : n;
        SalvarConfig(); Mudou();
    }

    public void Esquecer(Estado a)
    {
        aparelhos.Remove(a.Chave);
        Historico.Esquecer(a.Chave);
        Ciclos.Esquecer(a.Chave);
        SalvarConfig(); Mudou();
    }

    public void Mudou() { AtualizarIcones(); painel.Atualizar(); if (config != null && !config.IsDisposed) config.Atualizar(); }
    public void PainelMudou() { painel.Atualizar(); }

    string PedirTexto(string titulo, string dica, string valor)
    {
        using (var f = new JanelaTexto(titulo, dica, valor, TemaEscuro()))
        {
            if (f.ShowDialog() != DialogResult.OK) return null;
            string v = f.Valor.Replace("|", "").Replace("=", "").Replace("\r", "").Replace("\n", "");
            return v.Length == 0 ? null : v;
        }
    }

    // ---------------- icones da bandeja ----------------
    int TamIcone()
    {
        uint dpi = 96;
        try { IntPtr bar = FindWindow("Shell_TrayWnd", null); if (bar != IntPtr.Zero) dpi = GetDpiForWindow(bar); } catch { }
        if (dpi == 0) dpi = 96;
        try { return GetSystemMetricsForDpi(49, dpi); } catch { return (int)(16 * dpi / 96); }
    }

    bool BarraClara()
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
                if (v is int) return (int)v != 0;
            }
        }
        catch { }
        return false;
    }

    // aparelhos que aparecem na barra: os que voce fixou e os que estao com a bateria acabando
    public List<Estado> NaBarraAgora()
    {
        var l = new List<Estado>();
        foreach (var a in ListaAparelhos(false))
            if (a.NaBarra || (MostrarFracos && !a.Carregando && a.Pct >= 0 && a.Pct <= LimiteDe(a))) l.Add(a);
        return l;
    }

    public void AtualizarIcones()
    {
        var lista = ListaAparelhos(false);
        // icone do Volt (fica no ^, so para abrir o painel)
        int n = TamIcone();
        string chave = n.ToString();
        if (chave != chaveIcone)
        {
            chaveIcone = chave;
            using (var b = Desenho.Logo(n)) TrocarIcone(icone, b);
        }
        string dica = "Volt";
        if (lista.Count == 0) dica = "Volt · nenhum aparelho com bateria";
        else
        {
            var partes = new List<string>();
            foreach (var a in lista) partes.Add(Curto(NomeDe(a)) + " " + (a.Pct < 0 ? "?" : a.Pct + "%") + (a.Carregando ? "⚡" : ""));
            dica = string.Join("\n", partes);
        }
        icone.Text = dica.Length > 63 ? dica.Substring(0, 62) + "…" : dica;

        // faixa na barra de tarefas; se a barra nao permitir (vertical), um icone com numero por aparelho
        bool claro = BarraClara();
        var mostrar = NaBarraAgora();
        bool coube = faixa.Mostrar(mostrar, claro);
        var querem = new HashSet<string>();
        if (!coube) foreach (var a in mostrar) querem.Add(a.Chave);
        foreach (var k in new List<string>(numeros.Keys))
            if (!querem.Contains(k)) { numeros[k].Visible = false; numeros[k].Dispose(); numeros.Remove(k); }
        Color texto = claro ? Color.FromArgb(28, 28, 28) : Color.White;
        foreach (var a in mostrar)
        {
            if (!querem.Contains(a.Chave)) continue;
            NotifyIcon ni;
            bool novo = !numeros.TryGetValue(a.Chave, out ni);
            if (novo)
            {
                ni = new NotifyIcon();
                ni.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) painel.Alternar(); };
                ni.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) MenuEm(Cursor.Position); };
                numeros[a.Chave] = ni;
            }
            int nv = Volt.Nivel(a, LimiteDe(a));
            using (var b = Volt.IconeNumero(n, a.Pct, Volt.CorNivel(nv, !claro), texto, a.Carregando)) TrocarIcone(ni, b);
            string d = NomeDe(a) + ": " + (a.Pct < 0 ? "?" : a.Pct + "%") + (a.Carregando ? " (carregando)" : "");
            ni.Text = d.Length > 63 ? d.Substring(0, 63) : d;
            if (novo) ni.Visible = true;
        }
    }

    public void AbrirPainel() { Postar(() => painel.Alternar()); }

    // cartao com as informacoes do aparelho ao passar o mouse na faixa
    Dica dica;
    public void MostrarDica(Estado a, Rectangle r, int barraTopo, bool emCima, float esc)
    {
        if (painel.Visible || MenuAberto) return;
        if (dica == null) dica = new Dica(this);
        dica.Mostrar(a, r, barraTopo, emCima, esc);
    }
    public void EsconderDica() { if (dica != null) dica.Esconder(); }

    public string PrevisaoTexto(Estado a)
    {
        if (a.Dormindo) return "dormindo · visto " + Volt.Ha(a.Visto);
        double h = Historico.Previsao(a);
        if (a.Carregando) return a.Pct >= 100 ? "carregado" : h > 0 ? "cheio em " + Volt.Duracao(h) : "carregando";
        return h > 0 ? "dura " + Volt.Duracao(h) : "";
    }

    // menu do icone aberto a partir da faixa da barra
    public void MenuEm(Point p)
    {
        Postar(() =>
        {
            MenuAberto = true;
            MontarMenu();
            SetForegroundWindow(invocador.Handle);
            MostrarDentro(menu, p);
        });
    }

    // abre o menu inteiro na tela onde foi o clique (nunca vaza para o outro monitor)
    public static void MostrarDentro(ToolStripDropDown m, Point p)
    {
        var sz = m.GetPreferredSize(Size.Empty);
        var t = Screen.FromPoint(p).Bounds;
        int x = p.X + sz.Width <= t.Right ? p.X : p.X - sz.Width;
        int y = p.Y - sz.Height >= t.Top ? p.Y - sz.Height : p.Y;
        x = Math.Max(t.Left, Math.Min(x, t.Right - sz.Width));
        y = Math.Max(t.Top, Math.Min(y, t.Bottom - sz.Height));
        m.Show(new Point(x, y), ToolStripDropDownDirection.BelowRight);
    }

    public void FixarNaBarra(Estado a, bool v) { a.NaBarra = v; SalvarConfig(); Mudou(); }
    public void DefinirFracos(bool v) { MostrarFracos = v; SalvarConfig(); AtualizarIcones(); }

    static string Curto(string n) { return n.Length > 18 ? n.Substring(0, 17) + "…" : n; }

    static void TrocarIcone(NotifyIcon ni, Bitmap b)
    {
        var antigo = ni.Icon;
        ni.Icon = Desenho.IconeDe(b);
        if (antigo != null) antigo.Dispose();
    }

    public void DefinirIntervalo(int v) { Intervalo = v; SalvarConfig(); pedido.Set(); }

    // O icone do Volt fica sempre guardado no ^ (so a faixa dos aparelhos vai para a barra)
    static string ExeVolt { get { return "\\" + Path.GetFileName(Application.ExecutablePath); } }

    void LimparRegistrosAntigos()
    {
        if (cfgVersao == "3") return;
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
            {
                if (k == null) return;
                foreach (var n in k.GetSubKeyNames())
                {
                    string p;
                    using (var s = k.OpenSubKey(n)) p = s == null ? null : s.GetValue("ExecutablePath") as string;
                    if (p != null && p.EndsWith(ExeVolt, StringComparison.OrdinalIgnoreCase)) k.DeleteSubKeyTree(n, false);
                }
            }
        }
        catch { }
        cfgVersao = "3";
        SalvarConfig();
    }

    // se o Windows mesmo assim colocou o icone na barra, marca como guardado e poe o icone de novo
    void GuardarNoTriangulo()
    {
        bool mudou = false;
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
            {
                if (k == null) return;
                foreach (var n in k.GetSubKeyNames())
                    using (var s = k.OpenSubKey(n, true))
                    {
                        var p = s == null ? null : s.GetValue("ExecutablePath") as string;
                        if (p == null || !p.EndsWith(ExeVolt, StringComparison.OrdinalIgnoreCase)) continue;
                        object v = s.GetValue("IsPromoted");
                        if (!(v is int) || (int)v != 0) { s.SetValue("IsPromoted", 0, RegistryValueKind.DWord); mudou = true; }
                    }
            }
        }
        catch { }
        if (!mudou) return;
        icone.Visible = false;
        var t = new Timer { Interval = 1500 };
        t.Tick += (s, e) => { t.Stop(); t.Dispose(); icone.Visible = true; };
        t.Start();
    }

    // ---------------- menu do icone ----------------
    void MontarMenu()
    {
        menu.Items.Clear();
        menu.Items.Add(new ToolStripLabel("Volt") { Tag = "titulo" });
        var lista = ListaAparelhos(false);
        if (lista.Count == 0) menu.Items.Add(new ToolStripLabel("Nenhum aparelho com bateria") { Tag = "fraco" });
        foreach (var a in lista)
            menu.Items.Add(new ToolStripLabel(NomeDe(a) + "  ·  " + (a.Pct < 0 ? "?" : a.Pct + "%") + (a.Carregando ? "  ⚡" : "")) { Tag = "fraco" });
        menu.Items.Add(new ToolStripSeparator());
        var abrir = new ToolStripMenuItem("Abrir painel");
        abrir.Click += (s, e) => Postar(() => painel.Mostrar());
        menu.Items.Add(abrir);
        var ler = new ToolStripMenuItem("Ler a bateria agora");
        ler.Click += (s, e) => LerAgora();
        menu.Items.Add(ler);
        menu.Items.Add(new ToolStripSeparator());
        if (TemNovaVersao)
        {
            var at = new ToolStripMenuItem("Atualizar para a versão " + NovaVersaoTexto);
            at.Click += (s, e) => Postar(() => TentarInstalar(true));
            menu.Items.Add(at);
        }
        var cfg = new ToolStripMenuItem("Configurações…");
        cfg.Click += (s, e) => Postar(() => AbrirConfiguracoes(-1));
        menu.Items.Add(cfg);
        var sair = new ToolStripMenuItem("Sair");
        sair.Click += (s, e) => Sair();
        menu.Items.Add(sair);
        AplicarTema(menu);
    }

    public void ConferirPainel()
    {
        Postar(() =>
        {
            var af = Form.ActiveForm;
            bool cfg = config != null && !config.IsDisposed && af == config;
            if (painel.Visible && af != painel && !cfg) painel.Esconder();
        });
    }

    public void AbrirConfiguracoes(int pagina)
    {
        if (config != null && !config.IsDisposed) { if (pagina >= 0) config.IrPara(pagina); config.Activate(); return; }
        config = new Configuracoes(this);
        if (pagina >= 0) config.IrPara(pagina);
        config.FormClosed += (s, e) => { config = null; Aparar(); };
        config.Show();
        config.Activate();
    }

    void Sair()
    {
        faixa.Fechar();
        saindo = true; pedido.Set();
        SalvarConfig();
        icone.Visible = false;
        foreach (var ni in numeros.Values) ni.Visible = false;
        Application.Exit();
    }

    // ---------------- tema ----------------
    public string Tema { get { return tema; } set { tema = value; SalvarConfig(); chaveIcone = null; painel.TemaMudou(); } }

    public bool TemaEscuro()
    {
        if (tema == "claro") return false;
        if (tema == "escuro") return true;
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                if (v is int) return (int)v == 0;
            }
        }
        catch { }
        return false;
    }

    public static Color CorDeDestaque(bool escuro)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = k == null ? null : k.GetValue("AccentColor");
                if (v is int)
                {
                    int c = (int)v;
                    var cor = Color.FromArgb(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
                    if (escuro) cor = Color.FromArgb(Math.Min(255, cor.R + (255 - cor.R) / 2), Math.Min(255, cor.G + (255 - cor.G) / 2), Math.Min(255, cor.B + (255 - cor.B) / 2));
                    return cor;
                }
            }
        }
        catch { }
        return escuro ? Color.FromArgb(76, 194, 255) : Color.FromArgb(0, 103, 192);
    }

    public static Color TextoSobre(Color c) { return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) > 150 ? Color.Black : Color.White; }

    public static float EscalaEm(Point p)
    {
        try { uint dx, dy; if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out dx, out dy) == 0) return dx / 96f; } catch { }
        return 1f;
    }

    public static Rectangle AreaUtil(Screen tela, out int borda)
    {
        var b = tela.Bounds;
        var area = tela.WorkingArea;
        borda = 0;
        Rectangle barra = Rectangle.Empty;
        long melhor = 0;
        foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            IntPtr h = IntPtr.Zero;
            while ((h = FindWindowEx(IntPtr.Zero, h, cls, null)) != IntPtr.Zero)
            {
                RECT r;
                if (!GetWindowRect(h, out r)) continue;
                var rr = Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
                var inter = Rectangle.Intersect(rr, b);
                long ar = (long)inter.Width * inter.Height;
                bool encosta = inter.Width > 0 && inter.Height > 0 &&
                               ((rr.Width >= b.Width * 0.9 && Math.Abs(rr.Left - b.Left) < 4) || (rr.Height >= b.Height * 0.9 && Math.Abs(rr.Top - b.Top) < 4));
                if (encosta && ar > melhor) { melhor = ar; barra = rr; }
            }
        }
        if (barra == Rectangle.Empty)
        {
            if (area.Top > b.Top) borda = 1; else if (area.Left > b.Left) borda = 2; else if (area.Right < b.Right) borda = 3;
            return area;
        }
        bool horizontal = barra.Width >= barra.Height;
        if (horizontal) borda = barra.Top + barra.Height / 2 > b.Top + b.Height / 2 ? 0 : 1;
        else borda = barra.Left + barra.Width / 2 > b.Left + b.Width / 2 ? 3 : 2;
        int esp = Math.Min(horizontal ? barra.Height : barra.Width, (horizontal ? b.Height : b.Width) / 3);
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf(typeof(APPBARDATA)) };
        bool auto = false;
        try { auto = (SHAppBarMessage(4, ref abd) & 1) != 0; } catch { }
        if (auto || area == b)
        {
            if (borda == 0) area = Rectangle.FromLTRB(area.Left, area.Top, area.Right, Math.Min(area.Bottom, b.Bottom - esp));
            else if (borda == 1) area = Rectangle.FromLTRB(area.Left, Math.Max(area.Top, b.Top + esp), area.Right, area.Bottom);
            else if (borda == 2) area = Rectangle.FromLTRB(Math.Max(area.Left, b.Left + esp), area.Top, area.Right, area.Bottom);
            else area = Rectangle.FromLTRB(area.Left, area.Top, Math.Min(area.Right, b.Right - esp), area.Bottom);
        }
        return area;
    }

    public void AplicarTema(ContextMenuStrip m)
    {
        var r = new TemaMenu(TemaEscuro());
        ToolStripManager.Renderer = r;
        m.Renderer = r;
        float esc = EscalaEm(Cursor.Position);
        var fonte = new Font("Segoe UI", 9.5f * esc);
        m.Font = fonte;
        m.ImageScalingSize = new Size((int)(16 * esc), (int)(16 * esc));
        Arredondar(m);
        AplicarFonte(m.Items, fonte, esc, r);
    }

    void AplicarFonte(ToolStripItemCollection itens, Font fonte, float esc, TemaMenu r)
    {
        foreach (ToolStripItem it in itens)
        {
            it.Font = (it.Tag as string) == "titulo" ? new Font("Segoe UI Semibold", 10.5f * esc) : fonte;
            if (!(it is ToolStripSeparator)) it.Padding = new Padding(0, (int)(2 * esc), 0, (int)(2 * esc));
            var d = it as ToolStripMenuItem;
            if (d != null && d.HasDropDownItems)
            {
                d.DropDown.Renderer = r;
                d.DropDown.Font = fonte;
                var dd = d.DropDown as ToolStripDropDownMenu;
                if (dd != null) { dd.ShowImageMargin = false; dd.ShowCheckMargin = true; }
                Arredondar(d.DropDown);
                AplicarFonte(d.DropDownItems, fonte, esc, r);
            }
        }
    }

    static void Arredondar(ToolStripDropDown dd)
    {
        if ((dd.Tag as string) == "arredondado") return;
        dd.Tag = "arredondado";
        dd.Opened += (s, e) => { int pref = 2; try { DwmSetWindowAttribute(dd.Handle, 33, ref pref, 4); } catch { } };
    }

    // ---------------- configuracao ----------------
    void LerConfig()
    {
        try
        {
            if (!File.Exists(arqConfig)) return;
            foreach (var l in File.ReadAllLines(arqConfig))
            {
                var p = l.Split(new[] { '=' }, 2);
                if (p.Length != 2) continue;
                string k = p[0], v = p[1];
                int n;
                switch (k)
                {
                    case "barra_fracos": MostrarFracos = v == "1"; break;
                    case "cfg": cfgVersao = v; break;
                    case "animacoes": Animacoes = v == "1"; break;
                    case "menu_lateral": MenuLateralAberto = v == "1"; break;
                    case "atualizar": AtualizarSozinho = v == "1"; break;
                    case "avisar_fraca": AvisarFraca = v == "1"; break;
                    case "avisar_cheio": AvisarCheio = v == "1"; break;
                    case "avisar_previsao": AvisarPrevisao = v == "1"; break;
                    case "limite": if (int.TryParse(v, out n) && n >= 5 && n <= 60) LimitePadrao = n; break;
                    case "intervalo": if (int.TryParse(v, out n) && n >= 1 && n <= 60) Intervalo = n; break;
                    case "tema": if (v == "auto" || v == "claro" || v == "escuro") tema = v; break;
                    case "aparelho":
                        {
                            // chave|nome original|nome|tipo|via|limite|oculto|ultimo %|visto
                            var c = v.Split('|');
                            if (c.Length < 9) break;
                            bool naBarra = c.Length > 9 && c[9] == "1";
                            var e = new Estado { Chave = c[0], NomeOrig = c[1], Nome = c[2].Length == 0 ? null : c[2], Via = c[4], Oculto = c[6] == "1" };
                            Tipo t; if (Enum.TryParse(c[3], out t)) e.Tipo = t;
                            if (int.TryParse(c[5], out n)) e.Limite = n;
                            if (int.TryParse(c[7], out n)) e.Pct = n;
                            long tk; if (long.TryParse(c[8], out tk)) e.Visto = new DateTime(tk);
                            e.NaBarra = naBarra;
                            aparelhos[e.Chave] = e;
                        }
                        break;
                }
            }
        }
        catch (Exception ex) { Registro.Erro(ex); }
    }

    public void SalvarConfig()
    {
        try
        {
            var l = new List<string> {
                "cfg=" + (cfgVersao == "3" ? "3" : "2"), "barra_fracos=" + (MostrarFracos ? 1 : 0), "animacoes=" + (Animacoes ? 1 : 0),
                "menu_lateral=" + (MenuLateralAberto ? 1 : 0), "atualizar=" + (AtualizarSozinho ? 1 : 0),
                "avisar_fraca=" + (AvisarFraca ? 1 : 0), "avisar_cheio=" + (AvisarCheio ? 1 : 0), "avisar_previsao=" + (AvisarPrevisao ? 1 : 0),
                "limite=" + LimitePadrao, "intervalo=" + Intervalo, "tema=" + tema };
            foreach (var a in aparelhos.Values)
                l.Add("aparelho=" + string.Join("|", new[] { a.Chave, Limpo(a.NomeOrig), Limpo(a.Nome), a.Tipo.ToString(), Limpo(a.Via),
                    a.Limite.ToString(), a.Oculto ? "1" : "0", a.Pct.ToString(), a.Visto.Ticks.ToString(), a.NaBarra ? "1" : "0" }));
            File.WriteAllLines(arqConfig, l, System.Text.Encoding.UTF8);
        }
        catch { }
    }
    static string Limpo(string s) { return (s ?? "").Replace("|", "").Replace("\r", "").Replace("\n", ""); }

    // ---------------- iniciar com o Windows (Agendador de Tarefas) ----------------
    const string NomeTarefa = "Volt";

    static int Schtasks(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(15000); return p.ExitCode; }
        }
        catch { return -1; }
    }
    static bool TarefaExiste() { return Schtasks("/query /tn \"" + NomeTarefa + "\"") == 0; }
    static bool CriarTarefa()
    {
        string usuario = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        string exe = Application.ExecutablePath;
        Func<string, string> X = System.Security.SecurityElement.Escape;
        string xml =
            "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
            "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
            "  <RegistrationInfo><Description>Abre o Volt (bateria dos aparelhos sem fio) ao entrar no Windows.</Description></RegistrationInfo>\r\n" +
            "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + X(usuario) + "</UserId><Delay>PT5S</Delay></LogonTrigger></Triggers>\r\n" +
            "  <Principals><Principal id=\"Author\"><UserId>" + X(usuario) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>\r\n" +
            "  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority></Settings>\r\n" +
            "  <Actions Context=\"Author\"><Exec><Command>" + X(exe) + "</Command><WorkingDirectory>" + X(Path.GetDirectoryName(exe)) + "</WorkingDirectory></Exec></Actions>\r\n" +
            "</Task>\r\n";
        string tmp = Path.Combine(Path.GetTempPath(), "Volt_tarefa.xml");
        try { File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode); return Schtasks("/create /tn \"" + NomeTarefa + "\" /xml \"" + tmp + "\" /f") == 0; }
        finally { try { File.Delete(tmp); } catch { } }
    }
    void ConferirInicio() { bool e = TarefaExiste(); if (e) CriarTarefa(); inicioCache = e; }
    public bool IniciaComWindows() { if (inicioCache == null) inicioCache = TarefaExiste(); return inicioCache.Value; }
    public void AlternarInicio()
    {
        if (IniciaComWindows()) Schtasks("/delete /tn \"" + NomeTarefa + "\" /f");
        else if (!CriarTarefa()) MessageBox.Show("Não consegui criar a tarefa de inicialização no Agendador de Tarefas.", "Volt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        inicioCache = null;
    }

    // ---------------- atualizacao ----------------
    void AtualizarConfig() { if (config != null && !config.IsDisposed) config.Atualizar(); }
    public void DefinirAtualizarSozinho(bool v)
    {
        AtualizarSozinho = v; SalvarConfig();
        if (v) { tAtualizacao.Interval = 120 * 1000; tAtualizacao.Start(); } else tAtualizacao.Stop();
    }
    public bool TemNovaVersao { get { return novaVersao != null && novaBin != null; } }
    public string NovaVersaoTexto { get { return novaVersao == null ? "" : novaVersao.Versao.ToString(3); } }

    public void ProcurarAtualizacao(bool agora)
    {
        if (procurandoAtualizacao) return;
        if (TemNovaVersao) { TentarInstalar(agora); return; }
        procurandoAtualizacao = true;
        EstadoAtualizacao = "Procurando…"; AtualizarConfig();
        new Thread(() =>
        {
            Atualizador.Nova n = null; byte[] bin = null; string erro = null;
            try { n = Atualizador.Procurar(); if (n != null) bin = Atualizador.BaixarConferido(n, out erro); }
            catch (Exception ex) { erro = ex is System.Net.WebException ? "sem conexão" : ex.Message; }
            Postar(() =>
            {
                procurandoAtualizacao = false;
                if (n == null) EstadoAtualizacao = erro != null ? "Não deu para conferir (" + erro + ")" : "Você está na versão mais nova";
                else if (bin == null) EstadoAtualizacao = "Versão " + n.Versao + " recusada: " + erro;
                else { novaVersao = n; novaBin = bin; EstadoAtualizacao = "Versão " + n.Versao.ToString(3) + " pronta"; if (AtualizarSozinho || agora) TentarInstalar(agora); }
                AtualizarConfig();
            });
        }) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
    }

    public void TentarInstalar(bool agora)
    {
        if (!TemNovaVersao) return;
        bool ocupado = painel.Visible || MenuAberto || TelaCheiaAtiva() || (!agora && config != null && !config.IsDisposed);
        if (ocupado && !agora)
        {
            var t = new Timer { Interval = 5 * 60 * 1000 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); TentarInstalar(false); };
            t.Start();
            return;
        }
        string exe = Application.ExecutablePath;
        string r = Atualizador.Instalar(novaBin, exe, novaVersao.Versao);
        if (r != null) { EstadoAtualizacao = "Não deu para instalar (" + r + ")"; AtualizarConfig(); return; }
        saindo = true; pedido.Set();
        SalvarConfig();
        icone.Visible = false;
        foreach (var ni in numeros.Values) ni.Visible = false;
        faixa.Fechar();
        try { Process.Start(exe, "--atualizado"); } catch { }
        Application.Exit();
    }
}
