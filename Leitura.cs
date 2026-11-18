// Volt - leitura da bateria dos aparelhos sem fio (somente leitura: nunca grava nada nos aparelhos)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

enum Tipo { Outro, Mouse, Fone, Teclado, Controle, Celular }

class Aparelho
{
    public string Chave;        // identidade estavel entre leituras
    public string Nome;
    public string Via;          // "Bluetooth", "Receptor 2.4 GHz", "Cabo USB"...
    public Tipo Tipo;
    public int Pct = -1;        // 0..100 ou -1
    public bool Carregando;
    public string Detalhe;      // ex.: "E 80% · D 75% · estojo 40%"
    public bool Aproximado;     // o aparelho so informa niveis (baixo, medio, cheio)
    public int[] Partes;        // fones: esquerdo, direito, estojo (-1 = sem informacao)
}

static class Leitor
{
    // Le todos os aparelhos. Bloqueia por ate ~2 s: chamar fora da thread da tela.
    public static List<Aparelho> LerTudo()
    {
        var l = new List<Aparelho>();
        var hids = Hid.Listar();
        try { Logitech.Ler(hids, l); } catch (Exception ex) { Registro.Erro(ex); }
        try { PlayStation.Ler(hids, l); } catch (Exception ex) { Registro.Erro(ex); }
        try { AttackShark.Ler(hids, l); } catch (Exception ex) { Registro.Erro(ex); }
        int xbox = 0;
        try { xbox = Controles.LerWgi(l); } catch (Exception ex) { Registro.Erro(ex); }
        if (xbox == 0) try { Controles.LerXInput(l); } catch (Exception ex) { Registro.Erro(ex); }
        try { Celular.Ler(l); } catch (Exception ex) { Registro.Erro(ex); }
        try { GalaxyBuds.Ler(l); } catch (Exception ex) { Registro.Erro(ex); }
        try { Bluetooth.Ler(l); } catch (Exception ex) { Registro.Erro(ex); }
        try { HidPadrao.Ler(hids, l); } catch (Exception ex) { Registro.Erro(ex); }
        return l;
    }

    public static Tipo TipoPeloNome(string n)
    {
        n = (n ?? "").ToLowerInvariant();
        if (n.Contains("mouse") || n.Contains("superlight") || n.Contains("mx master") || n.Contains("g502") || n.Contains("g pro x") || n.Contains("viper")) return Tipo.Mouse;
        if (n.Contains("keyboard") || n.Contains("teclado") || n.Contains("keys")) return Tipo.Teclado;
        if (n.Contains("bud") || n.Contains("pod") || n.Contains("fone") || n.Contains("headset") || n.Contains("headphone") || n.Contains("earbud") || n.Contains("wh-") || n.Contains("wf-") || n.Contains("jbl") || n.Contains("arctis") || n.Contains("cloud")) return Tipo.Fone;
        if (n.Contains("controller") || n.Contains("controle") || n.Contains("gamepad") || n.Contains("dualsense") || n.Contains("xbox")) return Tipo.Controle;
        return Tipo.Outro;
    }
}

static class Registro
{
    public static void Erro(Exception ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Volt.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\r\n\r\n");
        }
        catch { }
    }
}

// =====================================================================
//  HID: lista as colecoes e conversa com elas (com tempo limite)
// =====================================================================
class ColecaoHid
{
    public string Caminho;
    public ushort Vid, Pid, Pagina, Uso;
    public int TamEntrada, TamSaida, TamRecurso;
    public string Produto;
    // identifica o aparelho fisico (as colecoes do mesmo receptor so mudam "&colNN")
    public string Fisico
    {
        get
        {
            string p = Caminho.ToLowerInvariant();
            int a = p.IndexOf('#'), b = a < 0 ? -1 : p.IndexOf('#', a + 1), e = b < 0 ? -1 : p.IndexOf('#', b + 1);
            if (e < 0) return p;
            string hw = p.Substring(a + 1, b - a - 1), inst = p.Substring(b + 1, e - b - 1);
            int col = hw.IndexOf("&col"); if (col >= 0) hw = hw.Remove(col, Math.Min(6, hw.Length - col));
            int amp = inst.LastIndexOf('&'); if (amp >= 0) inst = inst.Substring(0, amp);
            return hw + "#" + inst;
        }
    }
}

static class Hid
{
    [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h, ref ATRIBUTOS a);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, out CAPS c);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle h, StringBuilder b, int tam);
    [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr w, int f);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, int i, ref SP_INTERFACE dados);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref SP_INTERFACE dados, IntPtr det, int tam, out int req, IntPtr dd);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(string n, uint acesso, uint comp, IntPtr seg, uint disp, uint flags, IntPtr modelo);

    [StructLayout(LayoutKind.Sequential)] struct ATRIBUTOS { public int Tam; public ushort Vid, Pid, Versao; }
    [StructLayout(LayoutKind.Sequential)] struct SP_INTERFACE { public int Tam; public Guid Classe; public int Flags; public IntPtr Res; }
    [StructLayout(LayoutKind.Sequential)]
    struct CAPS
    {
        public ushort Uso, Pagina, TamEntrada, TamSaida, TamRecurso;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Res;
        public ushort a, b, c, d, e, f, g, h, i, j;
    }

    public static List<ColecaoHid> Listar()
    {
        var l = new List<ColecaoHid>();
        Guid g; HidD_GetHidGuid(out g);
        IntPtr s = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x02 | 0x10 /*PRESENT|DEVICEINTERFACE*/);
        if (s == new IntPtr(-1)) return l;
        try
        {
            for (int i = 0; ; i++)
            {
                var d = new SP_INTERFACE { Tam = Marshal.SizeOf(typeof(SP_INTERFACE)) };
                if (!SetupDiEnumDeviceInterfaces(s, IntPtr.Zero, ref g, i, ref d)) break;
                int req;
                SetupDiGetDeviceInterfaceDetail(s, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                if (req <= 0) continue;
                IntPtr buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(s, ref d, buf, req, out req, IntPtr.Zero)) continue;
                    string caminho = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                    var c = Info(caminho);
                    if (c != null) l.Add(c);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(s); }
        return l;
    }

    static ColecaoHid Info(string caminho)
    {
        using (var h = CreateFile(caminho, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))   // sem acesso de leitura/escrita: so consulta
        {
            if (h.IsInvalid) return null;
            var a = new ATRIBUTOS { Tam = Marshal.SizeOf(typeof(ATRIBUTOS)) };
            if (!HidD_GetAttributes(h, ref a)) return null;
            var c = new ColecaoHid { Caminho = caminho, Vid = a.Vid, Pid = a.Pid };
            IntPtr pp;
            if (HidD_GetPreparsedData(h, out pp))
            {
                CAPS caps;
                if (HidP_GetCaps(pp, out caps) == 0x110000)
                {
                    c.Pagina = caps.Pagina; c.Uso = caps.Uso;
                    c.TamEntrada = caps.TamEntrada; c.TamSaida = caps.TamSaida; c.TamRecurso = caps.TamRecurso;
                }
                HidD_FreePreparsedData(pp);
            }
            var sb = new StringBuilder(128);
            if (HidD_GetProductString(h, sb, sb.Capacity * 2)) c.Produto = sb.ToString().Trim();
            return c;
        }
    }
}

