// Volt - faixa na barra de tarefas: "icone do aparelho + 52%", ao lado do relogio.
// Uma janela filha em cada barra (monitor principal e os outros); so existe quando ha algo a mostrar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Barra
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string classe, string nome);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    delegate void EventoWin(IntPtr hook, uint ev, IntPtr h, int obj, int filho, uint thr, uint t);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, EventoWin f, uint pid, uint thr, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);

    readonly App app;
    readonly List<Faixa> faixas = new List<Faixa>();
    List<Estado> itens = new List<Estado>();
    bool claro;
    IntPtr gancho;
    EventoWin chamada;                 // guardado: o Windows chama de volta
    readonly Timer tPos = new Timer { Interval = 150 };

    public Barra(App a)
    {
        app = a;
        tPos.Tick += (s, e) => { tPos.Stop(); foreach (var f in faixas) f.Posicionar(); };
    }

    // mostra estes aparelhos em todas as barras; lista vazia = some. false se nenhuma barra permite
    public bool Mostrar(List<Estado> lista, bool barraClara)
    {
        itens = lista; claro = barraClara;
        if (lista.Count == 0) { Fechar(); return true; }
        var barras = new List<IntPtr>();
        IntPtr h = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
        if (h != IntPtr.Zero) barras.Add(h);
        h = IntPtr.Zero;
        while ((h = FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) barras.Add(h);
        foreach (var f in new List<Faixa>(faixas))
            if (!barras.Contains(f.BarraPai) || !IsWindow(f.BarraPai)) { f.Fechar(); faixas.Remove(f); }
        bool alguma = false;
        foreach (var b in barras)
        {
            var f = faixas.Find(x => x.BarraPai == b);
            if (f == null) { f = new Faixa(app, b, b == barras[0]); if (!f.Criar()) continue; faixas.Add(f); }
            f.Desenhar(itens, claro);
            alguma = true;
        }
        if (alguma && gancho == IntPtr.Zero)
        {
            // acompanha quando a barra/relogio mudam de tamanho (icones entram/saem) - sem ficar conferindo
            uint pid; GetWindowThreadProcessId(barras[0], out pid);
            chamada = (hk, ev, hw, obj, filho, thr, t) => { if (obj == 0) { tPos.Stop(); tPos.Start(); } };
            gancho = SetWinEventHook(0x800B /*LOCATIONCHANGE*/, 0x800B, IntPtr.Zero, chamada, pid, 0, 0);
        }
        return alguma;
    }

    public void Fechar()
    {
        if (gancho != IntPtr.Zero) { UnhookWinEvent(gancho); gancho = IntPtr.Zero; }
        foreach (var f in faixas) f.Fechar();
        faixas.Clear();
    }

    // a barra foi recriada (Explorer reiniciou): comeca de novo
    public void Recriar() { var l = itens; Fechar(); if (l.Count > 0) Mostrar(l, claro); }

    // desenha a faixa (tambem usado pela previa --testefaixa): icone do aparelho + numero ao lado
    public static Bitmap Renderizar(List<Estado> itens, bool claro, float e, int h, Func<Estado, int> limiteDe)
    {
        Color texto = claro ? Color.FromArgb(27, 27, 27) : Color.FromArgb(240, 240, 240);
        // proporcional a altura da barra (48 px a 100%)
        e = h / 48f;
        int pad = (int)Math.Round(4 * e), gap = (int)Math.Round(10 * e), gTam = (int)Math.Round(13 * e), gGap = (int)Math.Round(4 * e);
        using (var f = new Font("Segoe UI", 10.5f * e, GraphicsUnit.Pixel))
        using (var medir = Graphics.FromHwnd(IntPtr.Zero))
        {
            medir.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var larg = new List<float>();
            float w = pad;
            foreach (var a in itens)
            {
                string t = a.Pct < 0 ? "?" : a.Pct + "%";
                float tw = medir.MeasureString(t, f, PointF.Empty, StringFormat.GenericTypographic).Width + (a.Carregando ? 11 * e : 0);
                larg.Add(tw);
                w += gTam + gGap + tw + gap;
            }
            w += pad - gap;
            var bmp = new Bitmap(Math.Max(1, (int)Math.Ceiling(w)), Math.Max(1, h), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(1, 0, 0, 0));       // quase transparente: assim recebe os cliques
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float x = pad;
                for (int i = 0; i < itens.Count; i++)
                {
                    var a = itens[i];
                    int nivel = Volt.Nivel(a, limiteDe(a));
                    Color c = nivel == 2 ? Volt.Vermelho(!claro) : texto;   // fraco: vermelho
                    using (var gl = Desenho.Glifo(Volt.Glifo(a.Tipo), gTam, c, 0.95f)) g.DrawImage(gl, (int)Math.Round(x), (h - gTam) / 2);
                    x += gTam + gGap;
                    string t = a.Pct < 0 ? "?" : a.Pct + "%";
                    var sz = g.MeasureString(t, f, PointF.Empty, StringFormat.GenericTypographic);
                    using (var b = new SolidBrush(c)) g.DrawString(t, f, b, x, (h - sz.Height) / 2f, StringFormat.GenericTypographic);
                    if (a.Carregando)
                    {
                        int bt = (int)Math.Round(10 * e);
                        using (var gl = Desenho.Glifo('\uE945', bt, Volt.Azul(!claro), 1f)) g.DrawImage(gl, (int)(x + sz.Width + 1 * e), (h - bt) / 2);
                    }
                    x += larg[i] + gap;
                }
            }
            return bmp;
        }
    }
}

