using Cli;

using System.Text;
namespace clasptree
{

	internal class Program
	{
		[CmdArg(Ordinal =0,ElementName ="input",Description ="The root directory of the site. Defaults to the current directory")]
		static DirectoryInfo input = new DirectoryInfo(Environment.CurrentDirectory);
		[CmdArg(Ordinal = 1, Optional =true, ElementName = "output", Description = "The output file to generate. Defaults to <stdout>")]
		static TextWriter output = Console.Out;
		[CmdArg(Name = "block", ElementName = "block", Optional = true, Description = "The function call to send a literal block to the client.")]
		static string block = "response_block";
		[CmdArg(Name = "expr", ElementName = "expr", Optional = true, Description = "The function call to send an expression to the client.")]
		static string expr = "response_expr";
		[CmdArg(Name = "state", ElementName = "state", Optional = true, Description = "The variable name that holds the user state to pass to the response functions.")]
		static string state = "response_state";
		[CmdArg(Name = "prefix", ElementName = "prefix", Optional = true, Description = "The method prefix to use, if specified.")]
		static string prefix = null;
		[CmdArg(Name = "prologue", ElementName = "prologue", Optional = true, Description = "The file to insert into each method before any code")]
		static TextReader prologue = null;
		[CmdArg(Name = "epilogue", ElementName = "epilogue", Optional = true, Description = "The file to insert into each method after any code")]
		static TextReader epilogue = null;

		[CmdArg(Group = "help", Name = "?", Description = "Displays this screen")]
		static bool help = false;
		static HashSet<string> names = new HashSet<string>();
		static string MakeSafeName(string relpath, bool local = false)
		{
			if(string.IsNullOrEmpty(relpath))
			{
				return relpath;
			}
			
			int start = 0;
			while (start<relpath.Length && relpath[start]=='/' || relpath[start] == '\\' || relpath[start]=='.')
			{
				++start;
			}
			if (start == relpath.Length)
			{
				return "";
			}
			var sb = new StringBuilder(relpath.Length-start);
			for (int i = start; i < relpath.Length; i++)
			{
				var ch = relpath[i];
				if (ch == '_' || (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
				{
					sb.Append(ch);
				}
				else
				{
					if (sb.Length>0 && sb[sb.Length - 1] != '_')
					{
						sb.Append('_');
					}
				}
			}
			var result = sb.ToString();
			if (!local)
			{
				if (names.Contains(result))
				{
					int i = 2;
					while (names.Contains(result+i.ToString()))
					{
						++i;
					}
					result += i.ToString();
				}
				names.Add(result);
			}
			return result;
		}
		
		static int Main(string[] args)
		{
#if !DEBUG
			try
			{
#endif
			using (var parsed = CliUtility.ParseAndSet(args, null, typeof(Program)))
			{
				if (help)
				{
					CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(Program)));
					return 0;
				}
				if (prefix == null) prefix = "";
				var prolStr = prologue != null ? prologue.ReadToEnd():"";
				var epilStr = epilogue != null ? epilogue.ReadToEnd() : "";
				var fia = input.GetFiles("*.*", SearchOption.AllDirectories);
				var files = new Dictionary<string, FileSystemInfo>();
				for (int i = 0; i < fia.Length; i++)
				{
					var fi = fia[i];
					var mname = fi.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					var sn = MakeSafeName(mname);
					if (!string.IsNullOrEmpty(sn))
					{
						files[sn] = fi;
					}
				}
				var fname = input.Name;
				var oname = Path.GetFileNameWithoutExtension(CliUtility.GetFilename(output));
				if (!string.IsNullOrEmpty(oname))
				{
					fname = oname;
				}
				var def = MakeSafeName(fname.ToUpperInvariant() + "_H");
				output.WriteLine("// Generated with " + CliUtility.AssemblyTitle);
				output.WriteLine($"#ifndef {def}");
				output.WriteLine($"#define {def}");
				output.WriteLine("#include <stddef.h>");
				output.WriteLine();
				output.WriteLine("#ifdef __cplusplus");
				output.WriteLine("extern \"C\" {");
				output.WriteLine("#endif");
				output.WriteLine();
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					output.WriteLine($"// ./{mname}");
					output.WriteLine($"void {prefix}{fname}_{f.Key}(void* {state});");
					
				}
				output.WriteLine();
				output.WriteLine("#ifdef __cplusplus");
				output.WriteLine("}");
				output.WriteLine("#endif");
				output.WriteLine();
				output.WriteLine($"#endif // {def}");
				output.WriteLine();
				var impl =fname.ToUpperInvariant()+"_IMPLEMENTATION";
				output.WriteLine($"#ifdef {impl}");
				output.WriteLine();
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					output.WriteLine($"void {prefix}{fname}_{f.Key}(void* {state}) {{");
					if(f.Value.Extension.ToLowerInvariant()==".clasp")
					{
						clasp.Clasp.help = false;
						clasp.Clasp.output = output;
						clasp.Clasp.state = state;
						clasp.Clasp.block = block;
						clasp.Clasp.expr = expr;
						if (!string.IsNullOrEmpty(prolStr))
						{
							output.WriteLine(prolStr);
						}
						using (clasp.Clasp.input = File.OpenText(f.Value.FullName))
						{
							clasp.Clasp.Run();
						}
						if (!string.IsNullOrEmpty(epilStr))
						{
							output.WriteLine(epilStr);
						}
					} else
					{
						clstat.CLStat.status = "OK";
						clstat.CLStat.code = 200;
						clstat.CLStat.compress = clstat.CompressionType.auto;
						clstat.CLStat.type = null;
						clstat.CLStat.block = block;
						clstat.CLStat.state = state;
						clstat.CLStat.input = (FileInfo)f.Value;
						clstat.CLStat.output = output;
						clstat.CLStat.nostatus = false;
						if (!string.IsNullOrEmpty(prolStr))
						{
							output.WriteLine(prolStr);
						}
						clstat.CLStat.Run();
						if (!string.IsNullOrEmpty(epilStr))
						{
							output.WriteLine(epilStr);
						}
					}
					output.WriteLine("}");
					output.WriteLine();
					
				}
				output.WriteLine($"#endif // {impl}");
				output.WriteLine();

			}
#if !DEBUG
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error: " + ex.Message);
				return 1;
			}
#endif

			return 0;
		}
	}
}