// Conexao com uma colecao HID (leitura e escrita com tempo limite)
class DispHid : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteFile(SafeFileHandle h, IntPtr b, int n, out int esc, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle h, IntPtr b, int n, out int lidos, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetOverlappedResult(SafeFileHandle h, IntPtr ov, out int n, bool esperar);
    [DllImport("kernel32.dll")] static extern bool CancelIo(SafeFileHandle h);
    [DllImport("kernel32.dll")] static extern IntPtr CreateEvent(IntPtr a, bool manual, bool ini, string n);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool ResetEvent(IntPtr h);
    [DllImport("hid.dll")] static extern bool HidD_GetFeature(SafeFileHandle h, byte[] b, int n);
    public bool LerRecurso(byte id, int tam) { if (!Ok || tam <= 0) return false; var b = new byte[tam]; b[0] = id; return HidD_GetFeature(h, b, tam); }

    readonly SafeFileHandle h;
    readonly IntPtr ov, ev, buf;
    readonly int tamEntrada;
    bool lendo;
    public bool Ok { get { return !h.IsInvalid; } }

    public DispHid(ColecaoHid c) : this(c, false) { }
    // somenteLeitura: abre sem permissao de escrita (o Windows recusa qualquer envio ao aparelho)
    public DispHid(ColecaoHid c, bool somenteLeitura)
    {
        h = Hid.CreateFile(c.Caminho, somenteLeitura ? 0x80000000 : 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000 /*OVERLAPPED*/, IntPtr.Zero);
        tamEntrada = Math.Max(c.TamEntrada, 64);
        ov = Marshal.AllocHGlobal(IntPtr.Size == 8 ? 32 : 20);
        for (int i = 0; i < (IntPtr.Size == 8 ? 32 : 20); i++) Marshal.WriteByte(ov, i, 0);
        ev = CreateEvent(IntPtr.Zero, true, false, null);
        Marshal.WriteIntPtr(ov, IntPtr.Size == 8 ? 24 : 16, ev);
        buf = Marshal.AllocHGlobal(tamEntrada);
    }

    public bool Escrever(byte[] dados)
    {
        if (!Ok) return false;
        // escrita sobreposta: usa um OVERLAPPED proprio
        IntPtr o = Marshal.AllocHGlobal(IntPtr.Size == 8 ? 32 : 20);
        IntPtr e = CreateEvent(IntPtr.Zero, true, false, null);
        IntPtr bw = Marshal.AllocHGlobal(dados.Length);   // fora do coletor de lixo: a escrita pode terminar depois
        try
        {
            Marshal.Copy(dados, 0, bw, dados.Length);
            for (int i = 0; i < (IntPtr.Size == 8 ? 32 : 20); i++) Marshal.WriteByte(o, i, 0);
            Marshal.WriteIntPtr(o, IntPtr.Size == 8 ? 24 : 16, e);
            int n;
            if (!WriteFile(h, bw, dados.Length, out n, o))
            {
                if (Marshal.GetLastWin32Error() != 997) return false;
                if (WaitForSingleObject(e, 500) != 0) { CancelIo(h); GetOverlappedResult(h, o, out n, true); return false; }
                if (!GetOverlappedResult(h, o, out n, false)) return false;
            }
            return true;
        }
        finally { CloseHandle(e); Marshal.FreeHGlobal(o); Marshal.FreeHGlobal(bw); }
    }

    // le um relatorio; null se nada chegou no tempo
    public byte[] Ler(int ms)
    {
        if (!Ok) return null;
        int n;
        if (!lendo)
        {
            ResetEvent(ev);
            if (ReadFile(h, buf, tamEntrada, out n, ov)) return Copiar(n);
            if (Marshal.GetLastWin32Error() != 997) return null;
            lendo = true;
        }
        if (WaitForSingleObject(ev, (uint)ms) != 0) return null;   // continua pendente para a proxima chamada
        lendo = false;
        if (!GetOverlappedResult(h, ov, out n, false)) return null;
        return Copiar(n);
    }

    byte[] Copiar(int n) { var r = new byte[n]; Marshal.Copy(buf, r, 0, n); return r; }

    public void Dispose()
    {
        if (lendo) { CancelIo(h); int n; GetOverlappedResult(h, ov, out n, true); }
        h.Dispose();
        CloseHandle(ev);
        Marshal.FreeHGlobal(ov); Marshal.FreeHGlobal(buf);
    }
}

// =====================================================================
//  Logitech HID++ (receptores Unifying / Bolt / Lightspeed, cabo e Bluetooth)
//  So perguntas de leitura: ping, descobrir recursos, nome, tipo e bateria.
// =====================================================================
static class Logitech
{
    const byte SW = 0x0B;   // nosso "id de software" (as respostas de G HUB / Options+ vem com o deles)

    class Canal : IDisposable
    {
        public DispHid Curto, Longo;
        public void Dispose() { if (Curto != null) Curto.Dispose(); if (Longo != null) Longo.Dispose(); }

        // 1 = resposta, 0 = erro, -1 = sem resposta
        public int Pedir(byte idx, byte b2, byte b3, byte[] param, out byte[] resp, out byte erro, int ms = 800)
        {
            resp = null; erro = 0;
            bool longo = Longo != null && Longo.Ok;
            var msg = new byte[longo ? 20 : 7];
            msg[0] = (byte)(longo ? 0x11 : 0x10); msg[1] = idx; msg[2] = b2; msg[3] = b3;
            if (param != null) for (int i = 0; i < param.Length && 4 + i < msg.Length; i++) msg[4 + i] = param[i];
            if (!(longo ? Longo : Curto).Escrever(msg)) return -1;
            var fim = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < fim)
            {
                foreach (var d in new[] { Longo, Curto })
                {
                    if (d == null || !d.Ok) continue;
                    var r = d.Ler(10);
                    if (r == null || r.Length < 7 || (r[0] != 0x10 && r[0] != 0x11) || r[1] != idx) continue;
                    if (r[2] == b2 && r[3] == b3) { resp = new byte[r.Length - 4]; Array.Copy(r, 4, resp, 0, resp.Length); return 1; }
                    if ((r[2] == 0xFF || r[2] == 0x8F) && r[3] == b2 && r[4] == b3) { erro = r[5]; return 0; }
                }
            }
            return -1;
        }

        public byte[] Chamar(byte idx, byte recurso, int fn, params byte[] param)
        {
            byte[] r; byte e;
            return Pedir(idx, recurso, (byte)((fn << 4) | SW), param, out r, out e) == 1 ? r : null;
        }

