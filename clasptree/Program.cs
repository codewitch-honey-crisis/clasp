using Cli;

using System.Diagnostics.CodeAnalysis;
using System.Text;
namespace clasptree
{
	enum HandlersMode
	{
		none,
		@default,
		extended
	}
	internal class Program
	{
		[CmdArg(Ordinal = 0, ElementName = "input", Description = "The root directory of the site. Defaults to the current directory")]
		static DirectoryInfo input = new DirectoryInfo(Environment.CurrentDirectory);
		[CmdArg(Ordinal = 1, Optional = true, ElementName = "output", Description = "The output file to generate. Defaults to <stdout>")]
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
		[CmdArg(Name = "handlers", ElementName = "handlers", Optional = true, Description = "Indicated wither to generate no handler entries (none), default entries (@default) or extended (extended) handlers. None doesn't emit any. Default emits them in accordance with their paths, plus resoving indexes based on <index>. Extended does this and also adds path/ trailing handlers")]
		static HandlersMode handlers = HandlersMode.@default;
		[CmdArg(Name = "index", ElementName = "index", Optional = true, Description = "Generate / default handlers for files matching this wildcard. Defaults to \"index.*\"")]
		static string index = "index.*";

		[CmdArg(Group = "help", Name = "?", Description = "Displays this screen")]
		static bool help = false;
		static HashSet<string> names = new HashSet<string>();
		static string MakeSafeName(string relpath, bool local = false)
		{
			if (string.IsNullOrEmpty(relpath))
			{
				return relpath;
			}

			int start = 0;
			while (start < relpath.Length && relpath[start] == '/' || relpath[start] == '\\' || relpath[start] == '.')
			{
				++start;
			}
			if (start == relpath.Length)
			{
				return "";
			}
			var sb = new StringBuilder(relpath.Length - start);
			for (int i = start; i < relpath.Length; i++)
			{
				var ch = relpath[i];
				if (ch == '_' || (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
				{
					sb.Append(ch);
				}
				else
				{
					if (sb.Length > 0 && sb[sb.Length - 1] != '_')
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
					while (names.Contains(result + i.ToString()))
					{
						++i;
					}
					result += i.ToString();
				}
				names.Add(result);
			}
			return result;
		}
		private class FIAEqComp : IEqualityComparer<FileSystemInfo>
		{
			public bool Equals(FileSystemInfo x, FileSystemInfo y)
			{
				return ReferenceEquals(x, y) || x.FullName == y.FullName;
			}

			public int GetHashCode([DisallowNull] FileSystemInfo obj)
			{
				return obj.FullName.GetHashCode();
			}
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
				if (handlers==HandlersMode.none && parsed.NamedArguments.ContainsKey("index")) 
				{
					throw new ArgumentException($"{CliUtility.SwitchPrefix}<handlers> \"none\" cannot be specified with {CliUtility.SwitchPrefix}index");
				}
				if (prefix == null) prefix = "";
				var prolStr = prologue != null ? prologue.ReadToEnd() : "";
				var epilStr = epilogue != null ? epilogue.ReadToEnd() : "";
				var fia = input.GetFiles("*.*", SearchOption.AllDirectories);
				var deffia = input.GetFiles(index, SearchOption.AllDirectories);
				var files = new Dictionary<string, FileSystemInfo>();
				for (int i = 0; i < fia.Length; i++)
				{
					var fi = fia[i];
					var mname = fi.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); 
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
				var indout = new IndentedTextWriter(output);
				indout.Write($"// Generated with {CliUtility.AssemblyTitle}\r\n");
				indout.Write($"// To use this file, define {fname.ToUpperInvariant()}_IMPLEMENTATION in exactly one translation unit (.c/.cpp file) before including this header.\r\n");
				indout.Write($"#ifndef {def}\r\n");
				indout.Write($"#define {def}\r\n");
				indout.Write("\r\n");
				int handlersCount = (handlers != HandlersMode.none) ? files.Count + deffia.Length : 0;
				if (handlers == HandlersMode.extended)
				{
					foreach (var f in files)
					{
						var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;

						if (deffia.Contains(f.Value, new FIAEqComp()))
						{
							int li = mname.LastIndexOf('/');
							if (li > -1 && li != mname.Length - 1)
							{
								handlersCount++;
							}
						}
					}
				}
				if (handlers!=HandlersMode.none)
				{
					indout.Write($"#define {prefix.ToUpperInvariant()}RESPONSE_HANDLER_COUNT {handlersCount}\r\n");
					indout.Write($"typedef struct {{ const char* path; const char* path_encoded; void (* handler) (void* arg); }} {prefix}response_handler_t;\r\n");
					indout.Write($"extern {prefix}response_handler_t {prefix}response_handlers[{handlersCount}];\r\n");
				}


				indout.Write("#ifdef __cplusplus\r\n");
				indout.Write("extern \"C\" {\r\n");
				indout.Write("#endif\r\n");
				indout.Write("\r\n");
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					indout.Write($"// ./{mname}\r\n");
					indout.Write($"void {prefix}{fname}_{f.Key}(void* {state});\r\n");

				}
				indout.Write("\r\n");
				indout.Write("#ifdef __cplusplus\r\n");
				indout.Write("}\r\n");
				indout.Write("#endif\r\n\r\n");
				indout.Write($"#endif // {def}\r\n\r\n");
				var impl = fname.ToUpperInvariant() + "_IMPLEMENTATION";
				indout.Write($"#ifdef {impl}\r\n\r\n");
				
				if (handlers!=HandlersMode.none)
				{
					indout.Write($"{prefix}response_handler_t {prefix}response_handlers[{handlersCount}] = {{\r\n");
					int i = 0;
					foreach (var f in files)
					{
						var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
						
						if(deffia.Contains(f.Value,new FIAEqComp())) {
							// generate a default for this one
							var dname = "";
							int li = mname.LastIndexOf('/');
							if (li> -1) {
								dname = mname.Substring(0, li + 1);
							}
							if(dname.Length>1)
							{
								if(handlers==HandlersMode.extended)
								{
									indout.Write("    { ");
									indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/" + dname)}");
									indout.Write(", ");
									indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/" + System.Web.HttpUtility.UrlPathEncode(dname))}, {prefix}{fname}_{f.Key} }},\r\n");
								}
								dname=dname.Substring(0,dname.Length-1);
							}
							indout.Write("    { ");
							indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/" + dname)}");
							indout.Write(", ");
							indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/" + System.Web.HttpUtility.UrlPathEncode(dname))}, {prefix}{fname}_{f.Key} }},\r\n");
						}
						indout.Write("    { ");
						indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/"+mname)}");
						indout.Write(", ");
						indout.Write($"{clasp.ClaspUtility.ToSZLiteral("/" + System.Web.HttpUtility.UrlPathEncode(mname))}, {prefix}{fname}_{f.Key}");
						if (i < files.Count - 1)
						{
							indout.Write(" },\r\n");
						}
						else
						{
							indout.Write(" }\r\n");

						}
						++i;
					}
					indout.Write("};\r\n");
				}
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					indout.Write($"void {prefix}{fname}_{f.Key}(void* {state}) {{\r\n");
					if (f.Value.Extension.ToLowerInvariant() == ".clasp")
					{
						indout.IndentLevel++;
						clasp.Clasp.help = false;
						clasp.Clasp.output = indout;
						clasp.Clasp.state = state;
						clasp.Clasp.block = block;
						clasp.Clasp.expr = expr;
						if (!string.IsNullOrEmpty(prolStr))
						{
							indout.Write($"{prolStr}\r\n");
						}
						using (clasp.Clasp.input = File.OpenText(f.Value.FullName))
						{
							clasp.Clasp.Run();
						}
						if (!string.IsNullOrEmpty(epilStr))
						{
							indout.Write($"{epilStr}\r\n");
						}
						indout.IndentLevel--;
					}
					else
					{
						indout.IndentLevel++;
						clstat.CLStat.status = "OK";
						clstat.CLStat.code = 200;
						clstat.CLStat.compress = clstat.CompressionType.auto;
						clstat.CLStat.type = null;
						clstat.CLStat.block = block;
						clstat.CLStat.state = state;
						clstat.CLStat.input = (FileInfo)f.Value;
						clstat.CLStat.output = indout;
						clstat.CLStat.nostatus = false;
						if (!string.IsNullOrEmpty(prolStr))
						{
							indout.Write($"{prolStr}\r\n");
						}
						clstat.CLStat.Run();
						if (!string.IsNullOrEmpty(epilStr))
						{
							indout.Write($"{epilStr}\r\n");
						}
						indout.IndentLevel--;
					}
					indout.Write("}\r\n");

				}
				indout.Write($"#endif // {impl}\r\n");
				indout.Flush();

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
