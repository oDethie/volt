// Volt - pecas visuais e de sistema (vindas do Sintonia)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

// =====================================================================
//  Desenho: icones com a fonte de simbolos do Windows (mesmos do sistema)
// =====================================================================
// "Clicar de novo fecha": se o clique que fechou um menu caiu no mesmo lugar que o abriu,
// nao abre de novo
static class Alterna
{
    static string chave;
    static DateTime quando;
    public static bool PodeAbrir(string k) { return !(k == chave && (DateTime.Now - quando).TotalMilliseconds < 450); }
    public static void Fechou(string k) { chave = k; quando = DateTime.Now; if (abertaChave == k) { aberto = null; abertaChave = null; } }

    // Menu aberto agora: um clique (ou clique duplo) no mesmo lugar fecha em vez de abrir outro
    static ToolStripDropDown aberto;
    static string abertaChave;
    public static void Abriu(string k, ToolStripDropDown m) { aberto = m; abertaChave = k; }
    public static bool FecharSeAberto(string k)
    {
        if (aberto != null && abertaChave == k && aberto.Visible) { aberto.Close(); return true; }
        return false;
    }
}

static class Desenho
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static string fonte;

    public static string FonteIcones
    {
        get
        {
            if (fonte == null)
            {
                fonte = "Segoe MDL2 Assets";
                try { using (var f = new Font("Segoe Fluent Icons", 10f)) if (f.Name == "Segoe Fluent Icons") fonte = f.Name; } catch { }
            }
            return fonte;
        }
    }

    public static GraphicsPath Arred(RectangleF r, float raio)
    {
        var p = new GraphicsPath();
        float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Bitmap Glifo(char c, int n, Color cor, float escala)
    {
        // A fonte de icones so desenha pelo GDI (TextRenderer): desenha branco no preto
        // com suavizacao em tons de cinza e usa o brilho de cada ponto como transparencia.
        var bmp = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var f = new Font(FonteIcones, Math.Max(1, (int)Math.Round(n * escala)), GraphicsUnit.Pixel))
        {
            g.Clear(Color.Black);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            TextRenderer.DrawText(g, c.ToString(), f, new Rectangle(0, 0, n, n), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
        var r = new Rectangle(0, 0, n, n);
        var bd = bmp.LockBits(r, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var px = new int[n * n];
        Marshal.Copy(bd.Scan0, px, 0, px.Length);
        int rgb = cor.ToArgb() & 0xFFFFFF;
        for (int i = 0; i < px.Length; i++)
        {
            int v = px[i];
            int a = Math.Max((v >> 16) & 0xFF, Math.Max((v >> 8) & 0xFF, v & 0xFF));
            px[i] = (a << 24) | rgb;
        }
        Marshal.Copy(px, 0, bd.Scan0, px.Length);
        bmp.UnlockBits(bd);
        return bmp;
    }

    public static Icon IconeDe(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        var ic = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        return ic;
    }

    // Logo do Sintonia: quadrado arredondado roxo com um botao giratorio de volume
    // (desenho em grade 64x64)
    public static Bitmap Logo(int n)
    {
        var bmp = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float u = n / 64f;
            using (var p = Arred(new RectangleF(2 * u, 2 * u, 60 * u, 60 * u), 15 * u))
            using (var b = new LinearGradientBrush(new PointF(0, 0), new PointF(n, n), Color.FromArgb(255, 196, 61), Color.FromArgb(240, 120, 20)))
                g.FillPath(b, p);
            // raio
            var pts = new[] { new PointF(36 * u, 9 * u), new PointF(17 * u, 36 * u), new PointF(30 * u, 36 * u), new PointF(26 * u, 55 * u), new PointF(47 * u, 27 * u), new PointF(34 * u, 27 * u), new PointF(39 * u, 9 * u) };
            using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, pts);
        }
        return bmp;
    }

    // Icone da bandeja: so o botao giratorio, de uma cor (branco na barra escura,
    // preto na clara). No mudo ganha um risco atravessado.
    public static Bitmap Bandeja(int n, Color cor, bool mudo)
    {
        var bmp = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float u = n / 64f;
            float cx = 32 * u, cy = 35 * u, r = 25 * u;
            using (var pn = new Pen(cor, 7.5f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pn, new RectangleF(cx - r, cy - r, r * 2, r * 2), 144, 252);
            using (var b = new SolidBrush(cor)) g.FillEllipse(b, cx - 11 * u, cy - 11 * u, 22 * u, 22 * u);
            if (mudo)
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                using (var pn = new Pen(Color.Transparent, 15 * u)) g.DrawLine(pn, 6 * u, 6 * u, 58 * u, 58 * u);
                g.CompositingMode = CompositingMode.SourceOver;
                using (var pn = new Pen(cor, 6.5f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pn, 8 * u, 8 * u, 56 * u, 56 * u);
            }
        }
        return bmp;
    }

    // bolinha de "conectado" no canto do icone da bandeja (outro PC controlando este)
    public static void Conectado(Bitmap bmp, Color acento, Color contorno)
    {
        int n = bmp.Width;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Math.Max(5f, n * 0.42f), x = n - d, y = n - d;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var b = new SolidBrush(Color.Transparent)) g.FillEllipse(b, x - n * 0.07f, y - n * 0.07f, d + n * 0.14f, d + n * 0.14f);
            g.CompositingMode = CompositingMode.SourceOver;
            using (var b = new SolidBrush(acento)) g.FillEllipse(b, x, y, d, d);
        }
    }

    // icone da bandeja enquanto o outro PC muda algo: o item (app ou saida) + seta / mudo
    public static Bitmap Atividade(int n, Bitmap app, char glifo, Color cor, int tipo, Color acento)
    {
        var bmp = new Bitmap(n, n, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float t = n * 0.8f;
            if (app != null) g.DrawImage(app, new RectangleF(0, 0, t, t));
            else using (var gl = Glifo(glifo, (int)Math.Round(t), cor, 0.85f)) g.DrawImage(gl, 0, 0);
            // selo no canto: seta para cima/baixo, ou X (mudo), ou alto-falante (som de volta)
            float d = n * 0.56f, x = n - d, y = n - d;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var b = new SolidBrush(Color.Transparent)) g.FillEllipse(b, x - n * 0.06f, y - n * 0.06f, d + n * 0.12f, d + n * 0.12f);
            g.CompositingMode = CompositingMode.SourceOver;
            Color fundo = tipo == 2 ? Color.FromArgb(196, 43, 28)        // mudo: vermelho
                        : tipo == -1 ? Color.FromArgb(247, 185, 20)      // desceu: amarelo
                        : Color.FromArgb(16, 137, 62);                   // subiu / som de volta: verde
            using (var b = new SolidBrush(fundo)) g.FillEllipse(b, x, y, d, d);
            float cx = x + d / 2, cy = y + d / 2, r = d * 0.26f;
            using (var pn = new Pen(tipo == -1 ? Color.FromArgb(40, 30, 0) : Color.White, Math.Max(1.2f, n / 14f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (tipo == 1 || tipo == -1)
                {
                    float s = tipo == 1 ? -1 : 1;
                    g.DrawLine(pn, cx, cy - s * r, cx, cy + s * r * 1.05f);
                    g.DrawLines(pn, new[] { new PointF(cx - r, cy + s * r * 0.15f), new PointF(cx, cy + s * r * 1.05f), new PointF(cx + r, cy + s * r * 0.15f) });
                }
                else if (tipo == 2)
                {
                    g.DrawLine(pn, cx - r * 0.8f, cy - r * 0.8f, cx + r * 0.8f, cy + r * 0.8f);
                    g.DrawLine(pn, cx + r * 0.8f, cy - r * 0.8f, cx - r * 0.8f, cy + r * 0.8f);
                }
                else
                {
                    g.DrawLines(pn, new[] { new PointF(cx - r, cy + r * 0.05f), new PointF(cx - r * 0.25f, cy + r * 0.8f), new PointF(cx + r, cy - r * 0.7f) });
                }
            }
        }
        return bmp;
    }

    public static void SalvarIco(string caminho)
    {
        int[] tams = { 16, 20, 24, 32, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (var t in tams)
            using (var bmp = Logo(t))
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
//  Visual do menu (igual ao OLED Descanso)
// =====================================================================
class TemaMenu : ToolStripProfessionalRenderer
{
    public readonly Color Fundo, Borda, Destaque, Texto, Fraco, Linha;

    public TemaMenu(bool escuro) : base(new Cores(escuro))
    {
        RoundedEdges = false;
        Fundo = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
        Borda = escuro ? Color.FromArgb(69, 69, 69) : Color.FromArgb(213, 213, 213);
        Destaque = escuro ? Color.FromArgb(61, 61, 61) : Color.FromArgb(229, 229, 229);
        Texto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        Fraco = escuro ? Color.FromArgb(154, 154, 154) : Color.FromArgb(112, 112, 112);
        Linha = escuro ? Color.FromArgb(71, 71, 71) : Color.FromArgb(220, 220, 220);
    }

    class Cores : ProfessionalColorTable
    {
        readonly Color f, b, d;
        public Cores(bool e)
        {
            UseSystemColors = false;
            f = e ? Color.FromArgb(43, 43, 43) : Color.FromArgb(249, 249, 249);
            b = e ? Color.FromArgb(69, 69, 69) : Color.FromArgb(213, 213, 213);
            d = e ? Color.FromArgb(61, 61, 61) : Color.FromArgb(229, 229, 229);
        }
        public override Color ToolStripDropDownBackground { get { return f; } }
        public override Color ImageMarginGradientBegin { get { return f; } }
        public override Color ImageMarginGradientMiddle { get { return f; } }
        public override Color ImageMarginGradientEnd { get { return f; } }
        public override Color MenuBorder { get { return b; } }
        public override Color MenuItemBorder { get { return d; } }
        public override Color MenuItemSelected { get { return d; } }
        public override Color MenuItemSelectedGradientBegin { get { return d; } }
        public override Color MenuItemSelectedGradientEnd { get { return d; } }
        public override Color MenuItemPressedGradientBegin { get { return d; } }
        public override Color MenuItemPressedGradientEnd { get { return d; } }
        public override Color CheckBackground { get { return f; } }
        public override Color CheckSelectedBackground { get { return d; } }
        public override Color CheckPressedBackground { get { return d; } }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = (!e.Item.Enabled || (e.Item.Tag as string) == "fraco") ? Fraco : Texto;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Texto;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, e.ImageRectangle, Texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (var p = new Pen(Linha)) e.Graphics.DrawLine(p, 6, y, e.Item.Width - 6, y);
    }
}

// Base das janelinhas (atalho, nome): paleta do Windows 11 e escala do monitor
class JanelaBase : Form
{
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr hmon, int t, out uint x, out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint f);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    protected readonly float esc;
    protected readonly bool escuro;
    protected readonly Color cFundo, cCartao, cBorda, cTexto, cFraco, cTecla, cTeclaBorda, cAcento, cTextoAcento, cOk, cAviso, cErro;
    protected readonly Font fTitulo, fNormal, fPequena, fTecla;

    protected int E(float v) { return (int)Math.Round(v * esc); }

    public JanelaBase(bool temaEscuro)
    {
        escuro = temaEscuro;
        esc = 1f;
        try { uint dx, dy; if (GetDpiForMonitor(MonitorFromPoint(Cursor.Position, 2), 0, out dx, out dy) == 0) esc = dx / 96f; } catch { }
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cBorda = escuro ? Color.FromArgb(58, 58, 58) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cTecla = escuro ? Color.FromArgb(58, 58, 58) : Color.White;
        cTeclaBorda = escuro ? Color.FromArgb(85, 85, 85) : Color.FromArgb(200, 200, 200);
        cAcento = App.CorDeDestaque(escuro);
        cTextoAcento = App.TextoSobre(cAcento);
        cOk = escuro ? Color.FromArgb(108, 203, 95) : Color.FromArgb(15, 123, 15);
        cAviso = escuro ? Color.FromArgb(252, 225, 0) : Color.FromArgb(157, 93, 0);
        cErro = escuro ? Color.FromArgb(255, 153, 164) : Color.FromArgb(196, 43, 28);
        fTitulo = new Font("Segoe UI Semibold", 14f * esc);
        fNormal = new Font("Segoe UI", 10f * esc);
        fPequena = new Font("Segoe UI", 9f * esc);
        fTecla = new Font("Segoe UI Semibold", 13f * esc);

        Text = "Volt";
        using (var b = Desenho.Logo(32)) Icon = Desenho.IconeDe(b);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = cFundo;
        ForeColor = cTexto;
        Font = fNormal;
        KeyPreview = true;
    }

    protected Button Botao(string texto, bool principal)
    {
        var b = new Button
        {
            Text = texto, Font = fNormal, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Size = new Size(Math.Max(E(96), TextRenderer.MeasureText(texto, fNormal).Width + E(28)), E(34)),
            BackColor = principal ? cAcento : cTecla, ForeColor = principal ? cTextoAcento : cTexto,
            TabStop = false, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = principal ? cAcento : cTeclaBorda;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = principal ? ControlPaint.Light(cAcento, 0.15f) : (escuro ? Color.FromArgb(68, 68, 68) : Color.FromArgb(240, 240, 240));
        b.FlatAppearance.MouseDownBackColor = principal ? ControlPaint.Dark(cAcento, 0.05f) : (escuro ? Color.FromArgb(52, 52, 52) : Color.FromArgb(230, 230, 230));
        return b;
    }

    protected Panel Rodape()
    {
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = E(64), BackColor = cCartao };
        rodape.Paint += (s, e) => { using (var p = new Pen(cBorda)) e.Graphics.DrawLine(p, 0, 0, rodape.Width, 0); };
        Controls.Add(rodape);
        return rodape;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20 /*DWMWA_USE_IMMERSIVE_DARK_MODE*/, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref round, 4); } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var a = Screen.FromPoint(Cursor.Position).WorkingArea; // abre no monitor onde esta o mouse
        Location = new Point(a.X + (a.Width - Width) / 2, a.Y + (a.Height - Height) / 2);
        Activate();
    }
}

