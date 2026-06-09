using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsPackager;


public class DefaultShell : Shell
{
	public override string ShellExe => WSLShell.IsWindows ? "Cmd" : "bash";
}