        public byte Recurso(byte idx, ushort id)
        {
            var r = Chamar(idx, 0x00, 0, (byte)(id >> 8), (byte)id);
            return r != null && r.Length > 0 ? r[0] : (byte)0;
        }
    }

    public static void Ler(List<ColecaoHid> hids, List<Aparelho> saida)
    {
        // agrupa as colecoes HID++ por aparelho fisico
        var grupos = new Dictionary<string, ColecaoHid[]>();
        foreach (var c in hids)
        {
            if (c.Vid != 0x046D) continue;
            bool curto = c.Pagina == 0xFF00 && c.Uso == 0x0001 && c.TamSaida == 7;
            bool longo = ((c.Pagina == 0xFF00 && c.Uso == 0x0002) || (c.Pagina == 0xFF43 && c.Uso == 0x0202)) && c.TamSaida == 20;
            if (!curto && !longo) continue;
            ColecaoHid[] g;
            if (!grupos.TryGetValue(c.Fisico, out g)) grupos[c.Fisico] = g = new ColecaoHid[2];
            if (curto) g[0] = c; else g[1] = c;
        }
        foreach (var kv in grupos)
        {
            var c0 = kv.Value[1] ?? kv.Value[0];
            using (var ch = new Canal { Curto = kv.Value[0] != null ? new DispHid(kv.Value[0]) : null, Longo = kv.Value[1] != null ? new DispHid(kv.Value[1]) : null })
            {
                string produto = c0.Produto ?? "";
                bool receptor = produto.IndexOf("Receiver", StringComparison.OrdinalIgnoreCase) >= 0;
                string cam = c0.Caminho.ToLowerInvariant();
                bool bt = cam.Contains("{00001812-") || cam.Contains("{00001124-");
                byte[] r; byte e;
                List<byte> indices;
                if (c0.Pid == 0xC54F) indices = new List<byte> { 1 };
                else if (bt || (!receptor && ch.Pedir(0xFF, 0x00, (byte)(0x10 | SW), new byte[] { 0, 0, 0x5A }, out r, out e) == 1 && r.Length >= 3 && r[0] >= 2))
                    indices = new List<byte> { 0xFF };
                else indices = new List<byte> { 1, 2, 3, 4, 5, 6 };

                foreach (var idx in indices)
                {
                    var a = new Aparelho
                    {
                        Chave = "logi:" + c0.Pid.ToString("X4") + ":" + idx,
                        Nome = idx == 0xFF && produto.Length > 0 ? produto : "Aparelho Logitech",
                        Via = bt ? "Bluetooth" : idx == 0xFF ? "Cabo USB" : "Receptor 2.4 GHz"
                    };
                    int pr = ch.Pedir(idx, 0x00, (byte)(0x10 | SW), new byte[] { 0, 0, 0x5A }, out r, out e, 2000);
                    bool achou = false;
                    if (pr == 1 && r.Length >= 3 && r[0] >= 2)
                    {
                        NomeETipo(ch, idx, a);
                        achou = Bateria20(ch, idx, a);
                        string unidade = achou ? Unidade(ch, idx) : null;
                        if (!string.IsNullOrEmpty(unidade)) a.Chave = "logi:" + unidade;
                    }
                    else if (pr == 0 && e == 0x01) achou = Bateria10(ch, idx, a);   // aparelho HID++ 1.0
                    if (achou)
                    {
                        if (a.Tipo == Tipo.Outro) a.Tipo = Leitor.TipoPeloNome(a.Nome);
                        if (!saida.Exists(x => x.Chave == a.Chave)) saida.Add(a);
                    }
                }
            }
        }
    }

    static bool Bateria20(Canal ch, byte idx, Aparelho a)
    {
        byte f; byte[] p;
        if ((f = ch.Recurso(idx, 0x1004)) != 0 && (p = ch.Chamar(idx, f, 1)) != null && p.Length >= 3)   // bateria unificada
        {
            int soc = p[0], nivel = p[1], st = p[2];
            if (soc > 0 && soc <= 100) a.Pct = soc;
            else if (nivel != 0) { a.Pct = (nivel & 8) != 0 ? 100 : (nivel & 4) != 0 ? 60 : (nivel & 2) != 0 ? 20 : 5; a.Aproximado = true; }
            a.Carregando = st >= 1 && st <= 3;
            return true;
        }
        if ((f = ch.Recurso(idx, 0x1000)) != 0 && (p = ch.Chamar(idx, f, 0)) != null && p.Length >= 3)   // nivel de bateria
        {
            a.Carregando = p[2] >= 1 && p[2] <= 4;
            a.Pct = p[0] <= 100 ? p[0] : -1;
            if (a.Pct == 0 && p[2] == 3) a.Pct = 100;
            return true;
        }
        if ((f = ch.Recurso(idx, 0x1001)) != 0 && (p = ch.Chamar(idx, f, 0)) != null && p.Length >= 3)   // tensao
        {
            int mv = (p[0] << 8) | p[1];
            a.Carregando = (p[2] & 0x80) != 0;
            if (mv > 0) { a.Pct = PorTensao(mv); a.Aproximado = true; }
            return true;
        }
        if ((f = ch.Recurso(idx, 0x1F20)) != 0 && (p = ch.Chamar(idx, f, 0)) != null && p.Length >= 3)   // fones G (ADC)
        {
            int mv = (p[0] << 8) | p[1];
            a.Carregando = p[2] == 3;
            if (mv > 0) { a.Pct = PorTensaoFone(mv); a.Aproximado = true; }
            if (a.Tipo == Tipo.Outro) a.Tipo = Tipo.Fone;
            return true;
        }
        return false;
    }

    static bool Bateria10(Canal ch, byte idx, Aparelho a)
    {
        byte[] p; byte e;
        if (ch.Pedir(idx, 0x81, 0x0D, null, out p, out e) == 1 && p.Length >= 3 && p[0] > 0 && p[0] <= 100)
        { a.Pct = p[0]; a.Carregando = p[2] >= 0x21 && p[2] <= 0x26; return true; }
        if (ch.Pedir(idx, 0x81, 0x07, null, out p, out e) == 1 && p.Length >= 2 && p[0] > 0)
        {
            int[] t = { 0, 5, 10, 20, 35, 50, 75, 100 };
            a.Pct = t[Math.Min(7, (int)p[0])]; a.Aproximado = true;
            a.Carregando = p[1] >= 0x21 && p[1] <= 0x26;
            return true;
        }
        return false;
    }

    static void NomeETipo(Canal ch, byte idx, Aparelho a)
    {
        byte f = ch.Recurso(idx, 0x0005);
        if (f == 0) return;
        var p = ch.Chamar(idx, f, 0);
        if (p != null && p.Length > 0)
        {
            int tam = p[0];
            var sb = new StringBuilder();
            while (sb.Length < tam)
            {
                var q = ch.Chamar(idx, f, 1, (byte)sb.Length);
                if (q == null || q.Length == 0) break;
                int antes = sb.Length;
                foreach (var b in q) { if (sb.Length >= tam || b == 0) break; sb.Append((char)b); }
                if (sb.Length == antes) break;
            }
            string n = sb.ToString().Trim();
            if (n.Length > 0) a.Nome = n.StartsWith("Logitech") || n.StartsWith("G ") || n.StartsWith("MX") || n.StartsWith("PRO") ? n : "Logitech " + n;
        }
        p = ch.Chamar(idx, f, 2);
        if (p != null && p.Length > 0)
            switch (p[0])
            {
                case 0: case 2: a.Tipo = Tipo.Teclado; break;
                case 3: case 4: case 5: a.Tipo = Tipo.Mouse; break;
                case 8: a.Tipo = Tipo.Fone; break;
                case 0x0B: case 0x0C: a.Tipo = Tipo.Controle; break;
            }
    }

    static string Unidade(Canal ch, byte idx)
    {
        byte f = ch.Recurso(idx, 0x0003);
        if (f == 0) return null;
        var p = ch.Chamar(idx, f, 0);
        if (p == null || p.Length < 5 || (p[1] | p[2] | p[3] | p[4]) == 0) return null;
        return p[1].ToString("X2") + p[2].ToString("X2") + p[3].ToString("X2") + p[4].ToString("X2");
    }

    static int Interpolar(int mv, int[] v, int[] p)
    {
        if (mv >= v[0]) return p[0];
        for (int i = 1; i < v.Length; i++)
            if (mv >= v[i]) return p[i] + (p[i - 1] - p[i]) * (mv - v[i]) / Math.Max(1, v[i - 1] - v[i]);
        return 0;
    }
    static int PorTensao(int mv)
    {
        return Interpolar(mv, new[] { 4186, 4067, 3989, 3922, 3859, 3811, 3778, 3751, 3717, 3671, 3646, 3579, 3500 },
                              new[] { 100, 90, 80, 70, 60, 50, 40, 30, 20, 10, 5, 2, 0 });
    }
    static int PorTensaoFone(int mv)
    {
        return Interpolar(mv, new[] { 4100, 3850, 3780, 3730, 3650, 3300 }, new[] { 100, 50, 30, 20, 5, 0 });
    }
}

