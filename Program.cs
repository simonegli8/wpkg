using System;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsPackager
{

	public class Program
	{
		private static string LOCAL_DIR = Environment.CurrentDirectory;
		private static string[] ControlElements = {
				"Package: com.yourcompany.identifier",
				"Name: Name of the product",
				"Depends: ",
				"Architecture: any",
				"Description: This is a sample short description",
				"Maintainer: Maintainer Name",
				"Author: Author Name",
				"Section: Section",
				"Version: 1.0"
		  };
		private const string CREATE_DEBIAN_PACKAGE = "-b";
		private const string CREATE_RPM_PACKAGE = "-r";
		private const string CONVERT_DOS2UNIX = "-d2u";
		private const string EXTRACT_DEBIAN_PACKAGE = "-x";
		private const string THEME_DEB = "--theme";
		private const string HELPTEXT = "-h";
		private const string ERRMSG_DIR_FAILURE = "E: Directory was not found! Aborting...";
		private const string ERRMSG_FILE_FAILURE = "E: Specified file does not exist! Aborting...";
		private const string ERRMSG_ARGC_FAILURE = "E: Mismatch in arguments! (perhaps missing one or one too much?) Aborting...";
		private const string ERRMSG_DEB_FAILURE = "E: File is not a Debian Binary! Aborting...";
		private const string ERRMSG_STRUCT_FAILURE = "E: Directory does NOT match a standard structure! (Perhaps missing control?) Aborting...";
		public const string ERRMSG_IO_FAILURE = "E: Cannot read or write to file {0}! {1} Aborting...";
		private const int EXIT_ARGS_MISMATCH = 100;
		private const int EXIT_DIR_ERROR = 200;
		private const int EXIT_DEBFILE_ERROR = 300;
		private const int EXIT_STRUCT_ERROR = 400;
		public const int EXIT_IO_ERROR = 500;

		public static string GetCaseSensitivePath(string path)
		{
			var root = Path.GetPathRoot(path);
			try
			{
				foreach (var name in path.Substring(root.Length).Split(Path.DirectorySeparatorChar))
					root = Directory.GetFileSystemEntries(root, name).First();
			}
			catch (Exception e)
			{
				// Log("Path not found: " + path);
				root += path.Substring(root.Length);
			}
			return root;
		}

		static void Main(string[] args)
		{
			var cwd = Environment.CurrentDirectory;
			Environment.CurrentDirectory = Path.GetPathRoot(cwd);
			Environment.CurrentDirectory = GetCaseSensitivePath(cwd);

			// check because switch
			if (args.Length == 0)
			{
				InfoMessage();
				Environment.Exit(-1);
			}
			switch (args[0])
			{
				case CONVERT_DOS2UNIX:
					if (args.Length == 2)
					{
						Builder.Dos2Unix(args[1].Split(';', ','));
					}
					else
					{
						ExitWithMessage(ERRMSG_ARGC_FAILURE, EXIT_ARGS_MISMATCH);
					}
					break;
				case CREATE_RPM_PACKAGE:
					if (args.Length == 2)
					{
						if (Directory.Exists(args[1]))
						{
							BuilderRPMType(args[1], true);
						}
						else
						{
							ExitWithMessage(ERRMSG_DIR_FAILURE, EXIT_DIR_ERROR);
						}
					}
					else
					{
						BuilderRPMType(null, false);
					}
					break;
				case CREATE_DEBIAN_PACKAGE:
					if (args.Length == 2)
					{
						if (Directory.Exists(args[1]))
						{
							BuilderDebType(args[1], true);
						}
						else
						{
							ExitWithMessage(ERRMSG_DIR_FAILURE, EXIT_DIR_ERROR);
						}
					}
					else
					{
						BuilderDebType(null, false);
					}
					break;
				case EXTRACT_DEBIAN_PACKAGE:
					if (args.Length == 3)
					{
						// get properly formatted Path
						string[] cmdargs = Environment.GetCommandLineArgs();
						// check if file exists & create extraction stream
						if (File.Exists(cmdargs[2]) && Directory.Exists(cmdargs[3]))
						{
							ExtractorType(cmdargs[2], null, cmdargs[3]);
						}
						else
						{
							ExitWithMessage(ERRMSG_ARGC_FAILURE, EXIT_ARGS_MISMATCH);
						}
					}
					else if (args.Length == 2)
					{
						// check if we have a path or direct filename => file cannot contain the '\' char
						if (args[1].Contains("\\"))
						{
							if (File.Exists(args[1]))
							{
								ExtractorType(args[1], null, Path.GetDirectoryName(args[1]));
							}
							else
							{
								ExitWithMessage(ERRMSG_ARGC_FAILURE, EXIT_ARGS_MISMATCH);
							}
						}
						else
						{
							if (File.Exists(LOCAL_DIR + "\\" + args[1]))
							{
								ExtractorType(LOCAL_DIR + "\\" + args[1], args[1], null);
							}
							else
							{
								ExitWithMessage(ERRMSG_FILE_FAILURE, EXIT_DEBFILE_ERROR);
							}
						}
					}
					break;
				case THEME_DEB:
					if (args.Length != 2)
					{
						ExitWithMessage(ERRMSG_ARGC_FAILURE, EXIT_ARGS_MISMATCH);
					}
					// create base theme dir
					string target = LOCAL_DIR + "\\Library\\Themes\\" + args[1] + ".theme";
					Directory.CreateDirectory(target);
					// create the necessary subdirs
					Directory.CreateDirectory(target + "\\IconBundles");
					Directory.CreateDirectory(target + "\\Bundles\\com.apple.springboard");
					GenerateControlFile(LOCAL_DIR);
					break;
				case HELPTEXT:
					InfoMessage();
					break;
				default:
					InfoMessage();
					break;
			}
		}

		private static void BuilderDebType(string WorkDir, bool IsSpecified)
		{
			string dir = (IsSpecified) ? WorkDir : LOCAL_DIR;
			VerifyStructure(dir);
			Builder.BuildControlTarball(dir);
			Builder.BuildDataTarball(dir);
			Builder.BuildDebPackage(dir);
		}
		private static void BuilderRPMType(string WorkDir, bool IsSpecified)
		{
			string dir = (IsSpecified) ? WorkDir : LOCAL_DIR;
			Builder.BuildRPMPackage(dir);
		}

		private static void ExtractorType(string PassedFilePath, string FileName, string TargetDirectory)
		{
			VerifyFile(PassedFilePath);
			Extractor.DebName = Path.GetFileNameWithoutExtension(PassedFilePath);
			if (String.IsNullOrEmpty(TargetDirectory))
			{
				Stream DebFileStream = Builder.CreateStream(FileName);
				Extractor.ExtractEverything(DebFileStream, LOCAL_DIR);
			}
			else
			{
				Stream DebFileStream = Builder.CreateStream(PassedFilePath, 3);
				Extractor.ExtractEverything(DebFileStream, TargetDirectory);
			}
		}

		private static void VerifyFile(string PathToFile)
		{
			if (Extractor.IsDebianBinary(PathToFile) == false)
			{
				ExitWithMessage(ERRMSG_DEB_FAILURE, EXIT_DEBFILE_ERROR);
			}
		}

		public static void VerifyStructure(string directory)
		{
			int passed = 0;
			// check if we AT LEAST have 1 dir
			DirectoryInfo[] subdirs = new DirectoryInfo(directory).GetDirectories();
			if (subdirs.Length > 0)
			{
				passed++;
			}
			// check if we have a control file
			if (File.Exists(directory + "\\DEBIAN\\control"))
			{
				passed++;
			}
			// check if our struct matches
			if (passed != 2)
			{
				ExitWithMessage(ERRMSG_STRUCT_FAILURE, EXIT_STRUCT_ERROR);
			}
		}

		private static void GenerateControlFile(string WorkingDir)
		{
			File.WriteAllLines(WorkingDir + "\\control", ControlElements, Encoding.ASCII);
		}

		public static void ExitWithMessage(string Message, int ExitCode)
		{
			Console.WriteLine(Message);
			Environment.Exit(ExitCode);
		}

		private static void InfoMessage()
		{
			Console.WriteLine("Windows Packager (wpkg) v2.0 Guide");
			ColorizedMessage("Building:\n" +
				 "wpkg -b            - Build .deb inside the local directory\n" +
				 "wpkg -b <Path>     - Build .deb in the given path\n" +
				 "wpkg -r            - Build .rpm inside the local directory\n" +
				 "wpkg -r <Path>     - Build .rpm in the given path." +
				 "  For rpm creation:" +
				 "  The .spec file must reside in the SPECS folder. For this to\n" +
				 "	 work, you need to have an WSL distro with rpmbuild installed.\n",
				 ConsoleColor.DarkCyan);
			ColorizedMessage("Extraction:\n" +
				 "wpkg -x <PathToDeb> <DestFolder>   - Extract .deb to given path\n" +
				 "wpkg -x <PathToDeb>                - Extract .deb inside the original folder\n" +
				 "wpkg -x <DebfileName>              - Extract a .deb inside the folder you're in*\n" +
				 " *: only works if you're in the same folder as the .deb!\n",
				 ConsoleColor.DarkGreen);
			ColorizedMessage("Extras:\n" +
				 "wpkg -h                    - Show this helptext\n" +
				 "wpkg -d2u file1;file2;...  - Convert files from DOS to Unix\n" +
				 "wpkg --theme               - Create a base for an iOS Theme\n" +
				 "  in the directory you are currently\n",
				 ConsoleColor.DarkMagenta);
			ColorizedMessage("If you stumble upon an error, please send an email at\n" +
				 "simon.jakob.egli@gmail.com\n",
				 ConsoleColor.DarkRed);
		}

		private static void ColorizedMessage(string Message, ConsoleColor cColor)
		{
			Console.ForegroundColor = cColor;
			Console.WriteLine(Message);
			Console.ForegroundColor = ConsoleColor.White;
		}

		// <-- FIN -->
	}
}
