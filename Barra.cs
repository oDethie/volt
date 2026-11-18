// Volt - faixa na barra de tarefas: "icone do aparelho + 52%", ao lado dos icones do relogio.
// E uma janela filha da barra do Windows (como fazem os medidores de bateria); so existe quando ha algo a mostrar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class Barra : NativeWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr pai, IntPtr depois, string classe, string nome);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, IntPtr pDst, ref TAM tam, IntPtr src, ref PONTO pSrc, int cor, ref BLEND b, int f);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    delegate void EventoWin(IntPtr hook, uint ev, IntPtr h, int obj, int filho, uint thr, uint t);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, EventoWin f, uint pid, uint thr, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct PONTO { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct TAM { public int W, H; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alfa, Formato; }

    readonly App app;
    IntPtr barra, notif, gancho;
    EventoWin chamada;                  // guardado: o Windows chama de volta
    List<Estado> itens = new List<Estado>();
    Size tam;
    bool claro;
    readonly Timer tPos = new Timer { Interval = 120 };

    public Barra(App a)
    {
        app = a;
        tPos.Tick += (s, e) => { tPos.Stop(); Posicionar(); };
    }

    public bool Existe { get { return Handle != IntPtr.Zero; } }

    // mostra estes aparelhos; lista vazia = some. false se a barra nao permite (ex.: barra vertical)
    public bool Mostrar(List<Estado> lista, bool barraClara)
    {
        itens = lista; claro = barraClara;
        if (lista.Count == 0) { Fechar(); return true; }
        if (!Criar()) return false;
        Desenhar();
        return true;
    }

    public void Fechar()
    {
        if (gancho != IntPtr.Zero) { UnhookWinEvent(gancho); gancho = IntPtr.Zero; }
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    // a barra foi recriada (Explorer reiniciou): comeca de novo
    public void Recriar() { var l = itens; Fechar(); barra = IntPtr.Zero; if (l.Count > 0) Mostrar(l, claro); }

    bool Criar()
    {
        if (Handle != IntPtr.Zero && IsWindow(barra) && IsWindow(notif)) return true;
        Fechar();
        barra = FindWindow("Shell_TrayWnd", null);
        notif = barra == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(barra, IntPtr.Zero, "TrayNotifyWnd", null);
        if (barra == IntPtr.Zero || notif == IntPtr.Zero) return false;
        RECT rb; GetWindowRect(barra, out rb);
        if (rb.B - rb.T > rb.R - rb.L) return false;   // barra vertical: usa os icones da bandeja
        var cp = new CreateParams
        {
            Caption = "Volt.Barra",
            Parent = barra,
            Style = 0x40000000 /*CHILD*/ | 0x10000000 /*VISIBLE*/ | 0x04000000 /*CLIPSIBLINGS*/,
            ExStyle = 0x80000 /*LAYERED*/ | 0x08000000 /*NOACTIVATE*/
        };
        try { CreateHandle(cp); } catch (Exception ex) { Registro.Erro(new Exception("faixa: " + Marshal.GetLastWin32Error(), ex)); return false; }
        if (Handle == IntPtr.Zero) return false;
        // acompanha quando a area do relogio muda de tamanho (icones entram/saem) - sem ficar conferindo
        uint pid; GetWindowThreadProcessId(barra, out pid);
        chamada = (h, ev, hw, obj, filho, thr, t) => { if (hw == notif || hw == barra) { tPos.Stop(); tPos.Start(); } };
        gancho = SetWinEventHook(0x800B /*LOCATIONCHANGE*/, 0x800B, IntPtr.Zero, chamada, pid, 0, 0 /*OUTOFCONTEXT*/);
        return true;
    }

    float Escala { get { uint d = 96; try { d = GetDpiForWindow(barra); } catch { } return (d == 0 ? 96 : d) / 96f; } }

    void Desenhar()
    {
        if (Handle == IntPtr.Zero) return;
        RECT rb; GetWindowRect(barra, out rb);
        using (var bmp = Renderizar(itens, claro, Escala, rb.B - rb.T, app.LimiteDe))
        {
            tam = bmp.Size;
            Posicionar();
            Aplicar(bmp);
        }
    }

    // desenha a faixa (tambem usado pela previa --testefaixa)
    public static Bitmap Renderizar(List<Estado> itens, bool claro, float e, int h, Func<Estado, int> limiteDe)
    {
        Color texto = claro ? Color.FromArgb(27, 27, 27) : Color.White;
        int pad = (int)Math.Round(8 * e), gap = (int)Math.Round(14 * e), gTam = (int)Math.Round(16 * e), gGap = (int)Math.Round(5 * e);
        using (var f = new Font("Segoe UI Semibold", 13.5f * e, GraphicsUnit.Pixel))
        using (var medir = Graphics.FromHwnd(IntPtr.Zero))
        {
            medir.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var larg = new List<int>();
            int w = pad;
            foreach (var a in itens)
            {
                string t = a.Pct < 0 ? "?" : a.Pct + "%";
                int tw = (int)Math.Ceiling(medir.MeasureString(t, f, PointF.Empty, StringFormat.GenericTypographic).Width);
                int iw = gTam + gGap + tw + (a.Carregando ? (int)Math.Round(14 * e) : 0);
                larg.Add(iw);
                w += iw + gap;
            }
            w += pad - gap;
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(1, 0, 0, 0));       // quase transparente: assim recebe os cliques
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                int x = pad;
                for (int i = 0; i < itens.Count; i++)
                {
                    var a = itens[i];
                    int nivel = Volt.Nivel(a, limiteDe(a));
                    Color cTexto = nivel == 2 ? Volt.Vermelho(!claro) : texto;
                    using (var gl = Desenho.Glifo(Volt.Glifo(a.Tipo), gTam, cTexto, 0.95f)) g.DrawImage(gl, x, (h - gTam) / 2);
                    string t = a.Pct < 0 ? "?" : a.Pct + "%";
                    using (var b = new SolidBrush(cTexto))
                    {
                        var sz = g.MeasureString(t, f, PointF.Empty, StringFormat.GenericTypographic);
                        g.DrawString(t, f, b, x + gTam + gGap, (h - sz.Height) / 2f - 0.5f * e, StringFormat.GenericTypographic);
                    }
                    if (a.Carregando)
                    {
                        int bt = (int)Math.Round(12 * e);
                        using (var gl = Desenho.Glifo('', bt, Volt.Azul(!claro), 1f)) g.DrawImage(gl, x + larg[i] - bt, (h - bt) / 2);
                    }
                    x += larg[i] + gap;
                }
            }
            return bmp;
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

    // canto esquerdo da area do relogio, centralizado na altura da barra (coordenadas dentro da barra)
    PONTO PosicaoTela()
    {
        RECT rb, rn; GetWindowRect(barra, out rb); GetWindowRect(notif, out rn);
        return new PONTO { X = rn.L - rb.L - tam.Width - (int)Math.Round(4 * Escala), Y = 0 };
    }

    void Posicionar()
    {
        if (Handle == IntPtr.Zero || !IsWindow(notif)) return;
        var p = PosicaoTela();
        SetWindowPos(Handle, IntPtr.Zero, p.X, p.Y, tam.Width, tam.Height, 0x0010 /*NOACTIVATE*/ | 0x0004 /*NOZORDER*/);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0202 /*LBUTTONUP*/) { app.AbrirPainel(); return; }
        if (m.Msg == 0x0205 /*RBUTTONUP*/) { app.MenuEm(Cursor.Position); return; }
        if (m.Msg == 0x0021 /*MOUSEACTIVATE*/) { m.Result = new IntPtr(3); return; }
        base.WndProc(ref m);
    }
}
