using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WindowsPackager.ARFileFormat;
using ICSharpCode.SharpZipLib.Tar;
using System.IO.Compression;
using SolidCP.Providers.OS;

namespace WindowsPackager
{

	class CmdShell: Shell
	{
		public override string ShellExe => "Cmd";
	} 

	class Builder
	{
		private const LFileMode ArFileMode = LFileMode.S_IRUSR | LFileMode.S_IWUSR | LFileMode.S_IRGRP | LFileMode.S_IROTH | LFileMode.S_IFREG;
		private static string LOCAL_DIR = Environment.CurrentDirectory;
		private static string DebFileName;
		private const int EXIT_FILE_ERROR = 500;
		private const string ERRMSG_FILE_FAILURE = "E: Specified file does not exist! Aborting...";
		private const string ERRMSG_SPEC_FAILURE = "E: No spec file found! Aborting...";
		private const string ERRMSG_NAME_FAILURE = "E: Spec file contains no name! Aborting...";
		private const string ERRMSG_WSL_FAILURE = "E: No installed Fedora WSL distro found! Aborting...";
		private const int EXIT_SPEC_ERROR = 600;
		private const int EXIT_NAME_ERROR = 700;
		private const int EXIT_WSL_ERROR = 700;

		private static string WSLPath(string path) => Regex.Replace(Path.GetFullPath(path), "^(?<drive>[A-Z]):",
			match => $"/mnt/{match.Groups["drive"].Value.ToLower()}", RegexOptions.IgnoreCase | RegexOptions.Singleline)
			.Replace(Path.DirectorySeparatorChar, '/');
		public static void BuildRPMPackage(string PathToPackage)
		{
			string WorkingDirectory = "";
			if (String.IsNullOrEmpty(PathToPackage))
			{
				WorkingDirectory = LOCAL_DIR;
			}
			else
			{
				WorkingDirectory = PathToPackage;
			}
			var specFile = Directory.EnumerateFiles(WorkingDirectory + "\\rpmbuild\\SPECS", "*.spec").FirstOrDefault();
			if (specFile == null) Program.ExitWithMessage(ERRMSG_SPEC_FAILURE, EXIT_SPEC_ERROR);
			var spec = File.ReadAllText(specFile);
			var packageName = Regex.Match(spec, "(?<=^Name:\\s*).*$", RegexOptions.Multiline)?.Value.Trim();
			if (packageName == null) Program.ExitWithMessage(ERRMSG_NAME_FAILURE, EXIT_NAME_ERROR);
			var packageVersion = Regex.Match(spec, "(?<=^Version:\\s*).*$", RegexOptions.Multiline)?.Value.Trim();

			var dataDir = $"{WorkingDirectory}\\data";
			var tmpDir = $"{WorkingDirectory}\\tmp\\{packageName}-{packageVersion}";
			Directory.CreateDirectory($"{WorkingDirectory}\\tmp");
			Directory.Move(dataDir, tmpDir);
			BuildDataTarball(WorkingDirectory + "\\tmp", false);
			Directory.Move(tmpDir, dataDir);

			var srcFile = $"{WorkingDirectory}\\rpmbuild\\SOURCES\\{packageName}-{packageVersion}.tar.gz";
			using (var DataAsStream = CreateStream(WorkingDirectory+"\\tmp", 1))
			using (var pkg = File.Create(srcFile))
			{
				DataAsStream.CopyTo(pkg);
			}

			File.Delete($"{WorkingDirectory}\\tmp\\data.tar");
			File.Delete($"{WorkingDirectory}\\tmp\\data.tar.gz");
			Directory.Delete($"{WorkingDirectory}\\tmp");

			var shell = new CmdShell();
			var flavors = new string[] { "fedoraremix", "fedora", "OracleLinux_7_9", "OracleLinux_8_7", "OracleLinux_9_1", "openSUSE-Leap-15.5", "SUSE-Linux-Enterprise-Server-15-SP4",
			"SUSE-Linux-Enterprise-15-SP5", "openSUSE-Tumbleweed" };
			var linuxshell = flavors.FirstOrDefault(sh => shell.Find(sh) != null);
			if (linuxshell == null) Program.ExitWithMessage(ERRMSG_WSL_FAILURE, EXIT_WSL_ERROR);

			var homeSpecFile = specFile.Replace(WorkingDirectory, "~").Replace(Path.DirectorySeparatorChar, '/');
			var homeSrcFile = srcFile.Replace(WorkingDirectory, "~").Replace(Path.DirectorySeparatorChar, '/');

			var cmd = $@"{linuxshell} -c cp ""{WSLPath(specFile)}"" ""{homeSpecFile}""";
			Console.WriteLine(cmd);
			var output = shell.Exec(cmd).OutputAndError().Result;
			Console.WriteLine(output);

			cmd = $@"{linuxshell} -c cp ""{WSLPath(srcFile)}"" ""{homeSrcFile}""";
			Console.WriteLine(cmd);
			output = shell.Exec(cmd).OutputAndError().Result;
			Console.WriteLine(output);

			cmd = $@"{linuxshell} -c rpmlint ""{homeSpecFile}""";
			Console.WriteLine(cmd);
			output = shell.Exec(cmd).OutputAndError().Result;
			Console.WriteLine(output);

			cmd = $@"{linuxshell} -c rpmbuild -bb ""{homeSpecFile}""";
			Console.WriteLine(cmd);
			output = shell.Exec(cmd).OutputAndError().Result;
			Console.WriteLine(output);

			cmd = $@"{linuxshell} -c cp -r ~/rpmbuild/RPMS/* ""{WSLPath($"{WorkingDirectory}\\rpmbuild\\RPMS")}""";
			Console.WriteLine(cmd);
			output = shell.Exec(cmd).OutputAndError().Result;
			Console.WriteLine(output);

		}
		public static void BuildDebPackage(string PathToPackage)
		{
			string WorkingDirectory = "";
			if (String.IsNullOrEmpty(PathToPackage))
			{
				WorkingDirectory = LOCAL_DIR;
			}
			else
			{
				WorkingDirectory = PathToPackage;
			}
			//Program.VerifyStructure(WorkingDirectory);

			Version DebianVersion = new Version(2, 0);
			Stream DebFileStream = new MemoryStream();

			Stream ControlAsStream = CreateStream(WorkingDirectory, 0);
			Stream DataAsStream = CreateStream(WorkingDirectory, 1);

			ARFileCreator.WriteMagic(DebFileStream);
			ARFileCreator.WriteEntry(DebFileStream, "debian-binary", ArFileMode, DebianVersion + "\n");
			ARFileCreator.WriteEntry(DebFileStream, "control.tar.gz", ArFileMode, ControlAsStream);
			ARFileCreator.WriteEntry(DebFileStream, "data.tar.gz", ArFileMode, DataAsStream);

			var fs = File.Create(WorkingDirectory + "\\" + DebFileName);
			DebFileStream.Seek(0, SeekOrigin.Begin);
			DebFileStream.CopyTo(fs);
			fs.Close();

			ControlAsStream.Close();
			DataAsStream.Close();

			File.Delete(WorkingDirectory + "\\control.tar");
			File.Delete(WorkingDirectory + "\\data.tar");
			File.Delete(WorkingDirectory + "\\control.tar.gz");
			File.Delete(WorkingDirectory + "\\data.tar.gz");
			File.Delete(WorkingDirectory + "\\debian-binary");
		}