// Janelinha para digitar um nome (renomear dispositivo, criar perfil)
class JanelaTexto : JanelaBase
{
    readonly TextBox caixa;
    public string Valor { get { return caixa.Text.Trim(); } }

    public JanelaTexto(string titulo, string dica, string valor, bool temaEscuro) : this(titulo, dica, valor, temaEscuro, "Salvar", 40) { }

    public JanelaTexto(string titulo, string dica, string valor, bool temaEscuro, string textoOk, int max) : base(temaEscuro)
    {
        ClientSize = new Size(E(420), E(210));
        int x = E(24), w = ClientSize.Width - E(48), y = E(20);
        Controls.Add(new Label { Text = titulo, Font = fTitulo, AutoSize = true, Location = new Point(x, y), ForeColor = cTexto });
        y += E(38);
        Controls.Add(new Label { Text = dica, Font = fPequena, AutoSize = false, Size = new Size(w, E(22)), Location = new Point(x, y), ForeColor = cFraco });
        y += E(26);
        caixa = new TextBox
        {
            Text = valor ?? "", Font = fNormal, Location = new Point(x, y), Width = w,
            BackColor = cTecla, ForeColor = cTexto, BorderStyle = BorderStyle.FixedSingle, MaxLength = max
        };
        Controls.Add(caixa);

        var rodape = Rodape();
        var bSalvar = Botao(textoOk, true);
        var bCancelar = Botao("Cancelar", false);
        int by = (rodape.Height - E(34)) / 2;
        bSalvar.Location = new Point(rodape.Width - E(24) - bSalvar.Width, by);
        bCancelar.Location = new Point(bSalvar.Left - E(8) - bCancelar.Width, by);
        rodape.Controls.AddRange(new Control[] { bSalvar, bCancelar });
        bSalvar.Click += (s, e) => { if (Valor.Length > 0) DialogResult = DialogResult.OK; };
        bCancelar.Click += (s, e) => DialogResult = DialogResult.Cancel;
        AcceptButton = bSalvar;
        CancelButton = bCancelar;
        Shown += (s, e) => { caixa.Focus(); caixa.SelectAll(); };
    }
}

