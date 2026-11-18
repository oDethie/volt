// Volt - instalador simples (o proprio Volt.exe se instala e se desinstala)
//   Instala em %LOCALAPPDATA%\Programs\Volt, sem administrador: copia o exe, cria o atalho no menu Iniciar
//   e aparece em Configuracoes do Windows > Aplicativos (para desinstalar por la).
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Instalador
{
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);

    public static string Pasta { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Volt"); } }
    public static string ExeInstalado { get { return Path.Combine(Pasta, "Volt.exe"); } }
    static string Atalho { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Volt.lnk"); } }
    const string ChaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Volt";

    public static bool EstaInstalado { get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(ExeInstalado), StringComparison.OrdinalIgnoreCase); } }

    // aberto de fora da pasta de instalacao (ex.: Downloads): oferece instalar. true = pode seguir abrindo este exe
    public static bool Oferecer(bool escuro)
    {
        bool jaTem = File.Exists(ExeInstalado);
        Version vNova = Atualizador.Atual, vVelha = null;
        if (jaTem) try { var fv = FileVersionInfo.GetVersionInfo(ExeInstalado); vVelha = new Version(fv.FileMajorPart, fv.FileMinorPart, fv.FileBuildPart); } catch { }
        string titulo = !jaTem ? "Instalar o Volt" : vVelha != null && vVelha < vNova ? "Atualizar o Volt" : "Reinstalar o Volt";
        string texto = !jaTem
            ? "Ele fica na sua pasta de programas, abre sozinho com o Windows e aparece no menu Iniciar. Não precisa de administrador."
            : "O Volt já está instalado" + (vVelha != null ? " (versão " + vVelha.ToString(3) + ")" : "") + ". Instalar esta versão (" + vNova.ToString(3) + ") por cima? Suas configurações continuam.";
        using (var f = new JanelaInstalar(titulo, texto, escuro))
        {
            var r = f.ShowDialog();
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.No) return true;       // so abrir este, sem instalar
        }
        string erro = Instalar();
        if (erro != null) { MessageBox.Show("Não deu para instalar o Volt:\n" + erro, "Volt", MessageBoxButtons.OK, MessageBoxIcon.Warning); return true; }
        try { Process.Start(ExeInstalado); } catch { }
        return false;
    }

    static void FecharAberto()
    {
        uint msg = RegisterWindowMessage("Volt_Sair");
        PostMessage(new IntPtr(0xFFFF), msg, IntPtr.Zero, IntPtr.Zero);
        for (int i = 0; i < 50; i++)
        {
            bool aberto = false;
            foreach (var p in Process.GetProcessesByName("Volt")) if (p.Id != Process.GetCurrentProcess().Id) aberto = true;
            if (!aberto) return;
            Thread.Sleep(100);
        }
        foreach (var p in Process.GetProcessesByName("Volt")) if (p.Id != Process.GetCurrentProcess().Id) try { p.Kill(); p.WaitForExit(3000); } catch { }
    }

    public static string Instalar()
    {
        try
        {
            FecharAberto();
            Directory.CreateDirectory(Pasta);
            File.Copy(Application.ExecutablePath, ExeInstalado, true);
            CriarAtalho();
            using (var k = Registry.CurrentUser.CreateSubKey(ChaveDesinstalar))
            {
                k.SetValue("DisplayName", "Volt");
                k.SetValue("DisplayIcon", ExeInstalado + ",0");
                k.SetValue("DisplayVersion", Atualizador.Atual.ToString(3));
                k.SetValue("Publisher", "oDethie");
                k.SetValue("InstallLocation", Pasta);
                k.SetValue("UninstallString", "\"" + ExeInstalado + "\" --desinstalar");
                k.SetValue("URLInfoAbout", "https://github.com/" + Atualizador.Repo);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(ExeInstalado).Length / 1024), RegistryValueKind.DWord);
            }
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ja instalado (ou atualizado sozinho): garante o atalho e o registro em Configuracoes > Aplicativos
    public static void GarantirRegistro()
    {
        try
        {
            if (!File.Exists(Atalho)) CriarAtalho();
            using (var k = Registry.CurrentUser.CreateSubKey(ChaveDesinstalar))
            {
                if (k.GetValue("DisplayVersion") as string == Atualizador.Atual.ToString(3) && k.GetValue("UninstallString") != null) return;
                k.SetValue("DisplayName", "Volt");
                k.SetValue("DisplayIcon", ExeInstalado + ",0");
                k.SetValue("DisplayVersion", Atualizador.Atual.ToString(3));
                k.SetValue("Publisher", "oDethie");
                k.SetValue("InstallLocation", Pasta);
                k.SetValue("UninstallString", "\"" + ExeInstalado + "\" --desinstalar");
                k.SetValue("URLInfoAbout", "https://github.com/" + Atualizador.Repo);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(ExeInstalado).Length / 1024), RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    static void CriarAtalho()
    {
        var tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        object at = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Atalho });
        var ta = at.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, at, new object[] { ExeInstalado });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, at, new object[] { Pasta });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, at, new object[] { "Bateria dos aparelhos sem fio" });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, at, new object[] { ExeInstalado + ",0" });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, at, null);
        Marshal.FinalReleaseComObject(at);
        Marshal.FinalReleaseComObject(shell);
    }

    // Volt.exe --desinstalar (tambem pelo Windows: Configuracoes > Aplicativos)
    public static void Desinstalar(bool escuro)
    {
        if (MessageBox.Show("Desinstalar o Volt?\n\nRemove o programa, o atalho, o início com o Windows, as configurações e o histórico de bateria.",
            "Volt", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        FecharAberto();
        try { Process.Start(new ProcessStartInfo("schtasks.exe", "/delete /tn \"Volt\" /f") { CreateNoWindow = true, UseShellExecute = false }).WaitForExit(10000); } catch { }
        try { File.Delete(Atalho); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(ChaveDesinstalar, false); } catch { }
        string dados = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        try { File.Delete(Path.Combine(dados, "Volt.ini")); } catch { }
        try { File.Delete(Path.Combine(dados, "Volt.log")); } catch { }
        try { Directory.Delete(Path.Combine(dados, "Volt"), true); } catch { }
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings", true))
                if (k != null)
                    foreach (var n in k.GetSubKeyNames())
                    {
                        string p; using (var s = k.OpenSubKey(n)) p = s == null ? null : s.GetValue("ExecutablePath") as string;
                        if (p != null && p.EndsWith("\\Volt.exe", StringComparison.OrdinalIgnoreCase)) k.DeleteSubKeyTree(n, false);
                    }
        }
        catch { }
        // a pasta do programa e apagada logo depois que este exe fechar
        string pasta = Path.GetDirectoryName(Application.ExecutablePath);
        if (string.Equals(pasta, Pasta, StringComparison.OrdinalIgnoreCase))
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + pasta + "\"")
                { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            }
            catch { }
        MessageBox.Show("O Volt foi desinstalado.", "Volt", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// janela "Instalar o Volt" no estilo do Windows 11
class JanelaInstalar : JanelaBase
{
    public JanelaInstalar(string titulo, string texto, bool temaEscuro) : base(temaEscuro)
    {
        Text = "Volt";
        ClientSize = new Size(E(460), E(220));
        int x = E(24), w = ClientSize.Width - E(48), y = E(22);
        var logo = new PictureBox { Image = Desenho.Logo(E(40)), Size = new Size(E(40), E(40)), Location = new Point(x, y) };
        Controls.Add(logo);
        Controls.Add(new Label { Text = titulo, Font = fTitulo, AutoSize = true, Location = new Point(x + E(52), y + E(4)), ForeColor = cTexto });
        y += E(56);
        Controls.Add(new Label { Text = texto, Font = fNormal, AutoSize = false, Size = new Size(w, E(64)), Location = new Point(x, y), ForeColor = cFraco });
        var rodape = Rodape();
        var bInstalar = Botao(titulo.StartsWith("Instalar") ? "Instalar" : titulo.StartsWith("Atualizar") ? "Atualizar" : "Reinstalar", true);
        var bAbrir = Botao("Só abrir", false);
        int by = (rodape.Height - E(34)) / 2;
        bInstalar.Location = new Point(rodape.Width - E(24) - bInstalar.Width, by);
        bAbrir.Location = new Point(bInstalar.Left - E(8) - bAbrir.Width, by);
        rodape.Controls.AddRange(new Control[] { bInstalar, bAbrir });
        bInstalar.Click += (s, e) => DialogResult = DialogResult.OK;
        bAbrir.Click += (s, e) => DialogResult = DialogResult.No;
        AcceptButton = bInstalar;
    }
}