		public static void AddToTar(TarArchive arch, FileSystemInfo info, bool addDot = true)
		{
			TarEntry entry = TarEntry.CreateEntryFromFile(info.FullName);

			if (entry.Name.StartsWith(arch.RootPath+"/")) entry.Name = (addDot ? "./" : "") +entry.Name.Substring(arch.RootPath.Length+1);
			entry.UserId = 0;
			entry.UserName = "root";
			entry.GroupId = 0;
			entry.GroupName = "root";
			if (info is FileInfo) {
				if (entry.Name.Contains("/bin/") || info.FullName.EndsWith(".exe") ||
					info.Name == "preinst" || info.Name == "postinst" || info.Name == "prerm" || info.Name == "postrm") entry.TarHeader.Mode = Convert.ToInt32("755", 8);
				else entry.TarHeader.Mode = Convert.ToInt32("644", 8);
			} else entry.TarHeader.Mode = Convert.ToInt32("755", 8);
			arch.WriteEntry(entry, false);

			if (info is DirectoryInfo dir)
			{
				foreach (var subinfo in dir.EnumerateFileSystemInfos()) AddToTar(arch, subinfo, addDot);
			}
		}
		public static void BuildDataTarball(string directory, bool addDot = true)
		{
			string TarballName = "data.tar";
			Stream outStream = File.Create(directory + "\\" + TarballName);
			Stream tarballStream = new TarOutputStream(outStream);
			TarArchive dataTar = TarArchive.CreateOutputTarArchive(tarballStream);

			// fix str (mandatory hotfix due to SharpZipLib)
			dataTar.RootPath = new DirectoryInfo(directory).FullName.Replace('\\', '/');
			if (dataTar.RootPath.EndsWith("/"))
			{
				dataTar.RootPath = dataTar.RootPath.Remove(dataTar.RootPath.Length - 1);
			}

			DirectoryInfo[] subdirs = new DirectoryInfo(directory).GetDirectories();
			foreach (var dir in subdirs)
			{
				if (dir.Name.Equals("DEBIAN"))
				{
					continue;
				}
	
				AddToTar(dataTar, dir, addDot);
			}

			dataTar.Close();
		}

