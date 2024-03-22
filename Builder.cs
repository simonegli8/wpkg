using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WindowsPackager.ARFileFormat;
using ICSharpCode.SharpZipLib.Tar;

namespace WindowsPackager
{
	class Builder
	{
		private const LFileMode ArFileMode = LFileMode.S_IRUSR | LFileMode.S_IWUSR | LFileMode.S_IRGRP | LFileMode.S_IROTH | LFileMode.S_IFREG;
		private static string LOCAL_DIR = Environment.CurrentDirectory;
		private static string DebFileName;
		private const int EXIT_FILE_ERROR = 500;
		private const string ERRMSG_FILE_FAILURE = "E: Specified file does not exist! Aborting...";

		public static void BuildPackage(string PathToPackage)
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
			ARFileCreator.WriteEntry(DebFileStream, "control.tar", ArFileMode, ControlAsStream);
			ARFileCreator.WriteEntry(DebFileStream, "data.tar", ArFileMode, DataAsStream);

			var fs = File.Create(WorkingDirectory + "\\" + DebFileName);
			DebFileStream.Seek(0, SeekOrigin.Begin);
			DebFileStream.CopyTo(fs);
			fs.Close();

			ControlAsStream.Close();
			DataAsStream.Close();

			File.Delete(WorkingDirectory + "\\control.tar");
			File.Delete(WorkingDirectory + "\\data.tar");
		}

		public static void AddToTar(TarArchive arch, FileSystemInfo info)
		{
			TarEntry entry = TarEntry.CreateEntryFromFile(info.FullName);
			entry.UserId = 0;
			entry.UserName = "root";
			entry.GroupId = 0;
			entry.GroupName = "root";
			if (info is FileInfo) {
				if (info.FullName.Contains("/usr/bin/") || info.FullName.EndsWith(".exe")) entry.TarHeader.Mode = 0755;
				else entry.TarHeader.Mode = 0644;
			} else entry.TarHeader.Mode = 0755;
			arch.WriteEntry(entry, false);

			if (info is DirectoryInfo dir)
			{
				foreach (var subinfo in dir.EnumerateFileSystemInfos()) AddToTar(arch, subinfo);
			}
		}
		public static void BuildDataTarball(string directory)
		{
			string TarballName = "data.tar";
			Stream outStream = File.Create(directory + "\\" + TarballName);
			Stream tarballStream = new TarOutputStream(outStream);
			TarArchive dataTar = TarArchive.CreateOutputTarArchive(tarballStream);

			// fix str (mandatory hotfix due to SharpZipLib)
			dataTar.RootPath = directory.Replace('\\', '/');
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
	
				AddToTar(dataTar, dir);
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
			controlTar.RootPath = directory.Replace('\\', '/');
			if (controlTar.RootPath.EndsWith("/"))
			{
				controlTar.RootPath = controlTar.RootPath.Remove(controlTar.RootPath.Length - 1);
			}

			// generate filename
			var control = File.ReadAllText(directory + "\\DEBIAN\\control");
			DebFileName = Regex.Match(control, "(?<=^Package:\\s*).*$", RegexOptions.Multiline).Value + ".deb";
			Console.WriteLine("Building " + DebFileName + " ...");

			// scan for eligible control.tar entries & add them
			var files = new DirectoryInfo(directory + "\\DEBIAN").EnumerateFiles();
			foreach (var item in files)
			{
				var fn = item.Name;
				if (fn.Equals("control") || fn.Equals("preinst") || fn.Equals("postinst") || fn.Equals("prerm") || fn.Equals("postrm"))
				{
					// DEBUG: Console.WriteLine("Found match: " + fn);

					AddToTar(controlTar, item);
				}
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