// =====================================================================
//  Aviso na tela (ao trocar saida, perfil, mudo). Nao rouba o foco.
// =====================================================================
class Aviso : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);

    readonly Timer t = new Timer { Interval = 1600 };
    public int Duracao = 5000;
    readonly Timer tFade = new Timer { Interval = 15 };
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint cor, byte alfa, uint f);
    float alfa = 1f, alfaAlvo = 1f;
    public bool Suave = true;
    char glifo; string texto;
    Color cFundo, cTexto, cIcone;
    Font fTexto, fGlifo;
    float esc = 1f;

    public Aviso()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        t.Tick += (s, e) =>
        {
            t.Stop();
            if (Suave) { alfaAlvo = 0f; tFade.Start(); } else Hide();
        };
        tFade.Tick += (s, e) =>
        {
            alfa += (alfaAlvo - alfa) * 0.3f;
            if (Math.Abs(alfaAlvo - alfa) < 0.03f) alfa = alfaAlvo;
            SetLayeredWindowAttributes(Handle, 0, (byte)(alfa * 255), 2);
            if (alfa == alfaAlvo) { tFade.Stop(); if (alfa == 0f) Hide(); }
        };
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/ | 0x80000 /*LAYERED*/;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0 /*WM_DPICHANGED*/) { m.Result = IntPtr.Zero; return; }
        if (m.Msg == 0x0021 /*WM_MOUSEACTIVATE*/) { m.Result = new IntPtr(3) /*NOACTIVATE*/; return; }
        base.WndProc(ref m);
    }

    int E(float v) { return (int)Math.Round(v * esc); }

    public void Mostrar(char gl, string txt, bool escuro, Rectangle area, float escala)
    {
        glifo = gl; texto = txt; esc = escala;
        cFundo = escuro ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cIcone = App.CorDeDestaque(escuro);
        if (fTexto != null) { fTexto.Dispose(); fGlifo.Dispose(); }
        fTexto = new Font("Segoe UI Semibold", E(15), GraphicsUnit.Pixel);
        fGlifo = new Font(Desenho.FonteIcones, E(20), GraphicsUnit.Pixel);
        int w = Math.Min(area.Width - E(40), TextRenderer.MeasureText(texto, fTexto).Width + E(76));
        int h = E(52);
        int x = area.X + (area.Width - w) / 2, y = area.Bottom - h - E(64);
        var ignora = Handle;
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        alfa = Suave && !Visible ? 0f : 1f; alfaAlvo = 1f;
        SetLayeredWindowAttributes(Handle, 0, (byte)(alfa * 255), 2);
        if (Suave) tFade.Start(); else tFade.Stop();
        SetWindowPos(Handle, new IntPtr(-1), x, y, w, h, 0x0010 /*NOACTIVATE*/);
        ShowWindow(Handle, 4 /*SW_SHOWNOACTIVATE*/);
        SetWindowPos(Handle, new IntPtr(-1), x, y, w, h, 0x0010);
        Invalidate();
        t.Stop(); t.Interval = Duracao; t.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(cFundo);
        if (fTexto == null) return;
        TextRenderer.DrawText(g, glifo.ToString(), fGlifo, new Rectangle(E(14), 0, E(32), Height), cIcone,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, texto, fTexto, new Rectangle(E(54), 0, Width - E(66), Height), cTexto,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

// =====================================================================
//  Atualizacao automatica (GitHub Releases), com assinatura digital
//  - so aceita um .exe assinado pela chave de quem publica (a publica fica aqui no codigo)
//  - nao manda nada sobre voce: so pergunta ao GitHub qual e a ultima versao
// =====================================================================
static class Atualizador
{
    public const string Repo = "oDethie/volt";
    // chave publica (ECDSA P-256) de quem assina as versoes
    const string ChavePublica = "RUNTMSAAAABfAui0yTWc5bwOSNPB4iHckBUPdXvxQ1Tx9I0j23IY1Wk0wiYbYbRmWPsMWnB6BbKV2TJQ3bT1lkzRsqiftt8v";
    const string NomeChaveAssinatura = "Volt-Lancamentos";

    public class Nova { public Version Versao; public string UrlExe, UrlSig; }

    public static Version Atual
    {
        get { var v = Assembly.GetExecutingAssembly().GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }
    }

    static HttpWebRequest Pedido(string url)
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
        var r = (HttpWebRequest)WebRequest.Create(url);
        r.UserAgent = "Volt-Atualizador";        // nome generico, sem dados seus
        r.Timeout = 20000; r.ReadWriteTimeout = 20000;
        r.AllowAutoRedirect = true;
        return r;
    }

    // pergunta ao GitHub a ultima versao; null se nao tiver nada mais novo (ou sem internet)
    public static Nova Procurar()
    {
        var req = Pedido("https://api.github.com/repos/" + Repo + "/releases/latest");
        req.Accept = "application/vnd.github+json";
        string json;
        using (var resp = req.GetResponse()) using (var sr = new StreamReader(resp.GetResponseStream())) json = sr.ReadToEnd();
        var d = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        object o;
        if ((d.TryGetValue("draft", out o) && o is bool && (bool)o) || (d.TryGetValue("prerelease", out o) && o is bool && (bool)o)) return null;
        Version v;
        if (!Version.TryParse(((string)d["tag_name"]).TrimStart('v', 'V'), out v)) return null;
        v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        if (v <= Atual) return null;
        var n = new Nova { Versao = v };
        foreach (var a in (System.Collections.ArrayList)d["assets"])
        {
            var x = (Dictionary<string, object>)a;
            string nome = (string)x["name"], url = (string)x["browser_download_url"];
            if (!url.StartsWith("https://github.com/" + Repo + "/releases/download/", StringComparison.OrdinalIgnoreCase)) continue;
            if (nome == "Volt.exe") n.UrlExe = url;
            else if (nome == "Volt.exe.sig") n.UrlSig = url;
        }
        return n.UrlExe != null && n.UrlSig != null ? n : null;
    }

    public static byte[] Baixar(string url, int max)
    {
        using (var resp = Pedido(url).GetResponse())
        using (var st = resp.GetResponseStream())
        using (var ms = new MemoryStream())
        {
            var buf = new byte[65536]; int k;
            while ((k = st.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, k);
                if (ms.Length > max) throw new IOException("arquivo grande demais");
            }
            return ms.ToArray();
        }
    }

    public static bool AssinaturaOk(byte[] exe, byte[] sig)
    {
        try
        {
            using (var k = CngKey.Import(Convert.FromBase64String(ChavePublica), CngKeyBlobFormat.EccPublicBlob))
            using (var e = new ECDsaCng(k)) { e.HashAlgorithm = CngAlgorithm.Sha256; return e.VerifyData(exe, sig); }
        }
        catch { return false; }
    }

    // baixa e confere tudo; devolve o .exe pronto (ou erro)
    public static byte[] BaixarConferido(Nova n, out string erro)
    {
        erro = null;
        var exe = Baixar(n.UrlExe, 8 * 1024 * 1024);
        var sig = Baixar(n.UrlSig, 4096);
        if (!AssinaturaOk(exe, sig)) { erro = "assinatura não confere"; return null; }
        return exe;
    }

    // troca o .exe: o que esta rodando pode ser renomeado (nao apagado) no Windows
    public static string Instalar(byte[] exe, string destino, Version esperada)
    {
        string dir = Path.GetDirectoryName(destino);
        string novo = Path.Combine(dir, "Volt.novo.exe"), velho = Path.Combine(dir, "Volt.velho.exe");
        try
        {
            File.WriteAllBytes(novo, exe);
            Version vf;
            if (!Version.TryParse(FileVersionInfo.GetVersionInfo(novo).FileVersion ?? "", out vf) ||
                new Version(vf.Major, vf.Minor, Math.Max(0, vf.Build)) != esperada)
            { File.Delete(novo); return "versão do arquivo não confere"; }
            if (File.Exists(velho)) File.Delete(velho);
            File.Move(destino, velho);
            try { File.Move(novo, destino); }
            catch { File.Move(velho, destino); throw; }
            return null;
        }
        catch (Exception ex) { try { if (File.Exists(novo)) File.Delete(novo); } catch { } return ex.Message; }
    }

    public static void Limpar(string exe)
    {
        string dir = Path.GetDirectoryName(exe);
        foreach (var f in new[] { "Volt.velho.exe", "Volt.novo.exe" })
            try { string c = Path.Combine(dir, f); if (File.Exists(c)) File.Delete(c); } catch { }
    }

    // ---- para quem publica ----
    public static string Assinar(string arquivo)
    {
        CngKey k = CngKey.Exists(NomeChaveAssinatura) ? CngKey.Open(NomeChaveAssinatura)
            : CngKey.Create(CngAlgorithm.ECDsaP256, NomeChaveAssinatura, new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.None, KeyUsage = CngKeyUsages.Signing });
        using (k)
        using (var e = new ECDsaCng(k))
        {
            e.HashAlgorithm = CngAlgorithm.Sha256;
            if (File.Exists(arquivo)) File.WriteAllBytes(arquivo + ".sig", e.SignData(File.ReadAllBytes(arquivo)));
            return Convert.ToBase64String(k.Export(CngKeyBlobFormat.EccPublicBlob));
        }
    }

    // teste: procura, baixa, confere e instala numa copia (sem reiniciar)
    public static IEnumerable<string> Teste()
    {
        var l = new List<string>();
        try
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            l.Add("versão atual: " + Atual);
            var n = Procurar();
            if (n == null) { l.Add("nada mais novo no GitHub"); return l; }
            l.Add("nova: " + n.Versao + "  " + n.UrlExe);
            string erro;
            var bin = BaixarConferido(n, out erro);
            l.Add(bin == null ? "RECUSADO: " + erro : "baixado e assinatura ok (" + bin.Length + " bytes)");
            if (bin == null) return l;
            var falso = (byte[])bin.Clone(); falso[falso.Length / 2] ^= 1;
            l.Add("arquivo alterado em 1 byte: " + (AssinaturaOk(falso, Baixar(n.UrlSig, 4096)) ? "ACEITO (ERRO)" : "recusado (ok)"));
            string r = Instalar(bin, exe, n.Versao);
            l.Add(r == null ? "instalado: " + FileVersionInfo.GetVersionInfo(exe).FileVersion : "falhou: " + r);
        }
        catch (Exception ex) { l.Add("ERRO " + ex.Message); }
        return l;
    }
}

