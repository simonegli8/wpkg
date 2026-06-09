using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WindowsPackager;

public enum WSLNetworkingMode { NAT, mirrored }
public enum WSLDistros { Default, Ubuntu, Debian, Kali, Ubuntu18, Ubuntu20, Ubuntu22, Ubuntu24, Oracle7, Oracle8, Oracle9, openSUSELeap, SUSE_15, SUSE_16, openSUSEThumbleweed, Fedora, FedoraRemix, Alpine, AlmaLinux, Arch, eLxr, Native, Other };

public class WSLDistro
{
    public WSLDistros Distro { get; set; }
    public string OtherDistroName { get; set; }

    public static implicit operator string(WSLDistro distro) => WSLShell.GetDistroName(distro) ?? distro.OtherDistroName;
    public static implicit operator WSLDistros(WSLDistro distro) => distro.Distro;
    public static implicit operator WSLDistro(string distro)
    {
        var wslDistro = new WSLDistro() { OtherDistroName = null };
        for (WSLDistros d = WSLDistros.Default; d <= WSLDistros.Other; d++)
        {
            wslDistro.Distro = d;
            if (WSLShell.GetDistroName(wslDistro) == distro) return new WSLDistro() { Distro = d, OtherDistroName = null };
        }
        return new WSLDistro() { Distro = WSLDistros.Other, OtherDistroName = distro };
    }
    public static implicit operator WSLDistro(WSLDistros distro) => new WSLDistro() { Distro = distro, OtherDistroName = null };
    public override string ToString() => WSLShell.GetDistroName(this) ?? OtherDistroName;
}

public class WSLShell : Shell
{
    private static string ToCamelCase(string name)
    {
        if (name != null && name.Length > 0 && char.IsUpper(name[0])) return char.ToLower(name[0]) + name.Substring(1);
        return name;
    }
    private static string ToTitleCase(string name)
    {
        if (name != null && name.Length > 0 && char.IsLower(name[0])) return char.ToUpper(name[0]) + name.Substring(1);
        return name;
    }

    public class ConfigurationSection : OrderedNameDictionary<string>
    {
        public ConfigurationSection() : base(StringComparer.OrdinalIgnoreCase) { }
        public bool Exists => base.Count > 0;
        public virtual string Title { get; }
        public bool? ParseBool(string setting)
        {
            var value = this[setting];
            if (string.IsNullOrEmpty(value)) return null;
            bool boolean;
            if (bool.TryParse(value, out boolean)) return boolean;
            return null;
        }