// =====================================================================
//  Bluetooth: o proprio Windows guarda a bateria (a mesma de Configuracoes > Bluetooth)
// =====================================================================
static class Bluetooth
{
    [StructLayout(LayoutKind.Sequential)] struct CHAVE { public Guid G; public uint Id; public CHAVE(string g, uint id) { G = new Guid(g); Id = id; } }
    [StructLayout(LayoutKind.Sequential)] struct SP_DEVINFO { public int Tam; public Guid Classe; public uint Inst; public IntPtr Res; }
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(IntPtr g, string enumerador, IntPtr w, int f);
    [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInfo(IntPtr s, int i, ref SP_DEVINFO d);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] static extern bool SetupDiGetDeviceInstanceId(IntPtr s, ref SP_DEVINFO d, StringBuilder b, int n, out int req);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetupDiGetDevicePropertyW(IntPtr s, ref SP_DEVINFO d, ref CHAVE k, out uint tipo, byte[] buf, int n, out int req, int f);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);

    static CHAVE kBateria = new CHAVE("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 2);
    static CHAVE kConectado = new CHAVE("83DA6326-97A6-4088-9453-A1923F573B29", 15);
    static CHAVE kConteiner = new CHAVE("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C", 2);
    static CHAVE kNome = new CHAVE("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14);
    static CHAVE kDesc = new CHAVE("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2);
    static CHAVE kClasse = new CHAVE("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A", 10);

    static byte[] Prop(IntPtr s, ref SP_DEVINFO d, CHAVE k, out uint tipo)
    {
        var b = new byte[512]; int req;
        if (!SetupDiGetDevicePropertyW(s, ref d, ref k, out tipo, b, b.Length, out req, 0)) return null;
        var r = new byte[req]; Array.Copy(b, r, Math.Min(req, b.Length)); return r;
    }
    static string Texto(IntPtr s, ref SP_DEVINFO d, CHAVE k)
    {
        uint t; var b = Prop(s, ref d, k, out t);
        return b == null || t != 0x12 ? null : Encoding.Unicode.GetString(b).TrimEnd('\0').Trim();
    }

    class Achado { public int Pct; public string Nome; public bool Conectado; }

    public class Conectado { public string Nome, Conteiner; public ulong Endereco; }

    // aparelhos Bluetooth classicos conectados agora (nome, endereco e conteiner)
    public static List<Conectado> Conectados()
    {
        var l = new List<Conectado>();
        IntPtr s = SetupDiGetClassDevs(IntPtr.Zero, "BTHENUM", IntPtr.Zero, 0x04 | 0x02);
        if (s == new IntPtr(-1)) return l;
        try
        {
            var d = new SP_DEVINFO { Tam = Marshal.SizeOf(typeof(SP_DEVINFO)) };
            var sb = new StringBuilder(512);
            for (int i = 0; SetupDiEnumDeviceInfo(s, i, ref d); i++)
            {
                int req;
                if (!SetupDiGetDeviceInstanceId(s, ref d, sb, sb.Capacity, out req)) continue;
                string inst = sb.ToString();
                if (!inst.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase) || inst.Length < 24) continue;
                ulong end;
                if (!ulong.TryParse(inst.Substring(12, 12), System.Globalization.NumberStyles.HexNumber, null, out end)) continue;
                uint t;
                var con = Prop(s, ref d, kConectado, out t);
                if (con == null || t != 0x11 || con[0] == 0) continue;   // so conectados (nunca chama um aparelho desligado)
                string nome = Texto(s, ref d, kNome);
                var cb = Prop(s, ref d, kConteiner, out t);
                if (nome == null) continue;
                l.Add(new Conectado { Nome = nome, Endereco = end, Conteiner = cb != null && cb.Length == 16 ? new Guid(cb).ToString() : inst });
            }
        }
        finally { SetupDiDestroyDeviceInfoList(s); }
        return l;
    }
    public static readonly List<string> Diagnostico = new List<string>();   // para --diagnostico

    public static void Ler(List<Aparelho> saida)
    {
        var nomes = new Dictionary<string, string>(); var conectados = new Dictionary<string, bool>(); var classes = new Dictionary<string, uint>();
        var bat = new Dictionary<string, Achado>();
        // so os dispositivos Bluetooth (bem mais leve que listar tudo do PC)
        foreach (var enumerador in new[] { "BTHENUM", "BTHLE", "BTHLEDEVICE" })
        {
        IntPtr s = SetupDiGetClassDevs(IntPtr.Zero, enumerador, IntPtr.Zero, 0x04 | 0x02 /*ALLCLASSES|PRESENT*/);
        if (s == new IntPtr(-1)) continue;
        try
        {
            var d = new SP_DEVINFO { Tam = Marshal.SizeOf(typeof(SP_DEVINFO)) };
            var sb = new StringBuilder(512);
            for (int i = 0; SetupDiEnumDeviceInfo(s, i, ref d); i++)
            {
                int req;
                if (!SetupDiGetDeviceInstanceId(s, ref d, sb, sb.Capacity, out req)) continue;
                string inst = sb.ToString();
                if (!inst.StartsWith("BTHENUM\\") && !inst.StartsWith("BTHLE\\") && !inst.StartsWith("BTHLEDEVICE\\")) continue;
                uint t;
                var cb = Prop(s, ref d, kConteiner, out t);
                string cid = cb != null && cb.Length == 16 ? new Guid(cb).ToString() : inst;
                string nome = Texto(s, ref d, kNome) ?? Texto(s, ref d, kDesc);
                bool principal = inst.StartsWith("BTHENUM\\DEV_") || inst.StartsWith("BTHLE\\DEV_");
                var con = Prop(s, ref d, kConectado, out t);
                bool conectado = con == null || t != 0x11 || con[0] != 0;
                if (principal)
                {
                    if (nome != null) nomes[cid] = nome;
                    if (con != null && t == 0x11) conectados[cid] = con[0] != 0;
                    var cl = Prop(s, ref d, kClasse, out t);
                    if (cl != null && cl.Length >= 4) classes[cid] = BitConverter.ToUInt32(cl, 0);
                }
                var b = Prop(s, ref d, kBateria, out t);
                if (b == null || t != 0x03 || b[0] > 100) continue;
                Diagnostico.Add("BT " + nome + " | " + b[0] + "% | " + (conectado ? "conectado" : "desconectado"));
                Achado ant;
                if (bat.TryGetValue(cid, out ant) && ant.Conectado && !conectado) continue;
                bat[cid] = new Achado { Pct = b[0], Nome = nome, Conectado = conectado };
            }
        }
        finally { SetupDiDestroyDeviceInfoList(s); }
        }

        foreach (var kv in bat)
        {
            bool c; if (conectados.TryGetValue(kv.Key, out c) ? !c : !kv.Value.Conectado) continue;   // desligado: nao mostra
            string nome; if (!nomes.TryGetValue(kv.Key, out nome)) nome = LimparNome(kv.Value.Nome);
            if (string.IsNullOrEmpty(nome)) nome = "Aparelho Bluetooth";
            // ja lido por um protocolo proprio (ex.: Logitech por Bluetooth)
            if (saida.Exists(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase))) continue;
            uint cod; var tipo = Leitor.TipoPeloNome(nome);
            if (tipo == Tipo.Outro && classes.TryGetValue(kv.Key, out cod))
            {
                uint maior = (cod >> 8) & 0x1F, menor = (cod >> 2) & 0x3F;
                if (maior == 4) tipo = Tipo.Fone;
                else if (maior == 5) tipo = (menor & 0x30) == 0x20 ? Tipo.Mouse : (menor & 0x10) != 0 ? Tipo.Teclado : (menor & 0x0F) == 2 ? Tipo.Controle : Tipo.Outro;
            }
            saida.Add(new Aparelho { Chave = "bt:" + kv.Key, Nome = nome, Via = "Bluetooth", Tipo = tipo, Pct = kv.Value.Pct });
        }
    }

    static string LimparNome(string n)
    {
        if (n == null) return null;
        foreach (var suf in new[] { " Hands-Free AG", " Hands-Free", " Avrcp Transport", " Stereo", " LE" })
            if (n.EndsWith(suf)) n = n.Substring(0, n.Length - suf.Length);
        return n.Trim();
    }
}