// =====================================================================
//  Janela de Configuracoes (estilo Configuracoes do Windows 11)
// =====================================================================
class Configuracoes : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    readonly App app;
    float esc = 1f;
    bool escuro;
    Color cFundo, cCartao, cHover, cBorda, cTexto, cFraco, cAcento, cTextoAcento;
    Font fTitulo, fNormal, fPequena, fCab, fGlifo, fGlifoP;
    int pagina, rolagem, alturaConteudo;
    float navAtual = -1f;                       // 0 = barra lateral fechada (so icones), 1 = aberta
    readonly Timer tNav = new Timer { Interval = 15 };
    static readonly string[] Paginas = { "Geral", "Avisos", "Aparelhos" };
    static readonly char[] GlifosPag = { '\uE713', '\uEA8F', '\uE772' };
    readonly List<Linha> linhas = new List<Linha>();
    readonly List<Zona> zonas = new List<Zona>();
    Zona sobre;

    class Linha
    {
        public int Tipo;
        public string Titulo, Desc;
        public char Glifo;
        public Func<bool> Ler;
        public Action<bool> Gravar;
        public Func<string> Valor;
        public Action<Rectangle> Clicar;
    }
    class Zona { public Rectangle R; public int Pagina = -1; public Linha L; }
    const int L_CAB = 0, L_CHAVE = 1, L_BOTAO = 2, L_NOTA = 3, L_GRUPO = 4;

    int E(float v) { return (int)Math.Round(v * esc); }

    public void Atualizar() { Montar(); Invalidate(); }

    public void IrPara(int p)
    {
        if (p < 0 || p >= Paginas.Length) return;
        pagina = p; rolagem = 0; Montar(); Invalidate();
    }

    public Configuracoes(App a)
    {
        app = a;
        Text = "Configurações do Volt";
        using (var b = Desenho.Logo(32)) Icon = Desenho.IconeDe(b);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        var p = Cursor.Position;
        esc = App.EscalaEm(p);
        Preparar();
        var area = Screen.FromPoint(p).WorkingArea;
        int w = Math.Min(area.Width - E(40), E(860)), h = Math.Min(area.Height - E(40), E(620));
        Bounds = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
        MinimumSize = new Size(E(640), E(420));
        Montar();
    }

    void Preparar()
    {
        escuro = app.TemaEscuro();
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cHover = escuro ? Color.FromArgb(50, 50, 50) : Color.FromArgb(246, 246, 246);
        cBorda = escuro ? Color.FromArgb(29, 29, 29) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cAcento = App.CorDeDestaque(escuro);
        cTextoAcento = App.TextoSobre(cAcento);
        if (fTitulo != null) foreach (var f in new[] { fTitulo, fNormal, fPequena, fCab, fGlifo, fGlifoP }) f.Dispose();
        fTitulo = new Font("Segoe UI Semibold", E(28), GraphicsUnit.Pixel);
        fNormal = new Font("Segoe UI", E(14), GraphicsUnit.Pixel);
        fPequena = new Font("Segoe UI", E(12), GraphicsUnit.Pixel);
        fCab = new Font("Segoe UI Semibold", E(14), GraphicsUnit.Pixel);
        fGlifo = new Font(Desenho.FonteIcones, E(16), GraphicsUnit.Pixel);
        fGlifoP = new Font(Desenho.FonteIcones, E(12), GraphicsUnit.Pixel);
        BackColor = cFundo;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0 /*WM_DPICHANGED*/)
        {
            esc = (m.WParam.ToInt32() & 0xFFFF) / 96f;
            var r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
            Preparar();
            SetWindowPos(Handle, IntPtr.Zero, r.L, r.T, r.R - r.L, r.B - r.T, 0x0014);
            Invalidate();
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (k == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    // ---------------- conteudo de cada pagina ----------------
    void Cab(string t) { linhas.Add(new Linha { Tipo = L_CAB, Titulo = t }); }
    // titulo de grupo que abre/fecha (seta a direita)
    void Grupo(string t, bool aberto, Action alternar)
    {
        linhas.Add(new Linha { Tipo = L_GRUPO, Titulo = t, Ler = () => aberto, Clicar = r => alternar() });
    }
    void Nota(string t) { linhas.Add(new Linha { Tipo = L_NOTA, Titulo = t }); }
    void Chave(string t, string d, Func<bool> ler, Action<bool> gravar)
    {
        linhas.Add(new Linha { Tipo = L_CHAVE, Titulo = t, Desc = d, Ler = ler, Gravar = gravar });
    }
    void Botao(string t, string d, Func<string> valor, Action<Rectangle> clicar, char glifo = '\0')
    {
        linhas.Add(new Linha { Tipo = L_BOTAO, Titulo = t, Desc = d, Valor = valor, Clicar = clicar, Glifo = glifo });
    }

    ContextMenuStrip NovoMenu()
    {
        return new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
    }

    void MostrarMenu(ContextMenuStrip m, Rectangle r)
    {
        string chave = "cfg|" + pagina + "|" + (r.Y + rolagem);
        if (Alterna.FecharSeAberto(chave) || !Alterna.PodeAbrir(chave)) { m.Dispose(); return; } // o mesmo clique que fechou
        app.AplicarTema(m);
        m.Closed += (s, e) => { Alterna.Fechou(chave); Montar(); Invalidate(); };
        Alterna.Abriu(chave, m);
        m.Show(this, new Point(r.Right - E(12), r.Bottom - E(4)), ToolStripDropDownDirection.BelowLeft);
    }

    void Montar()
    {
        linhas.Clear();
        if (pagina == 0)
        {
            Cab("Barra de tarefas");
            Chave("Mostrar a % de cada aparelho", "Além do ícone do Volt, um ícone com o número de cada aparelho perto do relógio", () => app.NumerosBarra, v => app.DefinirNumeros(v));
            Chave("Fixar os ícones na barra", "Deixa os ícones sempre visíveis ao lado do relógio, em vez de escondidos no ^", () => app.FixarBandeja, v => app.AplicarFixar(v));

            Cab("Leitura");
            Botao("Ler a bateria a cada", "Com que frequência o Volt pergunta a bateria aos aparelhos. Ao abrir o painel ele também lê na hora",
                () => app.Intervalo + " min", r =>
                {
                    var m = NovoMenu();
                    foreach (var n in new[] { 1, 2, 5, 10, 15 })
                    {
                        int v = n;
                        var it = new ToolStripMenuItem(v + " min") { Checked = app.Intervalo == v };
                        it.Click += (s, e) => app.DefinirIntervalo(v);
                        m.Items.Add(it);
                    }
                    MostrarMenu(m, r);
                });

            Cab("Aparência");
            Botao("Tema", "Claro, escuro ou igual ao Windows", () => app.Tema == "claro" ? "Claro" : app.Tema == "escuro" ? "Escuro" : "Igual ao Windows", r =>
            {
                var m = NovoMenu();
                foreach (var t in new[] { "auto", "claro", "escuro" })
                {
                    string v = t;
                    var it = new ToolStripMenuItem(v == "auto" ? "Igual ao Windows" : v == "claro" ? "Claro" : "Escuro") { Checked = app.Tema == v };
                    it.Click += (s, e) => { app.Tema = v; Preparar(); OnHandleCreated(EventArgs.Empty); };
                    m.Items.Add(it);
                }
                MostrarMenu(m, r);
            });
            Chave("Animações suaves", "O painel desliza ao abrir e os aparelhos abrem e fecham devagar", () => app.Animacoes, v => { app.Animacoes = v; app.SalvarConfig(); });

            Cab("Sistema");
            Chave("Iniciar com o Windows", "Abre o Volt sozinho quando você entra no Windows", () => app.IniciaComWindows(), v => app.AlternarInicio());

            Cab("Atualizações");
            Chave("Atualizar sozinho", "A cada 12 horas confere no GitHub se há versão nova. Se houver, baixa, confere a assinatura digital e se atualiza quando você não estiver jogando em tela cheia",
                () => app.AtualizarSozinho, v => app.DefinirAtualizarSozinho(v));
            Botao(app.TemNovaVersao ? "Atualizar agora para a versão " + app.NovaVersaoTexto : "Procurar atualização agora",
                "Versão instalada: " + Atualizador.Atual.ToString(3) + (app.EstadoAtualizacao.Length > 0 ? " · " + app.EstadoAtualizacao : ""),
                () => "", r => app.ProcurarAtualizacao(true), '\uE895');
        }
        else if (pagina == 1)
        {
            Nota("Os avisos aparecem num cartãozinho perto do relógio. Durante jogos e vídeos em tela cheia eles esperam e aparecem quando você sair.");
            Cab("Bateria fraca");
            Chave("Avisar quando a bateria estiver fraca", "Um aviso quando o aparelho chega ao limite, e outro se continuar descendo", () => app.AvisarFraca, v => { app.AvisarFraca = v; app.SalvarConfig(); });
            Botao("Limite padrão", "Vale para os aparelhos sem um limite próprio", () => app.LimitePadrao + "%", r => MenuLimite(r, app.LimitePadrao, false, v => { app.LimitePadrao = v; app.SalvarConfig(); app.Mudou(); }));
            Cab("Carga completa");
            Chave("Avisar quando terminar de carregar", "Para tirar do cabo e poupar a bateria", () => app.AvisarCheio, v => { app.AvisarCheio = v; app.SalvarConfig(); });

            var lista = app.ListaAparelhos(true);
            if (lista.Count > 0)
            {
                Cab("Limite de cada aparelho");
                foreach (var ap in lista)
                {
                    var a = ap;
                    Botao(app.NomeDe(a), a.Limite < 0 ? "Usa o limite padrão" : null, () => a.Limite < 0 ? "Padrão (" + app.LimitePadrao + "%)" : a.Limite + "%",
                        r => MenuLimite(r, a.Limite, true, v => { a.Limite = v; app.SalvarConfig(); app.Mudou(); }), Volt.Glifo(a.Tipo));
                }
            }
        }
        else
        {
            var lista = app.ListaAparelhos(true);
            Nota("Aparelhos que o Volt já encontrou. Clique para dar um nome fácil, esconder do painel ou apagar o histórico.");
            if (lista.Count == 0) Nota("Nenhum aparelho encontrado ainda. Ligue o aparelho sem fio e abra o painel do Volt.");
            foreach (var ap in lista)
            {
                var a = ap;
                string desc = (a.NomeOrig != app.NomeDe(a) ? "Original: " + a.NomeOrig + " · " : "") + a.Via + (a.Pct >= 0 ? " · " + a.Pct + "%" : "") +
                              (a.Presente ? "" : " · visto " + Volt.Ha(a.Visto));
                Botao(app.NomeDe(a), desc, () => a.Oculto ? "Escondido" : "", r =>
                {
                    var m = NovoMenu();
                    var ren = new ToolStripMenuItem("Renomear…");
                    ren.Click += (s, e) => BeginInvoke(new Action(() => { app.Renomear(a); Montar(); Invalidate(); }));
                    m.Items.Add(ren);
                    if (a.Nome != null)
                    {
                        var volta = new ToolStripMenuItem("Voltar ao nome original");
                        volta.Click += (s, e) => { a.Nome = null; app.SalvarConfig(); app.Mudou(); };
                        m.Items.Add(volta);
                    }
                    var oc = new ToolStripMenuItem("Mostrar no painel") { Checked = !a.Oculto };
                    oc.Click += (s, e) => { a.Oculto = !a.Oculto; app.SalvarConfig(); app.Mudou(); };
                    m.Items.Add(oc);
                    m.Items.Add(new ToolStripSeparator());
                    var esq = new ToolStripMenuItem("Esquecer este aparelho");
                    var sim = new ToolStripMenuItem("Sim, esquecer (apaga o histórico)");
                    sim.Click += (s, e) => BeginInvoke(new Action(() => { app.Esquecer(a); Montar(); Invalidate(); }));
                    esq.DropDownItems.Add(sim);
                    esq.DropDownItems.Add(new ToolStripMenuItem("Cancelar"));
                    m.Items.Add(esq);
                    MostrarMenu(m, r);
                }, Volt.Glifo(a.Tipo));
            }
        }
    }

    void MenuLimite(Rectangle r, int atual, bool comPadrao, Action<int> definir)
    {
        var m = NovoMenu();
        if (comPadrao)
        {
            var p = new ToolStripMenuItem("Padrão (" + app.LimitePadrao + "%)") { Checked = atual < 0 };
            p.Click += (s, e) => definir(-1);
            m.Items.Add(p);
            m.Items.Add(new ToolStripSeparator());
        }
        foreach (var n in new[] { 5, 10, 15, 20, 25, 30, 40, 50 })
        {
            int v = n;
            var it = new ToolStripMenuItem(v + "%") { Checked = atual == v };
            it.Click += (s, e) => definir(v);
            m.Items.Add(it);
        }
        MostrarMenu(m, r);
    }

    static void Abrir(string cmd) { try { Process.Start(cmd); } catch { } }

    // ---------------- desenho ----------------
    const TextFormatFlags TF = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis |
                               TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    const TextFormatFlags TC = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    void Fundo(Graphics g, Rectangle r, Color c, int raio, Color? borda)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Desenho.Arred(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), raio))
        {
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            if (borda.HasValue) using (var pn = new Pen(borda.Value)) g.DrawPath(pn, p);
        }
        g.SmoothingMode = SmoothingMode.None;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(cFundo);
        zonas.Clear();
        if (navAtual < 0) navAtual = app.MenuLateralAberto ? 1f : 0f;
        int navW = E(56) + (int)Math.Round((E(240) - E(56)) * navAtual);
        bool comNomes = navAtual > 0.5f;

        // Navegacao (esquerda): botao de abrir/fechar + paginas
        int y = E(12);
        var rMenu = new Rectangle(E(8), y, E(40), E(36));
        zonas.Add(new Zona { R = rMenu, Pagina = -2 });
        if (sobre != null && sobre.Pagina == -2) Fundo(g, rMenu, cHover, E(5), null);
        TextRenderer.DrawText(g, "\uE700", fGlifo, rMenu, cTexto, TC);
        if (comNomes)
        {
            using (var logo = Desenho.Logo(E(20))) g.DrawImage(logo, E(58), y + E(8));
            TextRenderer.DrawText(g, "Volt", fCab, new Rectangle(E(86), y, navW - E(90), E(36)), cTexto, TF);
        }
        y += E(52);
        for (int i = 0; i < Paginas.Length; i++)
        {
            var r = new Rectangle(E(8), y, navW - E(16), E(40));
            zonas.Add(new Zona { R = r, Pagina = i });
            bool sel = i == pagina, hov = sobre != null && sobre.Pagina == i;
            if (sel || hov) Fundo(g, r, sel ? (escuro ? Color.FromArgb(45, 45, 45) : Color.FromArgb(234, 234, 234)) : cHover, E(5), null);
            if (sel) Fundo(g, new Rectangle(r.X, r.Y + E(12), E(3), E(16)), cAcento, E(1), null);
            TextRenderer.DrawText(g, GlifosPag[i].ToString(), fGlifo, new Rectangle(r.X + E(8), r.Y, E(24), r.Height), cTexto, TC);
            if (comNomes && r.Width - E(56) > E(30))
                TextRenderer.DrawText(g, Paginas[i], fNormal, new Rectangle(r.X + E(44), r.Y, r.Width - E(52), r.Height), cTexto, TF);
            y += E(44);
        }

        // Conteudo (direita)
        int x0 = navW + E(16), w = ClientSize.Width - x0 - E(28);
        var reg = g.Clip;
        g.SetClip(new Rectangle(navW, 0, ClientSize.Width - navW, ClientSize.Height));
        y = E(20) - rolagem;
        TextRenderer.DrawText(g, Paginas[pagina], fTitulo, new Rectangle(x0, y, w, E(44)), cTexto, TF);
        y += E(60);
        foreach (var l in linhas)
        {
            if (l.Tipo == L_GRUPO)
            {
                y += E(10);
                var rg = new Rectangle(x0 - E(6), y, w + E(12), E(36));
                zonas.Add(new Zona { R = rg, L = l });
                if (sobre != null && sobre.L == l) Fundo(g, rg, cHover, E(5), null);
                TextRenderer.DrawText(g, l.Titulo, fCab, new Rectangle(x0 + E(2), y, w - E(40), rg.Height), cTexto, TF);
                TextRenderer.DrawText(g, (l.Ler() ? '\uE70E' : '\uE70D').ToString(), fGlifoP,
                    new Rectangle(rg.Right - E(36), y, E(28), rg.Height), cFraco, TC);
                y += E(40);
                continue;
            }
            if (l.Tipo == L_CAB)
            {
                y += E(14);
                TextRenderer.DrawText(g, l.Titulo, fCab, new Rectangle(x0 + E(2), y, w, E(26)), cTexto, TF);
                y += E(32);
                continue;
            }
            if (l.Tipo == L_NOTA)
            {
                var tam = TextRenderer.MeasureText(g, l.Titulo, fNormal, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(x0 + E(2), y, w, tam.Height + E(4)), cFraco,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                y += tam.Height + E(16);
                continue;
            }
            // a explicacao quebra em mais linhas quando precisa (o cartao cresce junto)
            int txPre = l.Glifo != '\0' ? x0 + E(54) : x0 + E(20);
            int descW = Math.Max(E(80), w - (txPre - x0) - E(200));
            int descH = l.Desc == null ? 0 : TextRenderer.MeasureText(g, l.Desc, fPequena, new Size(descW, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height;
            int h = l.Desc != null ? E(50) + descH : E(54);
            var r = new Rectangle(x0, y, w, h);
            zonas.Add(new Zona { R = r, L = l });
            bool hov = sobre != null && sobre.L == l;
            Fundo(g, r, hov ? cHover : cCartao, E(6), cBorda);
            int tx = x0 + E(20);
            if (l.Glifo != '\0')
            {
                TextRenderer.DrawText(g, l.Glifo.ToString(), fGlifo, new Rectangle(x0 + E(14), y, E(28), h), cTexto, TC);
                tx = x0 + E(54);
            }
            int dirW = E(200);
            if (l.Desc != null)
            {
                TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(tx, y + E(13), w - (tx - x0) - dirW, E(22)), cTexto, TF);
                TextRenderer.DrawText(g, l.Desc, fPequena, new Rectangle(tx, y + E(36), descW, descH + E(2)), cFraco,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top);
            }
            else TextRenderer.DrawText(g, l.Titulo, fNormal, new Rectangle(tx, y, w - (tx - x0) - dirW, h), cTexto, TF);

            if (l.Tipo == L_CHAVE)
            {
                bool on = l.Ler();
                var rs = new Rectangle(r.Right - E(60), y + (h - E(20)) / 2, E(40), E(20));
                Interruptor(g, rs, on, hov);
                TextRenderer.DrawText(g, on ? "Ligado" : "Desligado", fNormal, new Rectangle(rs.Left - E(110), y, E(98), h), cTexto,
                    TF & ~TextFormatFlags.Left | TextFormatFlags.Right);
            }
            else
            {
                char seta = l.Glifo == '' ? '' : '';
                if (l.Glifo != '') TextRenderer.DrawText(g, seta.ToString(), fGlifoP, new Rectangle(r.Right - E(40), y, E(24), h), cFraco, TC);
                string v = l.Valor();
                if (v.Length > 0)
                    TextRenderer.DrawText(g, v, fNormal, new Rectangle(r.Right - E(44) - dirW, y, dirW, h), cFraco,
                        TF & ~TextFormatFlags.Left | TextFormatFlags.Right);
            }
            y += h + E(4);
        }
        alturaConteudo = y + rolagem + E(24);
        g.Clip = reg;

        if (alturaConteudo > ClientSize.Height)
        {
            float hb = Math.Max(E(30), ClientSize.Height * ClientSize.Height / (float)alturaConteudo);
            float yb = (ClientSize.Height - hb) * rolagem / (float)(alturaConteudo - ClientSize.Height);
            Fundo(g, new Rectangle(ClientSize.Width - E(8), (int)yb + E(4), E(4), (int)hb - E(8)), cFraco, E(2), null);
        }
    }

    void Interruptor(Graphics g, Rectangle r, bool on, bool hov)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
        using (var p = Desenho.Arred(rf, rf.Height / 2))
        {
            if (on) using (var b = new SolidBrush(cAcento)) g.FillPath(b, p);
            else using (var pn = new Pen(cFraco)) g.DrawPath(pn, p);
        }
        float d = r.Height - E(hov ? 8 : 10);
        float cx = on ? r.Right - r.Height / 2f : r.X + r.Height / 2f, cy = r.Y + r.Height / 2f;
        using (var b = new SolidBrush(on ? cTextoAcento : cFraco)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
        g.SmoothingMode = SmoothingMode.None;
    }

    // ---------------- mouse ----------------
    Zona ZonaEm(Point p)
    {
        foreach (var z in zonas) if (z.R.Contains(p)) return z;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var z = ZonaEm(e.Location);
        bool mudou = (z == null) != (sobre == null) || (z != null && (z.L != sobre.L || z.Pagina != sobre.Pagina));
        sobre = z;
        if (mudou) { Cursor = z != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (sobre != null) { sobre = null; Invalidate(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int max = Math.Max(0, alturaConteudo - ClientSize.Height);
        rolagem = Math.Max(0, Math.Min(max, rolagem - e.Delta * E(48) / 120));
        sobre = null;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        rolagem = Math.Max(0, Math.Min(rolagem, Math.Max(0, alturaConteudo - ClientSize.Height)));
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        var z = ZonaEm(e.Location);
        if (z == null) return;
        if (z.Pagina == -2)
        {
            app.MenuLateralAberto = !app.MenuLateralAberto;
            app.SalvarConfig();
            if (!app.Animacoes) { navAtual = app.MenuLateralAberto ? 1f : 0f; Invalidate(); return; }
            if (!tNav.Enabled)
            {
                tNav.Tick -= PassoNav; tNav.Tick += PassoNav;
                tNav.Start();
            }
            return;
        }
        if (z.Pagina >= 0) { pagina = z.Pagina; rolagem = 0; Montar(); Invalidate(); return; }
        var l = z.L;
        if (l.Tipo == L_CHAVE)
        {
            l.Gravar(!l.Ler());
            app.SalvarConfig();
            app.PainelMudou();
            Montar();      // refaz a lista na hora (opcoes que dependem desta aparecem/somem)
            sobre = null;
            Invalidate();
            return;
        }
        if ((l.Tipo == L_BOTAO || l.Tipo == L_GRUPO) && l.Clicar != null)
        {
            l.Clicar(z.R);
            if (!IsDisposed) { Montar(); Invalidate(); }
        }
    }

    void PassoNav(object s, EventArgs e)
    {
        float alvo = app.MenuLateralAberto ? 1f : 0f;
        navAtual += (alvo - navAtual) * 0.3f;
        if (Math.Abs(alvo - navAtual) < 0.02f) { navAtual = alvo; tNav.Stop(); }
        Invalidate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        app.ConferirPainel();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Montar(); // volta de uma janelinha (atalho, nome): mostra os valores novos
        Invalidate();
    }
}