        public int? ParseInt(string setting)
        {
            var value = this[setting];
            if (string.IsNullOrEmpty(value)) return null;
            int integer;
            if (int.TryParse(value, out integer)) return integer;
            return null;
        }
        int ntrivia = 0;
        public void AddTrivia(string text) => this[$"\"{ntrivia++}"] = text;
        public void Write(StringBuilder sb, string eol = null)
        {
            eol = eol ?? "\n";
            sb.Append($"[{ToCamelCase(Title)}]{eol}");
            foreach (var item in this)
            {
                if (item.Value != null)
                {
                    var key = item.Key;
                    if (key.StartsWith("\"")) sb.Append(item.Value);
                    else
                    {
                        sb.Append(ToCamelCase(item.Key));
                        sb.Append("=");
                        sb.Append(ToCamelCase(item.Value));
                    }
                    sb.Append(eol);
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }
    }

    public class BootSection : ConfigurationSection
    {
        public override string Title => "Boot";

        public bool? Systemd
        {
            get => ParseBool(this[nameof(Systemd)]);
            set => this[nameof(Systemd)] = value?.ToString();
        }
        public string Command
        {
            get => this[nameof(Command)];
            set => this[nameof(Command)] = value;
        }
    }

    public class Wsl2Section : ConfigurationSection
    {
        public override string Title => "Wsl2";
        public string Kernel
        {
            get => this[nameof(Kernel)];
            set => this[nameof(Kernel)] = value;
        }
        public string Memory
        {
            get => this[nameof(Memory)];
            set => this[nameof(Memory)] = value;
        }
        public int? Processors
        {
            get => ParseInt(this[nameof(Processors)]);
            set => this[nameof(Processors)] = value?.ToString();
        }
        public bool? LocalhostForwarding
        {
            get => ParseBool(this[nameof(LocalhostForwarding)]);
            set => this[nameof(LocalhostForwarding)] = value?.ToString();
        }
        public string KernelCommandLine
        {
            get => this[nameof(KernelCommandLine)];
            set => this[nameof(KernelCommandLine)] = value;
        }
        public bool? SafeMode
        {
            get => ParseBool(this[nameof(SafeMode)]);
            set => this[nameof(SafeMode)] = value?.ToString();
        }
        public string Swap
        {
            get => this[nameof(Swap)];
            set => this[nameof(Swap)] = value;
        }
        public string SwapFile
        {
            get => this[nameof(SwapFile)];
            set => this[nameof(SwapFile)] = value;
        }
        public bool? PageReporting
        {
            get => ParseBool(this[nameof(PageReporting)]);
            set => this[nameof(PageReporting)] = value?.ToString();
        }
        public bool? GuiApplications
        {
            get => ParseBool(this[nameof(GuiApplications)]);
            set => this[nameof(GuiApplications)] = value?.ToString();
        }
        public bool? DebugConsole
        {
            get => ParseBool(this[nameof(DebugConsole)]);
            set => this[nameof(DebugConsole)] = value?.ToString();
        }
        public bool? NestedVirtualization
        {
            get => ParseBool(this[nameof(NestedVirtualization)]);
            set => this[nameof(NestedVirtualization)] = value?.ToString();
        }
        public int? VmIdleTimeout
        {
            get => ParseInt(this[nameof(VmIdleTimeout)]);
            set => this[nameof(VmIdleTimeout)] = value?.ToString();
        }
        public bool? DnsProxy
        {
            get => ParseBool(this[nameof(DnsProxy)]);
            set => this[nameof(DnsProxy)] = value?.ToString();
        }
        public WSLNetworkingMode? NetworkingMode
        {
            get
            {
                var setting = this[nameof(NetworkingMode)];
                if (string.IsNullOrEmpty(setting)) return null;
                WSLNetworkingMode mode;
                if (Enum.TryParse<WSLNetworkingMode>(setting, true, out mode)) return mode;
                else return null;
            }
            set => this[nameof(NetworkingMode)] = ToCamelCase(value?.ToString());
        }
        public bool? Firewall
        {
            get => ParseBool(this[nameof(Firewall)]);
            set => this[nameof(Firewall)] = value?.ToString();
        }
        public bool? DnsTunneling
        {
            get => ParseBool(this[nameof(DnsTunneling)]);
            set => this[nameof(DnsTunneling)] = value?.ToString();
        }
        public bool? AutoProxy
        {
            get => ParseBool(this[nameof(AutoProxy)]);
            set => this[nameof(AutoProxy)] = value?.ToString();
        }
    }

    public class ExperimentalSection : ConfigurationSection
    {
        public override string Title => "Experimental";
        public string AutoMemoryReclaim
        {
            get => this[nameof(AutoMemoryReclaim)];
            set => this[nameof(AutoMemoryReclaim)] = value;
        }
        public bool? SparseVhd
        {
            get => ParseBool(this[nameof(SparseVhd)]);
            set => this[nameof(SparseVhd)] = value?.ToString();
        }
        public bool? UseWindowsDnsCache
        {
            get => ParseBool(this[nameof(UseWindowsDnsCache)]);
            set => this[nameof(UseWindowsDnsCache)] = value?.ToString();
        }
        public bool? BestEffortDnsParsing
        {
            get => ParseBool(this[nameof(BestEffortDnsParsing)]);
            set => this[nameof(BestEffortDnsParsing)] = value?.ToString();
        }
        public int? InitialAutoProxyTimeout
        {
            get => ParseInt(this[nameof(InitialAutoProxyTimeout)]);
            set => this[nameof(InitialAutoProxyTimeout)] = value?.ToString();
        }
        public string IgnorePorts
        {
            get => this[nameof(IgnorePorts)];
            set => this[nameof(IgnorePorts)] = value;
        }
        public bool? HostAddressLoopback
        {
            get => ParseBool(this[nameof(HostAddressLoopback)]);
            set => this[nameof(HostAddressLoopback)] = value?.ToString();
        }
    }
    public class AutomountSection : ConfigurationSection
    {
        public override string Title => "Automount";

        public bool? Enabled
        {
            get => ParseBool(this[nameof(Enabled)]);
            set => this[nameof(Enabled)] = value?.ToString();
        }
        public bool? MountFsTab
        {
            get => ParseBool(this[nameof(MountFsTab)]);
            set => this[nameof(MountFsTab)] = value?.ToString();
        }
        public string Root
        {
            get => this[nameof(Root)];
            set => this[nameof(Root)] = value;
        }
        public string Options
        {
            get => this[nameof(Options)];
            set => this[nameof(Options)] = value;
        }
    }

    public class NetworkSection : ConfigurationSection
    {
        public override string Title => "Network";

        public bool? GenerateHosts
        {
            get => ParseBool(this[nameof(GenerateHosts)]);
            set => this[nameof(GenerateHosts)] = value?.ToString();
        }
        public bool? GenerateResolvConf
        {
            get => ParseBool(this[nameof(GenerateResolvConf)]);
            set => this[nameof(GenerateResolvConf)] = value?.ToString();
        }

        public string Hostname
        {
            get => this[nameof(Hostname)];
            set => this[nameof(Hostname)] = value;
        }
    }

    public class InteropSection : ConfigurationSection
    {
        public override string Title => "Interop";

        public bool? Enabled
        {
            get => ParseBool(this[nameof(Enabled)]);
            set => this[nameof(Enabled)] = value?.ToString();
        }
        public bool? AppendWindowsPath
        {
            get => ParseBool(this[nameof(AppendWindowsPath)]);
            set => this[nameof(AppendWindowsPath)] = value?.ToString();
        }
    }

    public class UserSection : ConfigurationSection
    {
        public override string Title => "User";
        public string Default
        {
            get => this[nameof(Default)];
            set => this[nameof(Default)] = value;
        }
    }

    public class ConfigurationBase : OrderedNameDictionary<ConfigurationSection>
    {
        public virtual string File => null;
        string leadingTrivia;
        public WSLShell Shell { get; protected set; }

        public ConfigurationBase() { }

        public ConfigurationBase(WSLShell shell)
        {
            Shell = shell;
            Open();
        }

        protected virtual bool IsWslFile => !File.Contains('\\');

        public void Open()
        {
            var configTxt = Shell.ReadAllText(File);
            var match = Regex.Match(configTxt, @"(?<trivia>^.*?)(?<body>(?<=^|\n)\[[^]\n]\].*$)", RegexOptions.Singleline);
            leadingTrivia = match.Groups["trivia"].Value;
            var sections = match.Groups["body"].Value;
            var reader = new StringReader(sections);
            var line = reader.ReadLine();
            ConfigurationSection section = null;
            while (line != null)
            {
                line = line.Trim();

                if (line.StartsWith("["))
                {
                    var title = Regex.Match(line, @"(?<=^\[).*?(?=\]|$)", RegexOptions.Singleline).Value;
                    section = this[title];
                }
                else if (section == null) throw new ArgumentException("Not supported");
                else if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                {
                    section.AddTrivia(line);
                }
                else
                {
                    var tokens = line.Split('=').Select(token => token.Trim()).ToArray();
                    if (tokens.Length != 2) section.AddTrivia(line);
                    else
                    {
                        section[tokens[0]] = tokens[1];
                    }
                }
                line = reader.ReadLine();
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.Append(leadingTrivia);
            foreach (var section in this)
            {
                section.Value.Write(sb);
            }
            if (IsWslFile) Shell.WriteAllText(File, sb.ToString());
        }
        public override ConfigurationSection this[string section]
        {
            get
            {
                section = ToTitleCase(section);
                var config = base[section];
                if (section == null)
                {
                    var sectionType = Type.GetType($"HostPanelPro.Providers.OS.WSLShell.{section}Section, HostPanelPro.Providers.Base");
                    if (sectionType != null) config = Activator.CreateInstance(sectionType) as ConfigurationSection;
                    else config = new ConfigurationSection();
                    base[section] = config;
                }
                return config;
            }
            set => base[ToTitleCase(section)] = value;
        }
    }

    public class WSLConfiguration : ConfigurationBase
    {
        public WSLConfiguration(WSLShell shell) : base(shell) { }
        public override string File => "/etc/wsl.conf";
        protected override bool IsWslFile => true;
        public BootSection Boot => (BootSection)this[nameof(Boot)];
        public AutomountSection Automount => (AutomountSection)this[nameof(Automount)];
        public NetworkSection Network => (NetworkSection)this[nameof(Network)];
        public InteropSection Interop => (InteropSection)this[nameof(Interop)];
        public UserSection User => (UserSection)this[nameof(User)];
    }

    public class WSLGlobalConfiguration : ConfigurationBase
    {
        public WSLGlobalConfiguration(WSLShell shell) : base(shell) { }
        public WSLGlobalConfiguration(WSLShell shell, string user) { User = user; Shell = shell; Open(); }
        public virtual string User { get; set; } = null;
        public override string File => User == null ?
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".wslconfig") :
            Path.Combine(Path.GetDirectoryName(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)), User, ".wslconfig");
        protected override bool IsWslFile => false;
        public Wsl2Section Wsl2 => (Wsl2Section)this[nameof(Wsl2)];
        public ExperimentalSection Experimental => (ExperimentalSection)this[nameof(Experimental)];
    }
    public bool Debug { get; set; } = false;

