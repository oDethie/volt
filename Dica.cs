// Volt - cartaozinho que aparece ao passar o mouse num aparelho da faixa da barra
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Dica : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);

    readonly App app;
    Estado a;
    float esc = 1f;
    bool escuro;
    Color cFundo, cTexto, cFraco;
    Font fNome, fPct, fPeq, fGlifo;

    public Dica(App ap)
    {
        app = ap;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;                                 // sombra
            cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/ | 0x20 /*TRANSPARENT: nao pega o mouse*/;
            return cp;
        }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0) { m.Result = IntPtr.Zero; return; }
        if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; }
        if (m.Msg == 0x0084 /*NCHITTEST*/) { m.Result = new IntPtr(-1); return; }   // transparente ao mouse
        base.WndProc(ref m);
    }

    int E(float v) { return (int)Math.Round(v * esc); }

    // r: retangulo do aparelho na barra (coordenadas de tela); barraTopo: topo da barra
    public void Mostrar(Estado ap, Rectangle r, int barraTopo, bool barraEmCima, float escala)
    {
        a = ap; esc = escala;
        escuro = app.TemaEscuro();
        cFundo = escuro ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(165, 165, 165) : Color.FromArgb(96, 96, 96);
        if (fNome != null) foreach (var f in new[] { fNome, fPct, fPeq, fGlifo }) f.Dispose();
        fNome = new Font("Segoe UI Semibold", E(14), GraphicsUnit.Pixel);
        fPct = new Font("Segoe UI Semibold", E(22), GraphicsUnit.Pixel);
        fPeq = new Font("Segoe UI", E(12), GraphicsUnit.Pixel);
        fGlifo = new Font(Desenho.FonteIcones, E(16), GraphicsUnit.Pixel);
        int w = E(a.Partes != null ? 290 : 240), h = E(a.Partes != null ? 156 : 118);
        int wn = TextRenderer.MeasureText(app.NomeDe(a), fNome).Width + E(60);
        if (wn > w) w = Math.Min(E(340), wn);
        var tela = Screen.FromPoint(new Point(r.X + r.Width / 2, r.Y)).Bounds;
        int x = Math.Max(tela.Left + E(8), Math.Min(r.X + r.Width / 2 - w / 2, tela.Right - w - E(8)));
        int y = barraEmCima ? r.Bottom + E(10) : barraTopo - h - E(10);
        var ignora = Handle;
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        SetWindowPos(Handle, new IntPtr(-1), x, y, w, h, 0x0010);
        ShowWindow(Handle, 4 /*SHOWNOACTIVATE*/);
        Invalidate();
    }

    public void Esconder() { if (Visible) Hide(); }

    const TextFormatFlags TF = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (a == null || fNome == null) return;
        Desenhar(e.Graphics, ClientSize, a, app.NomeDe(a), app.LimiteDe(a), app.PrevisaoTexto(a), escuro, esc);
    }

    // desenho do cartao (tambem usado pela previa --testedica)
    public static void Desenhar(Graphics g, Size tam, Estado a, string nome, int limite, string prev, bool escuro, float esc)
    {
        Func<float, int> E = v => (int)Math.Round(v * esc);
        Color cFundo = escuro ? Color.FromArgb(44, 44, 44) : Color.FromArgb(249, 249, 249);
        Color cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        Color cFraco = escuro ? Color.FromArgb(165, 165, 165) : Color.FromArgb(96, 96, 96);
        g.Clear(cFundo);
        using (var fNome = new Font("Segoe UI Semibold", E(14), GraphicsUnit.Pixel))
        using (var fPct = new Font("Segoe UI Semibold", E(22), GraphicsUnit.Pixel))
        using (var fPeq = new Font("Segoe UI", E(12), GraphicsUnit.Pixel))
        using (var fGlifo = new Font(Desenho.FonteIcones, E(16), GraphicsUnit.Pixel))
        {
            int x = E(14), W = tam.Width - E(28);
            int nivel = Volt.Nivel(a, limite);
            Color cor = Volt.CorNivel(nivel, escuro);
            TextRenderer.DrawText(g, Volt.Glifo(a.Tipo).ToString(), fGlifo, new Rectangle(x - E(2), E(10), E(22), E(24)), cTexto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, nome, fNome, new Rectangle(x + E(26), E(10), W - E(26), E(24)), cTexto, TF);
            string pct = a.Pct < 0 ? "–" : a.Pct + "%";
            int pw = TextRenderer.MeasureText(g, pct, fPct, Size.Empty, TextFormatFlags.NoPadding).Width;
            TextRenderer.DrawText(g, pct, fPct, new Rectangle(x, E(38), pw + E(4), E(30)), nivel >= 1 ? cor : cTexto, TF);
            TextRenderer.DrawText(g, prev, fPeq, new Rectangle(x + pw + E(10), E(42), W - pw - E(10), E(24)), cFraco, TF);
            var rb = new Rectangle(x, E(74), W, E(5));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Desenho.Arred(rb, E(2.5f))) using (var b = new SolidBrush(escuro ? Color.FromArgb(70, 70, 70) : Color.FromArgb(222, 222, 222))) g.FillPath(b, p);
            if (a.Pct > 0) using (var p = Desenho.Arred(new RectangleF(rb.X, rb.Y, Math.Max(E(5), rb.Width * a.Pct / 100f), rb.Height), E(2.5f))) using (var b = new SolidBrush(cor)) g.FillPath(b, p);
            g.SmoothingMode = SmoothingMode.None;
            int yv = E(86);
            if (a.Partes != null)
            {
                Volt.Partes(g, new Rectangle(x, E(88), W, E(28)), a.Partes, fPeq, cTexto, cFraco, escuro ? Color.FromArgb(70, 70, 70) : Color.FromArgb(222, 222, 222), escuro, limite, esc);
                yv = E(124);
            }
            string via = a.Via + (string.IsNullOrEmpty(a.Detalhe) ? "" : " · " + a.Detalhe) + (a.Aproximado ? " · aproximado" : "") + " · lido " + Volt.Ha(a.Visto);
            TextRenderer.DrawText(g, via, fPeq, new Rectangle(x, yv, W, E(20)), cFraco, TF);
        }
    }
}