// =====================================================================
//  Samsung Galaxy Buds: esquerdo, direito e estojo.
//  Ao abrir a conexao RFCOMM do servico da Samsung, os fones mandam sozinhos uma mensagem de estado
//  com as tres baterias (nada e enviado a eles). Formato publico do projeto GalaxyBudsClient:
//    FD [tam baixo][tam alto|flags] [id] [dados...] [crc baixo][crc alto] DD   (Buds+ e mais novos)
//    FE [tipo] [tam] [id] [dados...] [crc] EE                                   (Buds original)
//    id 0x60: dados[1]=esq, [2]=dir, [6]=estojo   id 0x61: dados[2]=esq, [3]=dir, [7]=estojo
//    crc = CRC-16/XMODEM sobre id+dados
// =====================================================================
static class GalaxyBuds
{
    class EndBth : System.Net.EndPoint
    {
        readonly ulong end; readonly Guid servico;
        public EndBth(ulong e, Guid s) { end = e; servico = s; }
        public override System.Net.Sockets.AddressFamily AddressFamily { get { return (System.Net.Sockets.AddressFamily)32; } }
        public override System.Net.SocketAddress Serialize()
        {
            var sa = new System.Net.SocketAddress((System.Net.Sockets.AddressFamily)32, 30);
            var a = BitConverter.GetBytes(end); for (int i = 0; i < 8; i++) sa[2 + i] = a[i];
            var g = servico.ToByteArray(); for (int i = 0; i < 16; i++) sa[10 + i] = g[i];
            return sa;
        }
        public override System.Net.EndPoint Create(System.Net.SocketAddress s) { return this; }
    }

    static readonly Guid sppNovo = new Guid("2e73a4ad-332d-41fc-90e2-16bef06523f2");      // Buds2 e mais novos (FE)
    static readonly Guid sppPadrao = new Guid("00001101-0000-1000-8000-00805f9b34fb");    // Buds+, Live, Pro
    static readonly Guid sppAntigo = new Guid("00001102-0000-1000-8000-00805f9b34fd");    // Buds original

    public static void Ler(List<Aparelho> saida)
    {
        foreach (var b in Bluetooth.Conectados())
        {
            if (b.Nome.IndexOf("Buds", StringComparison.OrdinalIgnoreCase) < 0) continue;
            int e = -1, d = -1, c = -1;
            if (!Status(b.Endereco, sppNovo, false, ref e, ref d, ref c) && !Status(b.Endereco, sppPadrao, false, ref e, ref d, ref c) &&
                !Status(b.Endereco, sppAntigo, true, ref e, ref d, ref c)) continue;
            int pct = e >= 0 && d >= 0 ? Math.Min(e, d) : Math.Max(e, d);
            if (pct < 0) continue;
            saida.Add(new Aparelho { Chave = "bt:" + b.Conteiner, Nome = b.Nome, Via = "Bluetooth", Tipo = Tipo.Fone, Pct = pct, Partes = new[] { e, d, c } });
        }
    }