    public override string ShellExe
    {
        get
        {
            if (!IsWindows) return "";

            string user = "";
            if (!string.IsNullOrEmpty(User)) user = $" --user {User}";
            if (IsOldVersion)
            {
                return CurrentDistro == WSLDistros.Default ? $"wsl{user} --exec" : $"wsl --distribution {CurrentDistroName}{user} --exec";
            }
            else return CurrentDistro == WSLDistros.Default ? $"wsl{user}" : $"wsl --distribution {CurrentDistroName}{user}";
        }
    }
    public string User { get; set; } = null;
    public WSLShell() : base() => BaseShell = Shell.Standard.Clone;
    public WSLShell(WSLDistro distro) : this() => Use(distro);

    Shell baseShell = null;

    public Shell BaseShell
    {
        get => baseShell;
        set
        {
            if (baseShell != value)
            {
                if (baseShell != null)
                {
                    baseShell.Log -= OnBaseLog;
                    baseShell.LogCommandEnd -= OnBaseLogCommandEnd;
                    baseShell.LogOutput -= OnBaseLogOutput;
                    baseShell.LogError -= OnBaseLogError;
                }
                baseShell = value;
                if (value != null)
                {
                    value.Log += OnBaseLog;
                    value.LogCommandEnd += OnBaseLogCommandEnd;
                    value.LogOutput += OnBaseLogOutput;
                    value.LogError += OnBaseLogError;
                    value.Redirect = false;
                    value.LogFile = null;
                }
            }
        }
    }