// a faixa de uma barra de tarefas
class Faixa : NativeWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string classe, string nome);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, IntPtr pDst, ref TAM tam, IntPtr src, ref PONTO pSrc, int cor, ref BLEND b, int f);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct PONTO { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct TAM { public int W, H; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alfa, Formato; }

    readonly App app;
    public readonly IntPtr BarraPai;
    readonly bool principal;
    Size tam;

    public Faixa(App a, IntPtr barra, bool principal) { app = a; BarraPai = barra; this.principal = principal; }

    public bool Criar()
    {
        RECT rb; GetWindowRect(BarraPai, out rb);
        if (rb.B - rb.T > rb.R - rb.L) return false;   // barra vertical: nao cabe
        var cp = new CreateParams
        {
            Caption = "Volt.Barra",
            Parent = BarraPai,
            Style = 0x40000000 /*CHILD*/ | 0x04000000 /*CLIPSIBLINGS*/,
            ExStyle = 0x80000 /*LAYERED*/ | 0x08000000 /*NOACTIVATE*/
        };
        try { CreateHandle(cp); } catch (Exception ex) { Registro.Erro(ex); return false; }
        return Handle != IntPtr.Zero;
    }

    public void Fechar() { if (Handle != IntPtr.Zero) DestroyHandle(); }

    float Escala { get { uint d = 96; try { d = GetDpiForWindow(BarraPai); } catch { } return (d == 0 ? 96 : d) / 96f; } }

    public void Desenhar(List<Estado> itens, bool claro)
    {
        if (Handle == IntPtr.Zero) return;
        RECT rb; GetWindowRect(BarraPai, out rb);
        using (var bmp = Barra.Renderizar(itens, claro, Escala, rb.B - rb.T, app.LimiteDe))
        {
            tam = bmp.Size;
            Posicionar();
            Aplicar(bmp);
        }
    }

    void Aplicar(Bitmap bmp)
    {
        IntPtr tela = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(tela);
        IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0)), velho = SelectObject(mem, hb);
        try
        {
            var t = new TAM { W = bmp.Width, H = bmp.Height };
            var src = new PONTO();
            var bl = new BLEND { Op = 0, Flags = 0, Alfa = 255, Formato = 1 /*AC_SRC_ALPHA*/ };
            UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, ref t, mem, ref src, 0, ref bl, 2 /*ULW_ALPHA*/);
        }
        finally { SelectObject(mem, velho); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, tela); }
    }

    // logo a esquerda do relogio / area de notificacao; se ali ja tiver botoes de programas, esconde (nunca cobre nada)
    public void Posicionar()
    {
        if (Handle == IntPtr.Zero || !IsWindow(BarraPai)) return;
        float e = Escala;
        RECT rb; GetWindowRect(BarraPai, out rb);
        int direita = rb.R - (int)Math.Round(6 * e);
        IntPtr ref1 = principal ? FindWindowEx(BarraPai, IntPtr.Zero, "TrayNotifyWnd", null) : FindWindowEx(BarraPai, IntPtr.Zero, "ClockButton", null);
        RECT rr;
        if (ref1 != IntPtr.Zero && IsWindowVisible(ref1) && GetWindowRect(ref1, out rr)) direita = rr.L - (int)Math.Round(4 * e);
        int esquerda = direita - tam.Width;
        // botoes dos programas: nao pode encostar neles
        int limite = rb.L;
        foreach (var cls in new[] { "MSTaskListWClass", "MSTaskSwWClass", "ReBarWindow32" })
        {
            IntPtr t = FindWindowEx(BarraPai, IntPtr.Zero, cls, null);
            if (t == IntPtr.Zero && cls == "MSTaskListWClass") t = FindDeep(BarraPai, cls);
            RECT rt;
            if (t != IntPtr.Zero && IsWindowVisible(t) && GetWindowRect(t, out rt) && rt.R <= direita) limite = Math.Max(limite, rt.R);
        }
        if (esquerda < limite + (int)Math.Round(4 * e)) { ShowWindow(Handle, 0); return; }
        SetWindowPos(Handle, IntPtr.Zero /*HWND_TOP*/, esquerda - rb.L, 0, tam.Width, tam.Height, 0x0010 /*NOACTIVATE*/ | 0x0040 /*SHOWWINDOW*/);
    }

    static IntPtr FindDeep(IntPtr pai, string cls)
    {
        IntPtr w = FindWindowEx(pai, IntPtr.Zero, "WorkerW", null);
        return w == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(w, IntPtr.Zero, cls, null);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0202 /*LBUTTONUP*/) { app.AbrirPainel(); return; }
        if (m.Msg == 0x0205 /*RBUTTONUP*/) { app.MenuEm(Cursor.Position); return; }
        if (m.Msg == 0x0021 /*MOUSEACTIVATE*/) { m.Result = new IntPtr(3); return; }
        base.WndProc(ref m);
    }
}