    static bool Status(ulong end, Guid servico, bool antigo, ref int esq, ref int dir, ref int estojo)
    {
        try
        {
            using (var s = new System.Net.Sockets.Socket((System.Net.Sockets.AddressFamily)32, System.Net.Sockets.SocketType.Stream, (System.Net.Sockets.ProtocolType)3))
            {
                var r = s.BeginConnect(new EndBth(end, servico), null, null);
                if (!r.AsyncWaitHandle.WaitOne(4000)) return false;
                s.EndConnect(r);
                s.ReceiveTimeout = 1000;
                var buf = new List<byte>();
                var fim = DateTime.Now.AddSeconds(3);
                var pedaco = new byte[512];
                while (DateTime.Now < fim && buf.Count < 8192)
                {
                    int n;
                    try { n = s.Receive(pedaco); } catch (System.Net.Sockets.SocketException) { continue; }
                    if (n <= 0) break;
                    for (int i = 0; i < n; i++) buf.Add(pedaco[i]);
                    if (Achar(buf.ToArray(), antigo, ref esq, ref dir, ref estojo)) return true;
                }
            }
        }
        catch { }
        return false;
    }

    // teste do formato com quadros montados (sem precisar dos fones)
    public static List<string> Teste()
    {
        var r = new List<string>();
        Func<byte, byte[], bool, byte[]> quadro = (id, dados, estragar) =>
        {
            var corpo = new List<byte> { id }; corpo.AddRange(dados);
            ushort c = Crc(corpo.ToArray(), 0, corpo.Count);
            if (estragar) c ^= 1;
            int tam = corpo.Count + 2;
            var q = new List<byte> { 0xFD, (byte)(tam & 0xFF), (byte)(tam >> 8) };
            q.AddRange(corpo); q.Add((byte)c); q.Add((byte)(c >> 8)); q.Add(0xDD);
            return q.ToArray();
        };
        int e = -1, d = -1, c2 = -1;
        var lixo = new byte[] { 1, 2, 3, 0xFD, 9 };
        var q1 = quadro(0x61, new byte[] { 0, 1, 80, 75, 0, 0, 0, 40, 0 }, false);
        var tudo = new List<byte>(lixo); tudo.AddRange(q1);
        bool ok = Achar(tudo.ToArray(), false, ref e, ref d, ref c2);
        r.Add((ok && e == 80 && d == 75 && c2 == 40 ? "OK " : "FALHA ") + "status estendido: E " + e + " D " + d + " estojo " + c2);
        e = d = c2 = -1;
        ok = Achar(quadro(0x60, new byte[] { 0, 55, 60, 0, 0, 0, 0 }, false), false, ref e, ref d, ref c2);
        r.Add((ok && e == 55 && d == 60 && c2 == -1 ? "OK " : "FALHA ") + "status simples (estojo fechado): E " + e + " D " + d + " estojo " + c2);
        e = d = c2 = -1;
        ok = Achar(quadro(0x61, new byte[] { 0, 1, 80, 75, 0, 0, 0, 40, 0 }, true), false, ref e, ref d, ref c2);
        r.Add((!ok ? "OK " : "FALHA ") + "quadro com conferencia errada e recusado");
        return r;
    }

    static int Nivel(byte v) { return v >= 1 && v <= 100 ? v : -1; }