    public static string GetDistroName(WSLDistro distro)
    {
        if (!IsWindows) return "unix";

        switch (distro.Distro)
        {
            default:
            case WSLDistros.Default: return "wsl";
            case WSLDistros.Ubuntu: return "Ubuntu";
            case WSLDistros.Debian: return "Debian";
            case WSLDistros.Kali: return "kali-linux";
            case WSLDistros.Ubuntu18: return "Ubuntu-18.04";
            case WSLDistros.Ubuntu20: return "Ubuntu-20.04";
            case WSLDistros.Ubuntu22: return "Ubuntu-22.04";
            case WSLDistros.Ubuntu24: return "Ubuntu-24.04";
            case WSLDistros.Oracle7: return "OracleLinux_7_9";
            case WSLDistros.Oracle8: return "OracleLinux_8_10";
            case WSLDistros.Oracle9: return "OracleLinux_9_5";
            case WSLDistros.openSUSELeap: return "openSUSE-Leap-16.0";
            case WSLDistros.SUSE_15: return "SUSE-Linux-Enterprise-Server-15-SP7";
            case WSLDistros.SUSE_16: return "SUSE-Linux-Enterprise-16.0";
            case WSLDistros.openSUSEThumbleweed: return "openSUSE-Tumbleweed";
            case WSLDistros.Fedora: return "Fedora43";
            case WSLDistros.FedoraRemix: return "fedoraremix";
            case WSLDistros.Alpine: return "Alpine";
            case WSLDistros.AlmaLinux: return "AlmaLinux-10";
            case WSLDistros.Arch: return "archlinux";
            case WSLDistros.eLxr: return "eLxr";
            case WSLDistros.Native: return "unix";
            case WSLDistros.Other: return distro.OtherDistroName;
        }
    }
    protected string DistroName(WSLDistro distro) => GetDistroName(distro);
    public WSLDistro CurrentDistro { get; set; } = IsWindows ? WSLDistros.Default : WSLDistros.Native;
    public string CurrentDistroName => DistroName(CurrentDistro);
    public void Use(WSLDistro distro) => CurrentDistro = distro;
    public WSLShell For(WSLDistro distro)
    {
        var clone = (WSLShell)Clone;
        clone.CurrentDistro = distro;
        return clone;
    }
    protected string WSLList
    {
        get
        {
            if (IsWindows)
            {
                if (!IsOldVersion) return BaseShell.SilentClone.Exec($"wsl --list --verbose", Encoding.Unicode).Output().Result;
                else return BaseShell.SilentClone.Exec($"wslconfig /l", Encoding.Unicode).Output().Result;
            }
            return "";
        }
    }
    public WSLDistro[] InstalledDistros
    {
        get
        {
            if (IsWindows)
            {
                if (base.Find("wsl") == null) return new WSLDistro[0];

                if (!IsOldVersion) return Regex.Matches(WSLList, @"(?<=\n\*?\s+)[^\s]+")
                    .OfType<Match>()
                    .Select(match => (WSLDistro)match.Value)
                    .ToArray();

                // Old WSL version
                var list = WSLList;
                if (list.Contains("Windows Subsystem for Linux has no installed distributions.")) return new WSLDistro[0];
                return list.Split('\n')
                    .Skip(1)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Select(line => Regex.Match(line, @"^.*?(?=(?:\s*\(Default\))?\s*$)").Value)
                    .Select(name => (WSLDistro)name)
                    .ToArray();
            }
            return new[] { (WSLDistro)"unix" };
        }
    }
    public bool IsWslInstalled => IsWindows && base.Find("wsl") != null;
    public WSLDistro DefaultDistro => IsWindows ?
        (!IsWslInstalled ? null : Regex.Match(WSLList, @"(?<=\n\*\s+)[^\s]+").Value) :
        "unix";
    public bool IsInstalled(WSLDistro distro) => !IsWindows || Regex.IsMatch(WSLList, $@"^\*?\s+{Regex.Escape(distro)}\s", RegexOptions.IgnoreCase | RegexOptions.Multiline);
    public bool IsInstalledAny() => !IsWindows || IsWslInstalled && InstalledDistros.Length > 0;
    public bool IsInstalled() => !IsWindows ||
        (CurrentDistro.Distro == WSLDistros.Default && IsInstalledAny()) ||
        IsInstalled(CurrentDistroName);
    public void UpdateWsl() => BaseShell.Exec("wsl --update", Encoding.Unicode);
    public void ShutdownAll()
    {
        if (IsWindows) BaseShell.Exec("wsl --shutdown", Encoding.Unicode);
    }
    public void Terminate(WSLDistro distro = null)
    {
        distro ??= CurrentDistro;
        if (IsWindows) BaseShell.Exec($"wsl --terminate {distro}", Encoding.Unicode);
    }
    public async Task<WSLDistro> Install(string user, string password, WSLDistro distro = null, string name = null)
    {
        distro ??= CurrentDistro;
        var namearg = !string.IsNullOrEmpty(name) ? $" --name {name}" : "";
        if (IsWindows)
        {
            if (distro.Distro == WSLDistros.FedoraRemix) await BaseExecAsync(@"winget install ""Fedora Remix for WSL"" --accept-source-agreements --accept-package-agreements");
            else if (distro.Distro == WSLDistros.Alpine) await BaseExecAsync(@"winget install ""Alpine WSL"" --accept-source-agreements --accept-package-agreements");
            else if (distro.Distro == WSLDistros.AlmaLinux) await BaseExecAsync(@"winget install ""AlmaLinux OS 9"" --accept-source-agreements --accept-package-agreements");
            else
            {
                var shell = BaseExecAsync($"wsl --install {distro} {namearg}", Encoding.Unicode);
                while (!(await shell.OutputAsync()).Contains("acccount:")) await System.Threading.Tasks.Task.Delay(100);
                shell.Input.WriteLine(user);
                while (!(await shell.OutputAsync()).Contains("password:")) await System.Threading.Tasks.Task.Delay(100);
                shell.Input.WriteLine(password);
                while (!(await shell.OutputAsync()).Contains("Retype new password:")) await System.Threading.Tasks.Task.Delay(100);
                shell.Input.WriteLine(password);
                shell.Input.WriteLine("exit");
                await shell;
                return !string.IsNullOrEmpty(name) ? new WSLDistro() { Distro = WSLDistros.Other, OtherDistroName = name } : distro;
            }
            return distro;
        }
        else if (IsLinux || IsMac) return new WSLDistro() { Distro = WSLDistros.Native, OtherDistroName = name };
        return distro;
    }
    public void SetDefaultVersion(int n)
    {
        if (IsWindows) BaseExec($"wsl --set-default-version {n}", Encoding.Unicode);
    }
    public void Uninstall(WSLDistro distro = null)
    {
        distro ??= CurrentDistro;
        if (IsWindows) BaseExec($"wsl --unregister {distro}", Encoding.Unicode);
    }

