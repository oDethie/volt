// Volt - painel (abre ao clicar no icone): lista com barras, grafico ao abrir um aparelho
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

class Painel : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint cor, byte alfa, uint f);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    readonly App app;
    float esc = 1f;
    bool escuro;
    Color cFundo, cCartao, cHover, cBorda, cTexto, cFraco, cTrilho, cDiv;
    Font fTitulo, fNormal, fNegrito, fPct, fPequena, fGlifo, fGlifoG;
    int largura, alturaConteudo, rolagem, bordaBarra;
    Rectangle area;
    readonly List<Zona> zonas = new List<Zona>();
    Zona sobre;
    readonly HashSet<string> abertos = new HashSet<string>();
    readonly Dictionary<string, float> abertura = new Dictionary<string, float>();
    readonly Timer tAnim = new Timer { Interval = 15 };
    readonly Timer tRelogio = new Timer { Interval = 30000 };   // "atualizado ha X min" com o painel aberto
    float opac = 1f, desl;
    bool fechando;
    DateTime escondido = DateTime.MinValue;

    class Zona { public Rectangle R; public int Tipo; public Estado A; }
    const int Z_LER = 1, Z_CFG = 2, Z_CARTAO = 3;

    public Painel(App a)
    {
        app = a;
        Text = "Volt";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        tAnim.Tick += (s, e) => Passo();
        tRelogio.Tick += (s, e) => Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;                 // sombra
            cp.ExStyle |= 0x80 | 0x80000 | 0x8;       // sem Alt+Tab, camada (para aparecer suave), sempre no topo
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0 /*WM_DPICHANGED*/) { m.Result = IntPtr.Zero; return; }
        base.WndProc(ref m);
    }

    int E(float v) { return (int)Math.Round(v * esc); }

    void Preparar()
    {
        escuro = app.TemaEscuro();
        cFundo = escuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
        cCartao = escuro ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
        cHover = escuro ? Color.FromArgb(52, 52, 52) : Color.FromArgb(246, 246, 246);
        cBorda = escuro ? Color.FromArgb(29, 29, 29) : Color.FromArgb(229, 229, 229);
        cTexto = escuro ? Color.FromArgb(242, 242, 242) : Color.FromArgb(27, 27, 27);
        cFraco = escuro ? Color.FromArgb(160, 160, 160) : Color.FromArgb(96, 96, 96);
        cTrilho = escuro ? Color.FromArgb(70, 70, 70) : Color.FromArgb(222, 222, 222);
        cDiv = escuro ? Color.FromArgb(60, 60, 60) : Color.FromArgb(225, 225, 225);
        if (fTitulo != null) foreach (var f in new[] { fTitulo, fNormal, fNegrito, fPct, fPequena, fGlifo, fGlifoG }) f.Dispose();
        fTitulo = new Font("Segoe UI Semibold", E(16), GraphicsUnit.Pixel);
        fNormal = new Font("Segoe UI", E(14), GraphicsUnit.Pixel);
        fNegrito = new Font("Segoe UI Semibold", E(14), GraphicsUnit.Pixel);
        fPct = new Font("Segoe UI Semibold", E(17), GraphicsUnit.Pixel);
        fPequena = new Font("Segoe UI", E(12), GraphicsUnit.Pixel);
        fGlifo = new Font(Desenho.FonteIcones, E(16), GraphicsUnit.Pixel);
        fGlifoG = new Font(Desenho.FonteIcones, E(40), GraphicsUnit.Pixel);
        BackColor = cFundo;
        if (IsHandleCreated)
        {
            int sim = escuro ? 1 : 0, round = 2;
            try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
            try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }
    }

    public void TemaMudou() { if (Visible) { Preparar(); Invalidate(); } }

    // ---------------- abrir / fechar ----------------
    public void Alternar()
    {
        if (Visible && !fechando) { Esconder(); return; }
        if ((DateTime.Now - escondido).TotalMilliseconds < 350) return;   // o clique que fechou
        Mostrar();
    }

    public void Mostrar()
    {
        var p = Cursor.Position;
        var tela = Screen.FromPoint(p);
        esc = App.EscalaEm(tela.Bounds.Location + new Size(tela.Bounds.Width / 2, tela.Bounds.Height / 2));
        Preparar();
        area = App.AreaUtil(tela, out bordaBarra);
        largura = E(360);
        rolagem = 0;
        fechando = false;
        Montar(null);
        desl = app.Animacoes ? E(12) : 0;
        opac = app.Animacoes ? 0f : 1f;
        Posicionar();
        if (!Visible) Show();
        SetLayeredWindowAttributes(Handle, 0, (byte)(opac * 255), 2);
        int sim = escuro ? 1 : 0, round = 2;
        try { DwmSetWindowAttribute(Handle, 20, ref sim, 4); } catch { }
        try { DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        Activate(); SetForegroundWindow(Handle);
        if (app.Animacoes) tAnim.Start();
        tRelogio.Start();
        if ((DateTime.Now - app.UltimaLeitura).TotalSeconds > 20) app.LerAgora();   // abriu: le na hora
        Invalidate();
    }

    public void Esconder()
    {
        if (!Visible) return;
        if (app.Animacoes && !fechando) { fechando = true; tAnim.Start(); return; }
        EsconderAgora();
    }

    void EsconderAgora()
    {
        fechando = false;
        tAnim.Stop(); tRelogio.Stop();
        Hide();
        escondido = DateTime.Now;
        app.Aparar();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!app.MenuAberto) Esconder();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (k == Keys.Escape) { Esconder(); return true; }
        if (k == Keys.F5) { app.LerAgora(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    void Posicionar()
    {
        int h = Math.Min(alturaConteudo, area.Height - E(24));
        int x = area.Right - largura - E(12), y = area.Bottom - h - E(12) + (int)desl;
        if (bordaBarra == 1) y = area.Top + E(12) - (int)desl;
        if (bordaBarra == 2) x = area.Left + E(12);
        rolagem = Math.Max(0, Math.Min(rolagem, alturaConteudo - h));
        SetWindowPos(Handle, new IntPtr(-1), x, y, largura, h, 0x0010);
    }

    public void Atualizar()
    {
        if (!Visible) return;
        Montar(null); Posicionar(); Invalidate();
    }

    void Passo()
    {
        bool mexeu = false, layout = false;
        float alvoO = fechando ? 0f : 1f;
        if (opac != alvoO)
        {
            opac += (alvoO - opac) * (fechando ? 0.4f : 0.3f);
            if (Math.Abs(alvoO - opac) < 0.03f) opac = alvoO;
            SetLayeredWindowAttributes(Handle, 0, (byte)(opac * 255), 2); mexeu = true;
        }
        float alvoD = fechando ? E(10) : 0f;
        if (desl != alvoD) { desl += (alvoD - desl) * 0.3f; if (Math.Abs(alvoD - desl) < 0.5f) desl = alvoD; layout = true; }
        foreach (var k in new List<string>(abertura.Keys))
        {
            float f = abertura[k], alvo = abertos.Contains(k) ? 1f : 0f;
            if (f == alvo) continue;
            f += (alvo - f) * 0.28f;
            if (Math.Abs(alvo - f) < 0.02f) f = alvo;
            abertura[k] = f; layout = true;
        }
        if (layout) { Montar(null); Posicionar(); Invalidate(); mexeu = true; }
        if (!mexeu) { tAnim.Stop(); if (fechando) EsconderAgora(); }
    }

    float Abertura(string k) { float f; return abertura.TryGetValue(k, out f) ? f : 0f; }

    // ---------------- desenho + mapa de cliques ----------------
    const TextFormatFlags TF = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    const TextFormatFlags TD = TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    const TextFormatFlags TC = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    void Fundo(Graphics g, Rectangle r, Color c, int raio)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Desenho.Arred(new RectangleF(r.X, r.Y, r.Width, r.Height), raio)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = SmoothingMode.None;
    }

    void Montar(Graphics g)
    {
        zonas.Clear();
        int W = largura, pad = E(12);
        int y = pad - rolagem;

        // cabecalho
        var rLer = new Rectangle(W - pad - E(68), y, E(32), E(32));
        var rCfg = new Rectangle(W - pad - E(32), y, E(32), E(32));
        zonas.Add(new Zona { R = rLer, Tipo = Z_LER });
        zonas.Add(new Zona { R = rCfg, Tipo = Z_CFG });
        if (g != null)
        {
            int lg = E(22);
            using (var logo = Desenho.Logo(lg)) g.DrawImage(logo, pad + E(4), y + (E(32) - lg) / 2);
            int x = pad + E(4) + lg + E(10);
            TextRenderer.DrawText(g, "Volt", fTitulo, new Rectangle(x, y, E(60), E(32)), cTexto, TF);
            int tw = TextRenderer.MeasureText(g, "Volt", fTitulo, Size.Empty, TextFormatFlags.NoPadding).Width;
            string sub = app.Lendo ? "lendo…" : app.UltimaLeitura == DateTime.MinValue ? "" : "atualizado " + Volt.Ha(app.UltimaLeitura);
            TextRenderer.DrawText(g, "  ·  " + sub, fPequena, new Rectangle(x + tw, y, rLer.Left - x - tw - E(4), E(32)), cFraco, TF);
            foreach (var z in new[] { rLer, rCfg })
            {
                bool hov = sobre != null && sobre.R == z;
                if (hov) Fundo(g, z, cHover, E(4));
                TextRenderer.DrawText(g, z == rLer ? "" : "", fGlifo, z, app.Lendo && z == rLer ? cFraco : cTexto, TC);
            }
        }
        y += E(32) + E(10);
        if (g != null) using (var p = new Pen(cDiv)) g.DrawLine(p, pad + E(4), y, W - pad - E(4), y);
        y += E(10);

        var lista = app.ListaAparelhos(false);
        if (lista.Count == 0)
        {
            if (g != null)
            {
                TextRenderer.DrawText(g, "", fGlifoG, new Rectangle(0, y + E(10), W, E(56)), cFraco, TC);
                TextRenderer.DrawText(g, app.Lendo && app.UltimaLeitura == DateTime.MinValue ? "Procurando aparelhos…" : "Nenhum aparelho sem fio encontrado", fNegrito,
                    new Rectangle(pad, y + E(70), W - pad * 2, E(22)), cTexto, TC);
                TextRenderer.DrawText(g, "Ligue o mouse, fone ou controle sem fio", fPequena, new Rectangle(pad, y + E(94), W - pad * 2, E(18)), cFraco, TC);
            }
            y += E(124);
        }
        foreach (var a in lista) y = Cartao(g, a, y, pad, W);
        y += pad - E(4);
        alturaConteudo = y + rolagem;
    }

    int Cartao(Graphics g, Estado a, int y, int pad, int W)
    {
        float f = Abertura(a.Chave);
        int hBase = E(76), hGraf = (int)Math.Round(E(118) * f);
        var r = new Rectangle(pad, y, W - pad * 2, hBase + hGraf);
        zonas.Add(new Zona { R = r, Tipo = Z_CARTAO, A = a });
        if (g != null)
        {
            int lim = app.LimiteDe(a);
            int nivel = Volt.Nivel(a, lim);
            Color cor = Volt.CorNivel(nivel, escuro);
            bool hov = sobre != null && sobre.A == a;
            bool apagado = a.Dormindo;
            Fundo(g, r, hov ? cHover : cCartao, E(6));
            if (nivel == 1 || nivel == 2) Fundo(g, new Rectangle(r.X, r.Y + E(10), E(3), hBase - E(20)), cor, E(1.5f));   // barrinha lateral: precisa de atencao
            Color ct = apagado ? cFraco : cTexto;
            int xi = r.X + E(14);
            // linha 1: icone, nome e %
            var r1 = new Rectangle(xi, r.Y + E(10), r.Width - E(28), E(24));
            TextRenderer.DrawText(g, Volt.Glifo(a.Tipo).ToString(), fGlifo, new Rectangle(r1.X - E(2), r1.Y, E(22), r1.Height), ct, TC);
            string pct = a.Pct < 0 ? "–" : a.Pct + "%";
            int pw = TextRenderer.MeasureText(g, pct, fPct, Size.Empty, TextFormatFlags.NoPadding).Width;
            Color cPct = apagado ? cFraco : nivel == 1 || nivel == 2 ? cor : cTexto;
            TextRenderer.DrawText(g, pct, fPct, new Rectangle(r1.Right - pw - E(2), r1.Y, pw + E(2), r1.Height), cPct, TD);
            int xr = r1.Right - pw - E(6);
            if (a.Carregando) { TextRenderer.DrawText(g, "", fGlifo, new Rectangle(xr - E(20), r1.Y, E(20), r1.Height), Volt.Azul(escuro), TC); xr -= E(22); }
            TextRenderer.DrawText(g, app.NomeDe(a), fNormal, new Rectangle(r1.X + E(28), r1.Y, xr - r1.X - E(32), r1.Height), ct, TF);
            // linha 2: barra
            var rb = new Rectangle(xi, r.Y + E(40), r.Width - E(28), E(6));
            Fundo(g, rb, cTrilho, E(3));
            if (a.Pct > 0) Fundo(g, new Rectangle(rb.X, rb.Y, Math.Max(E(6), rb.Width * a.Pct / 100), rb.Height), apagado ? cFraco : cor, E(3));
            // linha 3: origem e previsao
            var r3 = new Rectangle(xi, r.Y + E(50), r.Width - E(28), E(18));
            string prev = Previsao(a);
            int pvw = prev.Length == 0 ? 0 : TextRenderer.MeasureText(g, prev, fPequena, Size.Empty, TextFormatFlags.NoPadding).Width + E(2);
            if (pvw > 0) TextRenderer.DrawText(g, prev, fPequena, new Rectangle(r3.Right - pvw, r3.Y, pvw, r3.Height), cFraco, TD);
            string via = a.Via + (a.Aproximado ? " · nível aproximado" : "");
            TextRenderer.DrawText(g, via, fPequena, new Rectangle(r3.X, r3.Y, r3.Width - pvw - E(8), r3.Height), cFraco, TF);
            if (hGraf > E(4))
            {
                var est = g.Save();
                g.SetClip(new Rectangle(r.X, r.Y + hBase, r.Width, hGraf));
                Grafico(g, a, new Rectangle(xi, r.Y + hBase, r.Width - E(28), E(118)), cor);
                g.Restore(est);
            }
        }
        return y + hBase + hGraf + E(8);
    }

    string Previsao(Estado a)
    {
        if (a.Dormindo) return "dormindo · visto " + Volt.Ha(a.Visto);
        double h = Historico.Previsao(a);
        if (a.Carregando) return a.Pct >= 100 ? "carregado" : h > 0 ? "cheio em " + Volt.Duracao(h) : "carregando";
        return h > 0 ? "dura " + Volt.Duracao(h) : "";
    }

    // grafico das ultimas 48 h (ou desde o primeiro ponto)
    void Grafico(Graphics g, Estado a, Rectangle r, Color cor)
    {
        var todos = Historico.De(a.Chave);
        var ini = DateTime.Now.AddHours(-48);
        var pts = new List<Ponto>();
        foreach (var p in todos) if (p.T >= ini) pts.Add(p);
        var ra = new Rectangle(r.X, r.Y + E(8), r.Width, r.Height - E(34));
        using (var pn = new Pen(cDiv) { DashStyle = DashStyle.Dot })
            foreach (var v in new[] { 0, 50, 100 }) { int yy = ra.Bottom - ra.Height * v / 100; g.DrawLine(pn, ra.X, yy, ra.Right, yy); }
        if (pts.Count < 2)
        {
            TextRenderer.DrawText(g, "O gráfico aparece depois de algumas leituras", fPequena, ra, cFraco, TC);
        }
        else
        {
            DateTime t0 = pts[0].T, t1 = DateTime.Now;
            double total = Math.Max(1, (t1 - t0).TotalMinutes);
            var lista = new List<PointF>();
            foreach (var p in pts) lista.Add(new PointF(ra.X + (float)((p.T - t0).TotalMinutes / total * ra.Width), ra.Bottom - ra.Height * p.Pct / 100f));
            lista.Add(new PointF(ra.Right, lista[lista.Count - 1].Y));   // mantem o ultimo valor ate agora
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gp = new GraphicsPath())
            {
                gp.AddLines(lista.ToArray());
                gp.AddLine(ra.Right, ra.Bottom, ra.X + (float)0, ra.Bottom);
                gp.CloseFigure();
                using (var b = new LinearGradientBrush(new Point(0, ra.Top), new Point(0, ra.Bottom + 1), Color.FromArgb(70, cor), Color.FromArgb(5, cor))) g.FillPath(b, gp);
            }
            using (var pn = new Pen(cor, Math.Max(1.5f, E(2))) { LineJoin = LineJoin.Round }) g.DrawLines(pn, lista.ToArray());
            g.SmoothingMode = SmoothingMode.None;
            string desde = Volt.Ha(t0);
            TextRenderer.DrawText(g, desde, fPequena, new Rectangle(r.X, ra.Bottom + E(6), r.Width / 2, E(18)), cFraco, TF);
        }
        TextRenderer.DrawText(g, "agora", fPequena, new Rectangle(r.X + r.Width / 2, ra.Bottom + E(6), r.Width / 2, E(18)), cFraco, TD);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(cFundo);
        if (fTitulo == null) return;
        Montar(g);
        if (alturaConteudo > ClientSize.Height)
        {
            float frac = ClientSize.Height / (float)alturaConteudo;
            float hb = Math.Max(E(24), ClientSize.Height * frac);
            float yb = (ClientSize.Height - hb) * rolagem / (float)(alturaConteudo - ClientSize.Height);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(cFraco)) using (var p = Desenho.Arred(new RectangleF(ClientSize.Width - E(5), yb + E(2), E(3), hb - E(4)), E(1.5f))) g.FillPath(b, p);
        }
    }

    // ---------------- mouse ----------------
    Zona ZonaEm(Point p) { for (int i = zonas.Count - 1; i >= 0; i--) if (zonas[i].R.Contains(p)) return zonas[i]; return null; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var z = ZonaEm(e.Location);
        bool mudou = (z == null) != (sobre == null) || (z != null && (z.Tipo != sobre.Tipo || z.A != sobre.A));
        sobre = z;
        if (mudou) { Cursor = z == null ? Cursors.Default : Cursors.Hand; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (sobre != null) { sobre = null; Invalidate(); } }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int max = Math.Max(0, alturaConteudo - ClientSize.Height);
        if (max == 0) return;
        rolagem = Math.Max(0, Math.Min(max, rolagem - e.Delta * E(40) / 120));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var z = ZonaEm(e.Location);
        if (z == null) return;
        if (e.Button == MouseButtons.Right) { if (z.Tipo == Z_CARTAO) MenuAparelho(z.A); return; }
        if (e.Button != MouseButtons.Left) return;
        if (z.Tipo == Z_LER) { app.LerAgora(); return; }
        if (z.Tipo == Z_CFG) { app.AbrirConfiguracoes(-1); return; }
        if (z.Tipo == Z_CARTAO)
        {
            string k = z.A.Chave;
            if (!abertos.Remove(k)) abertos.Add(k);
            if (!abertura.ContainsKey(k)) abertura[k] = 0f;
            if (app.Animacoes) tAnim.Start();
            else { abertura[k] = abertos.Contains(k) ? 1f : 0f; Atualizar(); }
        }
    }

    void MenuAparelho(Estado a)
    {
        string chave = "ap|" + a.Chave;
        if (Alterna.FecharSeAberto(chave) || !Alterna.PodeAbrir(chave)) return;
        var m = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
        m.Items.Add(new ToolStripLabel(app.NomeDe(a)) { Tag = "fraco" });
        var ren = new ToolStripMenuItem("Renomear…");
        ren.Click += (s, e) => BeginInvoke(new Action(() => { Esconder(); app.Renomear(a); Mostrar(); }));
        m.Items.Add(ren);
        var lim = new ToolStripMenuItem("Avisar com bateria em");
        var pad = new ToolStripMenuItem("Padrão (" + app.LimitePadrao + "%)") { Checked = a.Limite < 0 };
        pad.Click += (s, e) => { a.Limite = -1; app.SalvarConfig(); app.Mudou(); };
        lim.DropDownItems.Add(pad);
        foreach (var n in new[] { 5, 10, 15, 20, 25, 30, 40, 50 })
        {
            int v = n;
            var it = new ToolStripMenuItem(v + "%") { Checked = a.Limite == v };
            it.Click += (s, e) => { a.Limite = v; app.SalvarConfig(); app.Mudou(); };
            lim.DropDownItems.Add(it);
        }
        m.Items.Add(lim);
        var nb = new ToolStripMenuItem("Sempre mostrar na barra de tarefas") { Checked = a.NaBarra };
        nb.Click += (s, e) => app.FixarNaBarra(a, !a.NaBarra);
        m.Items.Add(nb);
        var oc = new ToolStripMenuItem("Esconder do painel");
        oc.Click += (s, e) => { a.Oculto = true; app.SalvarConfig(); app.Mudou(); };
        m.Items.Add(oc);
        app.AplicarTema(m);
        app.MenuAberto = true;
        m.Closed += (s, e) => { Alterna.Fechou(chave); app.MenuAberto = false; app.ConferirPainel(); };
        Alterna.Abriu(chave, m);
        App.MostrarDentro(m, Cursor.Position);
    }
}