    static ushort Crc(byte[] d, int ini, int n)
    {
        ushort crc = 0;
        for (int i = 0; i < n; i++)
        {
            crc ^= (ushort)(d[ini + i] << 8);
            for (int b = 0; b < 8; b++) crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    static bool Achar(byte[] d, bool antigo, ref int esq, ref int dir, ref int estojo)
    {
        byte som = (byte)(antigo ? 0xFE : 0xFD), eom = (byte)(antigo ? 0xEE : 0xDD);
        bool achou = false;
        for (int i = 0; i + 6 <= d.Length; i++)
        {
            if (d[i] != som) continue;
            int tam;
            if (antigo) tam = d[i + 2];
            else
            {
                int cab = d[i + 1] | (d[i + 2] << 8);
                if ((cab & 0x2000) != 0) continue;     // pedaco de firmware
                tam = cab & 0x3FF;
            }
            if (tam < 3 || i + 3 + tam >= d.Length || d[i + 3 + tam] != eom) continue;
            int corpo = i + 3;
            ushort crc = (ushort)(d[corpo + tam - 2] | (d[corpo + tam - 1] << 8));
            if (Crc(d, corpo, tam - 2) != crc) continue;
            byte id = d[corpo]; int p = corpo + 1, plen = tam - 3;
            if (id == 0x61 && plen >= 4) { esq = Nivel(d[p + 2]); dir = Nivel(d[p + 3]); if (!antigo && plen >= 8) estojo = Nivel(d[p + 7]); }
            else if (id == 0x60 && plen >= 3) { esq = Nivel(d[p + 1]); dir = Nivel(d[p + 2]); if (!antigo && plen >= 7) estojo = Nivel(d[p + 6]); }
            else continue;
            achou = true;
            i += 3 + tam;
        }
        return achou;
    }
}

// =====================================================================
//  Controles PlayStation (DualShock 4, DualSense, DualSense Edge), por USB ou Bluetooth.
//  A bateria vem no relatorio normal do controle (so leitura):
//    DualShock 4: USB relatorio 01 byte 30, Bluetooth relatorio 11 byte 32: bits 0-3 nivel, bit 4 cabo
//    DualSense:   USB relatorio 01 byte 53, Bluetooth relatorio 31 byte 54: bits 0-3 nivel (0-10), bits 4-7 estado
//  No Bluetooth eles mandam um relatorio reduzido ate alguem ler o relatorio de recurso 05 (calibracao),
//  como os jogos fazem. Layout: driver hid-playstation do Linux.
// =====================================================================
static class PlayStation
{
    static bool Ds4(ushort pid) { return pid == 0x05C4 || pid == 0x09CC || pid == 0x0BA0; }
    static bool DualSense(ushort pid) { return pid == 0x0CE6 || pid == 0x0DF2; }

    public static void Ler(List<ColecaoHid> hids, List<Aparelho> saida)
    {
        foreach (var c in hids)
        {
            if (c.Vid != 0x054C || !(Ds4(c.Pid) || DualSense(c.Pid)) || c.Pagina != 0x01 || c.Uso != 0x05) continue;
            bool bt = c.TamEntrada > 64;
            int pct = -1; bool carregando = false;
            using (var d = new DispHid(c))
            {
                if (!d.Ok) continue;
                if (bt) d.LerRecurso(0x05, Math.Max(41, c.TamRecurso));   // passa a mandar o relatorio completo
                var fim = DateTime.Now.AddMilliseconds(600);
                while (pct < 0 && DateTime.Now < fim)
                {
                    var r = d.Ler(100);
                    if (r == null) continue;
                    if (!Analisar(c.Pid, bt, r, out pct, out carregando)) pct = -1;
                }
            }
            if (pct < 0) continue;
            string nome = Ds4(c.Pid) ? "DualShock 4" : c.Pid == 0x0DF2 ? "DualSense Edge" : "DualSense";
            saida.Add(new Aparelho { Chave = "ps:" + c.Pid.ToString("X4") + ":" + c.Fisico.GetHashCode().ToString("X8"), Nome = nome, Tipo = Tipo.Controle,
                Via = bt ? "Bluetooth" : c.Pid == 0x0BA0 ? "Adaptador sem fio" : "Cabo USB", Pct = pct, Carregando = carregando });
        }
    }

    public static bool Analisar(ushort pid, bool bt, byte[] r, out int pct, out bool carregando)
    {
        pct = -1; carregando = false;
        if (r.Length < 10) return false;
        if (Ds4(pid))
        {
            int at = r[0] == 0x01 ? 30 : r[0] == 0x11 ? 32 : 0;
            if (at == 0 || r.Length <= at || (r[0] == 0x01 && bt)) return false;
            if (pid == 0x0BA0 && r[at] == 0) return false;          // adaptador sem controle
            int nivel = r[at] & 0x0F; bool cabo = (r[at] & 0x10) != 0;
            pct = cabo ? (nivel >= 11 ? 100 : Math.Min(100, nivel * 10)) : Math.Min(100, nivel * 10 + 5);
            carregando = cabo && nivel < 11;
            return true;
        }
        int a = r[0] == 0x01 ? 53 : r[0] == 0x31 ? 54 : 0;
        if (a == 0 || r.Length <= a || (r[0] == 0x01 && bt)) return false;
        int n = r[a] & 0x0F, estado = r[a] >> 4;
        pct = Math.Min(100, n * 10 + 5);
        carregando = estado == 1;
        if (estado == 2) { pct = 100; carregando = true; }
        return true;
    }
}

// =====================================================================
//  Attack Shark (X11 e outros com chip Xenta, VID 1D57): receptor 2.4 GHz (FA60) ou cabo (FA55).
//  O mouse manda sozinho, a cada ~2 s, o relatorio 03 MM 40 EE NN na colecao de pagina 0x0A:
//  MM = modelo (0x55 = X11), EE = estado (01 bateria, 02 na energia, 03 carregando), NN = %.
//  So escuta: abre sem permissao de escrita e nunca envia nada.
//  Ao (re)conectar ele manda um "100%" solto, por isso so vale o valor que chega 2 vezes seguidas.
//  Protocolo descrito por projetos abertos (attackshark-battery-windows, attackshark-x11-linux).
// =====================================================================
static class AttackShark
{
    static readonly Dictionary<string, int> ultimo = new Dictionary<string, int>();   // ultimo valor aceito (estado*1000 + %)

    public static void Ler(List<ColecaoHid> hids, List<Aparelho> saida)
    {
        foreach (var c in hids)
        {
            if (c.Vid != 0x1D57 || (c.Pid != 0xFA60 && c.Pid != 0xFA55) || c.Pagina != 0x0A || c.TamEntrada < 5) continue;
            int modelo = -1, aceito = -1, anterior = -1;
            int jaVisto; bool temUltimo = ultimo.TryGetValue(c.Fisico, out jaVisto);
            using (var d = new DispHid(c, true))
            {
                if (!d.Ok) continue;
                var fim = DateTime.Now.AddMilliseconds(4600);   // chega 1 a cada ~2 s: da tempo de ver 2
                while (aceito < 0 && DateTime.Now < fim)
                {
                    var r = d.Ler(250);
                    int m, est, pct;
                    if (r == null || !Analisar(r, out m, out est, out pct)) continue;
                    int v = est * 1000 + pct;
                    if (v == anterior || (temUltimo && v == jaVisto)) { aceito = v; modelo = m; }
                    anterior = v;
                }
            }
            if (aceito < 0) continue;   // silencio: desligado, dormindo ou no Bluetooth (nunca quer dizer 0%)
            ultimo[c.Fisico] = aceito;
            int estado = aceito / 1000;
            saida.Add(new Aparelho
            {
                Chave = "attackshark:" + c.Fisico.GetHashCode().ToString("X8"),
                Nome = NomeDoModelo(modelo), Tipo = Tipo.Mouse,
                Via = c.Pid == 0xFA60 ? "Receptor 2.4 GHz" : "Cabo USB",
                Pct = aceito % 1000, Carregando = estado == 2 || estado == 3
            });
        }
    }

    public static string NomeDoModelo(int m) { return m == 0x55 ? "Attack Shark X11" : "Mouse Attack Shark"; }

    public static bool Analisar(byte[] r, out int modelo, out int estado, out int pct)
    {
        modelo = estado = pct = -1;
        if (r == null || r.Length < 5 || r[0] != 0x03 || r[2] != 0x40) return false;
        if (r[3] < 1 || r[3] > 3 || r[4] > 100) return false;
        modelo = r[1]; estado = r[3]; pct = r[4];
        return true;
    }
}

// =====================================================================
//  Celular Android pelo app Vincular ao Celular (Phone Link) do Windows.
//  O app mantem o cartao do celular do menu Iniciar num arquivo; so o nome e a bateria sao lidos
//  (o resto, como notificacoes, e ignorado). Sem Bluetooth nem rede: so leitura de um arquivo do proprio PC.
// =====================================================================
static class Celular
{
    static string Arq { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Packages\Microsoft.YourPhone_8wekyb3d8bbwe\LocalState\StartMenu\StartMenuCompanion.json"); } }

    public static void Ler(List<Aparelho> saida)
    {
        var f = new FileInfo(Arq);
        if (!f.Exists || f.Length > 4 * 1024 * 1024) return;
        if ((DateTime.Now - f.LastWriteTime).TotalHours > 3) return;      // cartao antigo: celular desconectado
        string json;
        using (var st = new FileStream(Arq, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sr = new StreamReader(st, Encoding.UTF8)) json = sr.ReadToEnd();
        string nome; int pct;
        if (!Analisar(json, out nome, out pct)) return;
        saida.Add(new Aparelho { Chave = "celular", Nome = nome, Tipo = Tipo.Celular, Via = "Vincular ao Celular", Pct = pct });
    }

    // "speak": "<nome do celular> - Connected to your PC" (ou em portugues) e "Battery 80% remaining" / "Bateria: 80%"
    public static bool Analisar(string json, out string nome, out int pct)
    {
        nome = null; pct = -1;
        var m = System.Text.RegularExpressions.Regex.Match(json, "\"speak\"\\s*:\\s*\"([^\"]{1,120})\"");
        if (!m.Success) return false;
        string fala = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value);
        var mn = System.Text.RegularExpressions.Regex.Match(fala, "^(.+?)\\s+-\\s+(Connected to your PC|Conectado ao seu PC|Conectado ao PC|Conectado ao computador)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!mn.Success) return false;
        var mb = System.Text.RegularExpressions.Regex.Match(json, "(?:Battery|Bateria)\\D{0,12}?(\\d{1,3})\\s?%", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!mb.Success) return false;
        int v = int.Parse(mb.Groups[1].Value);
        if (v < 0 || v > 100) return false;
        nome = mn.Groups[1].Value.Trim(); pct = v;
        return true;
    }
}

// =====================================================================
//  Controles Xbox (XInput): o Windows informa so o nivel (vazio, baixo, medio, cheio)
// =====================================================================
static class Controles
{
    // Xbox e compativeis pelo Windows.Gaming.Input: a % de verdade (carga em mWh). Devolve quantos achou.
    public static int LerWgi(List<Aparelho> saida)
    {
        var tipo = Type.GetType("Windows.Gaming.Input.RawGameController, Windows.Gaming, ContentType=WindowsRuntime");
        if (tipo == null) return 0;
        var lista = tipo.GetProperty("RawGameControllers").GetValue(null) as System.Collections.IEnumerable;
        if (lista == null) return 0;
        int n = 0;
        foreach (var c in lista)
        {
            var t = c.GetType();
            int vid = Convert.ToInt32(t.GetProperty("HardwareVendorId").GetValue(c));
            if (vid != 0x045E) continue;                          // Microsoft (Xbox)
            var rel = t.GetMethod("TryGetBatteryReport").Invoke(c, null);
            if (rel == null) continue;
            var tr = rel.GetType();
            object resto = tr.GetProperty("RemainingCapacityInMilliwattHours").GetValue(rel), cheio = tr.GetProperty("FullChargeCapacityInMilliwattHours").GetValue(rel);
            int status = Convert.ToInt32(tr.GetProperty("Status").GetValue(rel));   // 0 sem bateria, 1 descarregando, 2 parado, 3 carregando
            if (resto == null || cheio == null || status == 0) continue;
            double f = Convert.ToDouble(cheio), r = Convert.ToDouble(resto);
            if (f <= 0) continue;
            string nome = null;
            var pn = t.GetProperty("DisplayName");
            if (pn != null) try { nome = pn.GetValue(c) as string; } catch { }
            if (string.IsNullOrWhiteSpace(nome)) nome = "Controle Xbox";
            int pid = Convert.ToInt32(t.GetProperty("HardwareProductId").GetValue(c));
            saida.Add(new Aparelho { Chave = "xbox:" + pid.ToString("X4") + ":" + n, Nome = nome, Via = "Sem fio", Tipo = Tipo.Controle,
                Pct = (int)Math.Round(Math.Max(0, Math.Min(100, r * 100 / f))), Carregando = status == 3 });
            n++;
        }
        return n;
    }

    [StructLayout(LayoutKind.Sequential)] struct BATERIA { public byte Tipo, Nivel; }
    [DllImport("xinput1_4.dll")] static extern int XInputGetBatteryInformation(int usuario, byte tipo, out BATERIA b);

    public static void LerXInput(List<Aparelho> saida)
    {
        for (int i = 0; i < 4; i++)
        {
            BATERIA b;
            try { if (XInputGetBatteryInformation(i, 0, out b) != 0) continue; } catch { return; }
            if (b.Tipo == 0 /*desconectado*/ || b.Tipo == 1 /*com fio*/ || b.Tipo == 0xFF) continue;
            int[] pct = { 5, 30, 65, 100 };
            saida.Add(new Aparelho { Chave = "xinput:" + i, Nome = "Controle Xbox " + (i + 1), Via = "Sem fio", Tipo = Tipo.Controle, Pct = pct[Math.Min(3, (int)b.Nivel)], Aproximado = true });
        }
    }
}

// =====================================================================
//  Aparelhos que seguem o padrao HID de bateria ("Battery Strength")
// =====================================================================
static class HidPadrao
{
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, byte[] caps);
    [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int tipo, byte[] caps, ref ushort n, IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int tipo, ushort pagina, ushort link, ushort uso, out uint valor, IntPtr pp, byte[] rel, int tam);
    [DllImport("hid.dll")] static extern bool HidD_GetFeature(SafeFileHandle h, byte[] b, int n);

    public static void Ler(List<ColecaoHid> hids, List<Aparelho> saida)
    {
        var vistos = new HashSet<string>();
        foreach (var c in hids)
        {
            if (c.Vid == 0x046D || c.TamRecurso == 0) continue;   // Logitech ja tem leitura propria
            if (vistos.Contains(c.Fisico)) continue;
            using (var h = Hid.CreateFile(c.Caminho, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (h.IsInvalid) continue;
                IntPtr pp;
                if (!HidD_GetPreparsedData(h, out pp)) continue;
                try
                {
                    // procura o uso "Battery Strength" (pagina 0x06, uso 0x20) nos relatorios de recurso
                    var caps = new byte[64];
                    if (HidP_GetCaps(pp, caps) != 0x110000) continue;
                    ushort nv = BitConverter.ToUInt16(caps, 2 * 5 + 34 + 2 * 8);   // NumberFeatureValueCaps
                    if (nv == 0 || nv > 64) continue;
                    var vc = new byte[72 * nv];
                    if (HidP_GetValueCaps(2, vc, ref nv, pp) != 0x110000) continue;
                    for (int i = 0; i < nv; i++)
                    {
                        int o = 72 * i;
                        ushort pagina = BitConverter.ToUInt16(vc, o);
                        byte relId = vc[o + 2];
                        ushort link = BitConverter.ToUInt16(vc, o + 6);
                        bool faixa = vc[o + 12] != 0;
                        ushort uso = BitConverter.ToUInt16(vc, o + 56);
                        int min = BitConverter.ToInt32(vc, o + 40), max = BitConverter.ToInt32(vc, o + 44);
                        if (faixa || pagina != 0x06 || uso != 0x20) continue;
                        var rel = new byte[c.TamRecurso]; rel[0] = relId;
                        if (!HidD_GetFeature(h, rel, rel.Length)) continue;
                        uint v;
                        if (HidP_GetUsageValue(2, pagina, link, uso, out v, pp, rel, rel.Length) != 0x110000) continue;
                        int pct = max > min ? (int)Math.Round(((int)v - min) * 100.0 / (max - min)) : (int)v;
                        if (pct < 0 || pct > 100) continue;
                        string nome = string.IsNullOrEmpty(c.Produto) ? "Aparelho sem fio" : c.Produto;
                        if (saida.Exists(x => string.Equals(x.Nome, nome, StringComparison.OrdinalIgnoreCase))) break;
                        saida.Add(new Aparelho { Chave = "hid:" + c.Vid.ToString("X4") + ":" + c.Pid.ToString("X4"), Nome = nome, Via = "USB", Tipo = Leitor.TipoPeloNome(nome), Pct = pct });
                        vistos.Add(c.Fisico);
                        break;
                    }
                }
                finally { HidD_FreePreparsedData(pp); }
            }
        }
    }
}