    public WSLShell Import(string file, WSLDistro distro = null)
    {
        distro ??= CurrentDistro;
        string format = "";
        if (file.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase)) format = " --vhd";
        if (IsWindows)
        {
            var path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            path = Path.Combine(path, "WSL", distro);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            BaseExec($"wsl --import {distro} \"{path}\" {file}{format}", Encoding.Unicode);
        }
        if (distro == CurrentDistro) return this;
        else
        {
            var test = (WSLShell)Clone;
            test.CurrentDistro = distro;
            return test;
        }
    }

    public Shell BaseExec(string cmd, Encoding encoding = null)
    {
        Shell parent = this;
        while (parent != null)
        {
            parent.LogCommand?.Invoke(cmd);
            parent = parent.Parent;
        }
        return BaseShell.Exec(cmd, encoding);
    }
    public Shell BaseExecAsync(string cmd, Encoding encoding = null)
    {
        Shell parent = this;
        while (parent != null)
        {
            parent.LogCommand?.Invoke(cmd);
            parent = parent.Parent;
        }
        return BaseShell.ExecAsync(cmd, encoding);
    }

    public void Export(string file, WSLDistro distro = null)
    {
        distro ??= CurrentDistro;
        string format = "";
        var match = Regex.Match(file, @"\.(tar|tar\.gz|tar\.xz|vhd|vhdx)$");
        if (match.Success)
        {
            format = match.Groups[1].Value;
            if (format == "vhdx") format = "vhd";
            format = $" --format {format}";
        }
        if (IsWindows) BaseExec($"wsl --export {distro} {file}{format}", Encoding.Unicode);
    }
    public string ReadAllText(string path)
    {
        if (IsWindows)
        {
            var tmpfile = Path.GetTempFileName();
            Exec($"cp \"{path}\" \"{WSLPath(tmpfile)}\"");
            var txt = File.ReadAllText(tmpfile);
            File.Delete(tmpfile);
            return txt;
        }
        else return File.ReadAllText(path);
    }
    public void WriteAllText(string path, string content)
    {
        if (IsWindows)
        {
            var tmpfile = Path.GetTempFileName();
            File.WriteAllText(tmpfile, content);
            var shell = Exec($"cp \"{WSLPath(tmpfile)}\" \"{path}\"");
            File.Delete(tmpfile);
        }
        else File.WriteAllText(path, content);
    }