		public static void BuildControlTarball(string directory)
		{
			string TarballName = "control.tar";
			Stream outStream = File.Create(directory + "\\" + TarballName);
			Stream tarballStream = new TarOutputStream(outStream);
			TarArchive controlTar = TarArchive.CreateOutputTarArchive(tarballStream);

			// fix str (mandatory hotfix due to SharpZipLib)
			controlTar.RootPath = new DirectoryInfo(directory+"\\DEBIAN").FullName.Replace('\\', '/');
			if (controlTar.RootPath.EndsWith("/"))
			{
				controlTar.RootPath = controlTar.RootPath.Remove(controlTar.RootPath.Length - 1);
			}

			// generate filename
			var control = File.ReadAllText(directory + "\\DEBIAN\\control");
			DebFileName = Regex.Match(control, "(?<=^Package:\\s*)[^\\s]+.*$", RegexOptions.Multiline).Value + ".deb";
			Console.WriteLine("Building " + DebFileName + " ...");

			// scan for eligible control.tar entries & add them
			var files = new DirectoryInfo(directory + "\\DEBIAN").EnumerateFiles();
			foreach (var item in files)
			{
				var fn = item.Name;
				//if (fn.Equals("control") || fn.Equals("preinst") || fn.Equals("postinst") || fn.Equals("prerm") || fn.Equals("postrm"))
				//{
					// DEBUG: Console.WriteLine("Found match: " + fn);

					AddToTar(controlTar, item);
				//}
			}

			controlTar.Close();
		}

		public static Stream CreateStream(string FileLocation, int TypeOfStream)
		{
			string WorkingType = "";
			if (TypeOfStream == 0)
			{
				WorkingType = FileLocation + "\\control.tar";
			}
			else if (TypeOfStream == 1)
			{
				WorkingType = FileLocation + "\\data.tar";
			}
			else
			{
				WorkingType = FileLocation;
			}
			try
			{
				Stream fs = File.OpenRead(WorkingType);

				if (TypeOfStream == 0 || TypeOfStream == 1)
				{
					// gzip the tar
					using (fs)
					using (var gzip = new GZipStream(File.Create(WorkingType + ".gz"), CompressionLevel.Optimal, false)) fs.CopyTo(gzip);

					fs = File.OpenRead(WorkingType + ".gz");
				}
				return fs;
			}
			catch (FileNotFoundException)
			{
				Program.ExitWithMessage(ERRMSG_FILE_FAILURE, EXIT_FILE_ERROR);
				return null;
			}
		}

		public static Stream CreateStream(string FileName)
		{
			try
			{
				Stream fs = File.OpenRead(LOCAL_DIR + "\\" + FileName);
				return fs;
			}
			catch (FileNotFoundException)
			{
				Program.ExitWithMessage(ERRMSG_FILE_FAILURE, EXIT_FILE_ERROR);
				return null;
			}
		}
	}
}