    public bool FileExists(string path)
    {
        if (IsWindows) return Exec($"test -f \"{path}\"").ExitCode().Result == 0;
        else return File.Exists(path);
    }
    public bool DirectoryExists(string path)
    {
        if (IsWindows) return Exec($"test -d \"{path}\"").ExitCode().Result == 0;
        else return Directory.Exists(path);
    }

    WSLConfiguration wslConfiguration = null;
    WSLGlobalConfiguration wslGlobalConfiguration = null;
    public WSLConfiguration Configuration
    {
        get => IsWindows ?
            wslConfiguration ?? (wslConfiguration = new WSLConfiguration(this)) :
            throw new PlatformNotSupportedException("Configuration is only available on Windows");
        private set
        {
            if (IsWindows) wslConfiguration = value;
            else throw new PlatformNotSupportedException("Configuration is only available on Windows");
        }
    }
    public WSLGlobalConfiguration GlobalConfiguration => IsWindows ?
        wslGlobalConfiguration ?? (wslGlobalConfiguration = new WSLGlobalConfiguration(this)) :
        throw new PlatformNotSupportedException("GlobalConfiguration is only available on Windows");
    public WSLGlobalConfiguration GlobalConfigurationFor(string user) => IsWindows ? new WSLGlobalConfiguration(this, user) :
        throw new PlatformNotSupportedException("GlobalConfiguration is only available on Windows");
    public string WSLPath(string path) => IsWindows ? Regex.Replace(Path.GetFullPath(path), "^(?<drive>[A-Z]):",
        match => $"/mnt/{match.Groups["drive"].Value.ToLower()}", RegexOptions.IgnoreCase | RegexOptions.Singleline)
        .Replace(Path.DirectorySeparatorChar, '/') :
        path;
    public override string WorkingDirectory
    {
        get { return base.WorkingDirectory; }
        set { BaseShell.WorkingDirectory = base.WorkingDirectory = value; }
    }
    protected override string ToTempFile(string script)
    {
        script = script.Replace(System.Environment.NewLine, "\n");
        var localTmp = base.ToTempFile(script);
        return WSLPath(localTmp);
    }

    static string QuoteWindowsArg(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        // Escape backslashes and quotes
        arg = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{arg}\"";
    }

    public override Shell ExecAsync(string command, Encoding encoding = null, Dictionary<string, string> environment = null)
    {
        Shell parent = this;
        while (parent != null)
        {
            parent.LogCommand?.Invoke(command);
            parent = parent.Parent;
        }

        if (IsWindows)
        {
            if (command.StartsWith("which "))
            {
                return BaseShell.ExecAsync($"{ShellExe} {command}", encoding, environment);
            }
            else
            {
                var cmd = Find("bash") != null ? "bash" : (Find("ash") != null ? "ash" : "sh");

                return BaseShell.ExecAsync($"{ShellExe} {cmd} -lc {QuoteWindowsArg(command)}", encoding, environment);
            }
        }
        else // System is already unix, do not use WSL
        {
            return BaseShell.ExecAsync(command, encoding, environment);
        }
    }
    public override Shell ExecScriptAsync(string script, string args = null, Encoding encoding = null, Dictionary<string, string> environment = null)
    {
        var cmd = Find("bash") != null ? "bash" : (Find("ash") != null ? "ash" : "sh");

        LogCommand?.Invoke($"{cmd} {script}");

        script = script.Trim().Replace("\r", "");
        var file = ToTempFile(script.Trim());
        if (!IsWindows)
        {
            SetFilePermissions(file, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var shell = BaseShell.ExecAsync($"{ShellExe} {cmd} -lc \"{file}\"".TrimStart(), encoding, environment);
        if (shell.Process != null)
        {
            if (IsWindows)
            {
                file = Regex.Replace(file, "^/mnt/(?<drive>[a-zA-Z])/", m => m.Groups["drive"].Value.ToUpper() + ":\\")
                    .Replace('/', Path.DirectorySeparatorChar);
            }
            shell.Process.Exited += (sender, args) => File.Delete(file);
            if (shell.Process.HasExited && File.Exists(file)) File.Delete(file);
        }
        return shell;
    }

    public override Shell Clone
    {
        get
        {
            var clone = (WSLShell)base.Clone;
            clone.CurrentDistro = CurrentDistro;
            clone.Redirect = Redirect;
            clone.LogFile = LogFile;
            clone.Debug = Debug;
            clone.BaseShell = BaseShell.Clone;
            clone.User = User;
            return clone;
        }
    }
    public override Shell SilentClone
    {
        get
        {
            var clone = (WSLShell)base.SilentClone;
            clone.BaseShell = BaseShell.SilentClone;
            clone.Redirect = Debug;
            clone.User = User;
            return clone;
        }
    }
    public override string Find(string cmd)
    {
        if (IsWindows)
        {
            var shell = (WSLShell)SilentClone;

            if (!shell.IsInstalled()) return null;

            var output = shell.Exec($"which {cmd}").Output().Result.Trim();
            if (string.IsNullOrEmpty(output)) return null;

            return output;
        }
        else return base.Find(cmd);

    }
    protected override void OnLogCommand(string text)
    {
        OutputAndErrorLock.Wait();
        try
        {
            text = $"{CurrentDistroName}> {text}";
            if (Redirect) Console.WriteLine(text);
            if (LogFile != null) AppendAllText(LogFile, text);
        }
        finally
        {
            OutputAndErrorLock.Release();
        }
    }
    protected void OnBaseLog(string msg) => Log?.Invoke(msg);
    protected void OnBaseLogCommandEnd() => LogCommandEnd?.Invoke();
    protected void OnBaseLogOutput(string msg) => LogOutput?.Invoke(msg);
    protected void OnBaseLogError(string msg) => LogError?.Invoke(msg);

    public new readonly static WSLShell Default = new WSLShell();

    static bool? isOldVersion = null;
    public bool IsOldVersion => isOldVersion ??= !BaseShell.SilentClone.Exec("wsl --version", Encoding.Unicode).Output().Result.Contains("WSL version:");
    public bool IsWsl1
    {
        get
        {
            if (!FileExists("/proc/version"))
            {
                //Console.WriteLine("Not running on Linux");
                return false;
            }

            var version = ReadAllText("/proc/version");

            if (version.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (version.IndexOf("microsoft-standard", StringComparison.OrdinalIgnoreCase) >= 0)
                    //Console.WriteLine("Running inside WSL2");
                    return false;
                else
                    //Console.WriteLine("Running inside WSL1");
                    return true;
            }
            else
            {
                //Console.WriteLine("Running on native Linux");
                return false;
            }
        }
    }
    public bool IsWsl2
    {
        get
        {
            if (!FileExists("/proc/version"))
            {
                //Console.WriteLine("Not running on Linux");
                return false;
            }

            var version = ReadAllText("/proc/version");

            if (version.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (version.IndexOf("microsoft-standard", StringComparison.OrdinalIgnoreCase) >= 0)
                    //Console.WriteLine("Running inside WSL2");
                    return true;
                else
                    //Console.WriteLine("Running inside WSL1");
                    return false;
            }
            else
            {
                //Console.WriteLine("Running on native Linux");
                return false;
            }
        }
    }

    public static new bool IsWindows => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
    public static bool IsMac => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);

    [System.FlagsAttribute]
    public enum UnixFileMode
    {
        None = 0,
        OtherExecute = 1,
        OtherWrite = 2,
        OtherRead = 4,
        GroupExecute = 8,
        GroupWrite = 0x10,
        GroupRead = 0x20,
        UserExecute = 0x40,
        UserWrite = 0x80,
        UserRead = 0x100,
        StickyBit = 0x200,
        SetGroup = 0x400,
        SetUser = 0x800,
        All = 0x8ff
    }

    public static void SetFilePermissions(string path, UnixFileMode mode, bool resetChildPermissions = false)
    {
        if (!resetChildPermissions)
        {
            FileSystemInfo info;
            if (File.Exists(path)) info = new FileInfo(path);
            else if (Directory.Exists(path)) info = new DirectoryInfo(path);
            else throw new FileNotFoundException(path);

            var prop = info.GetType().GetProperty("UnixFileMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(info, mode);
            info.Refresh();
        }
        else
        {
            SetFilePermissions(path, mode, false);

            foreach (var e in new DirectoryInfo(path).GetFileSystemInfos())
            {
                var prop = e.GetType().GetProperty("UnixFileMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                prop?.SetValue(e, mode);
                e.Refresh();
            }
        }
    }
}
