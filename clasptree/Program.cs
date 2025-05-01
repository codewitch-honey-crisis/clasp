using Cli;
using VisualFA;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using System.Drawing;
using System.Reflection;
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
		[CmdArg(Name = "nostatus", ElementName ="nostatus", Optional = true, Description = "Suppress the status headers")]
		public static bool nostatus = false;
		[CmdArg(Name = "handlerfsm", ElementName = "handlerfsm", Optional = true, Description = "Generate a finite state machine that can be used for matching headers")]
		public static bool handlerfsm = false;
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
		
		struct HandlerEntry
		{
			public string Path;
			public string EncodedPath;
			public string Method;
			public HandlerEntry(string path, string encodedPath, string method)
			{
				Path = path;
				EncodedPath = encodedPath;
				Method = method;
			}
		}
		static int FsmWidthBytes(int[] table)
		{
			int width = 1;
			for (int i = 0; i < table.Length; ++i)
			{
				var entry = table[i];
				if (entry > 32767 || entry < -32768)
				{
					return 4;
				}
				else if (entry > 127 || entry < -128)
				{
					width = 2;
				}
			}
			return width;
		}
		static string FsmWidthToType(int width)
		{
			return $"uint{width * 8}_t";
		}
		static string FsmWidthToSignedType(int width)
		{
			return $"int{width * 8}_t";
		}
		static string FsmReplaceTypes(string s)
		{
			s = s.Replace("UINT8", "uint8_t");
			s = s.Replace("INT8", "int8_t");
			s = s.Replace("UINT16", "uint16_t");
			s = s.Replace("INT16", "int16_t");
			s = s.Replace("UINT32", "uint32_t");
			s = s.Replace("INT32", "int32_t");
			return s;
		}
		static void EmitFsm(List<HandlerEntry> handlers, TextWriter output)
		{
			FA[] hfas = new FA[handlers.Count];
			for(var i = 0;i<handlers.Count;++i)
			{
				var h = handlers[i];
				hfas[i] = FA.Literal(h.EncodedPath, i);
			}
			var lexer = FA.ToLexer(hfas, true);
			int[] fsmData = lexer.ToArray();
			var width = FsmWidthBytes(fsmData);
			output.Write($"static const {FsmWidthToSignedType(width)} fsm_data[] = {{");
			
			for (var i = 0; i < fsmData.Length; ++i)
			{
				if ((i % 20) == 0)
				{
					output.Write("\r\n");
					if (i < fsmData.Length - 1)
					{
						output.Write("    ");
					}
				}
				string entry;
				if (fsmData[i] == -1)
				{
					entry = "-1";
				}
				else
				{
					entry = "0x" + fsmData[i].ToString("X" + (width * 2).ToString());
				}
				if (i < fsmData.Length- 1)
				{
					entry += ", ";
				}
				output.Write(entry);
			}
			output.Write("};\r\n\r\n");
			var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream("clasptree.runner.c");
			TextReader tr = new StreamReader(stm);
			var s = tr.ReadToEnd();
			s = s.Replace("TYPE", width == 4 ? "INT32" : width == 1 ? "INT8" : "INT16");
			s = FsmReplaceTypes(s);
			output.Write(s);


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
				if (handlers == HandlersMode.none && handlerfsm)
				{
					throw new ArgumentException($"{CliUtility.SwitchPrefix}<handlers> \"none\" cannot be specified with {CliUtility.SwitchPrefix}handlersfsm");
				}
					if (prefix == null) prefix = "";
				var prolStr = prologue != null ? prologue.ReadToEnd() : "";
				var epilStr = epilogue != null ? epilogue.ReadToEnd() : "";
				var fia = input.GetFiles("*.*", SearchOption.AllDirectories);
				var deffia = new List<FileInfo>(input.GetFiles(index, SearchOption.AllDirectories));
				for(int i = 0; i < deffia.Count; i++)
				{
					var file = deffia[i];
					if(file.Extension.ToLowerInvariant()==".h")
					{
						deffia.RemoveAt(i--);
					}
				}
				var files = new Dictionary<string, FileSystemInfo>();
				var includes = new StringBuilder();
				for (int i = 0; i < fia.Length; i++)
				{
					var fi = fia[i];
					if (fi.Extension.ToLowerInvariant() == ".h")
					{
						var relpath = fi.Directory.FullName.Substring(Path.GetFullPath(input.FullName).Length);
						if(relpath.StartsWith(Path.DirectorySeparatorChar))
						{
							relpath = relpath.Substring(1);
						}
						var outdir = Path.GetDirectoryName(CliUtility.GetFilename(output));
						var outfulldir = Path.GetFullPath(outdir);
						var fn = Path.Combine(outfulldir, Path.Combine(relpath, fi.Name));
						includes.Append($"#include \"{Path.Combine(relpath,Path.GetFileName(fn))}\"\r\n");
						var dir = Path.GetDirectoryName(fn);
						if (!Directory.Exists(dir))
						{
							Directory.CreateDirectory(dir);
						}
						
						
						try
						{
							File.Delete(fn);
						}
						catch { }
						fi.CopyTo(fn);
					}
					else
					{
						var mname = fi.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
						var sn = MakeSafeName(mname);
						if (!string.IsNullOrEmpty(sn))
						{
							files[sn] = fi;
						}
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
				indout.Write(includes.ToString()+"\r\n");
				var handlersList = new List<HandlerEntry>();
				if (handlers != HandlersMode.none)
				{
					foreach (var f in files)
					{
						var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;

						if (deffia.Contains(f.Value, new FIAEqComp()))
						{
							// generate a default for this one
							var dname = "";
							int li = mname.LastIndexOf('/');
							if (li > -1)
							{
								dname = mname.Substring(0, li + 1);
							}
							string hext = null;
							string hstd = $"/{dname}";
							if(dname.Length>1)
							{
								hext = "/" + dname.Substring(0, dname.Length - 1);
							}
							handlersList.Add(new HandlerEntry(hstd, System.Web.HttpUtility.UrlPathEncode(hstd), $"{prefix}content_{f.Key}"));
							if(hext!=null && handlers==HandlersMode.extended)
							{
								handlersList.Add(new HandlerEntry(hext, System.Web.HttpUtility.UrlPathEncode(hext), $"{prefix}content_{f.Key}"));
							}
						}
						handlersList.Add(new HandlerEntry("/" + mname, "/" + System.Web.HttpUtility.UrlPathEncode(mname), $"{prefix}content_{f.Key}"));
					}
					handlersList.Sort((x, y) => x.Path.CompareTo(y.Path));
				}

				if (handlers!=HandlersMode.none)
				{
					indout.Write($"#define {prefix.ToUpperInvariant()}RESPONSE_HANDLER_COUNT {handlersList.Count}\r\n");
					indout.Write($"typedef struct {{ const char* path; const char* path_encoded; void (* handler) (void* arg); }} {prefix}response_handler_t;\r\n");
					indout.Write($"extern {prefix}response_handler_t {prefix}response_handlers[{handlersList.Count}];\r\n");
				}


				indout.Write("#ifdef __cplusplus\r\n");
				indout.Write("extern \"C\" {\r\n");
				indout.Write("#endif\r\n");
				indout.Write("\r\n");
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					indout.Write($"// ./{mname}\r\n");
					indout.Write($"void {prefix}content_{f.Key}(void* {state});\r\n");
				}
				if(handlerfsm)
				{
					indout.Write("// matches an url to a response handler index\r\n");
					indout.Write($"int {prefix}response_handler_match(const char* uri);\r\n");
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
					indout.Write($"{prefix}response_handler_t {prefix}response_handlers[{handlersList.Count}] = {{\r\n");
					for (var i = 0; i < handlersList.Count; i++)
					{
						var handler = handlersList[i];
						indout.Write("    { ");
						indout.Write($"{clasp.ClaspUtility.ToSZLiteral(handler.Path)}");
						indout.Write(", ");
						indout.Write($"{clasp.ClaspUtility.ToSZLiteral(handler.EncodedPath)}, {handler.Method}");
						if (i < handlersList.Count - 1)
						{
							indout.Write(" },\r\n");
						}
						else
						{
							indout.Write(" }\r\n");

						}
					}
					indout.Write("};\r\n");
				}
				if (handlerfsm)
				{
					indout.Write("/// @brief Matches an URL to one of the response handler entries\r\n/// @param uri The URL to match\r\n/// @return The index of the response handler entry, or -1 if no match\r\n");
					indout.Write($"int {prefix}response_handler_match(const char* uri) {{\r\n");
					indout.IndentLevel++;
					EmitFsm(handlersList, indout);
					indout.IndentLevel--;
					indout.Write("}\r\n");
				}
				foreach (var f in files)
				{
					var mname = f.Value.FullName.Substring(input.FullName.Length + 1).Replace(Path.DirectorySeparatorChar, '/'); ;
					indout.Write($"void {prefix}content_{f.Key}(void* {state}) {{\r\n");
					if (f.Value.Extension.ToLowerInvariant() == ".clasp")
					{
						indout.IndentLevel++;
						clasp.Clasp.help = false;
						clasp.Clasp.output = indout;
						clasp.Clasp.state = state;
						clasp.Clasp.block = block;
						clasp.Clasp.expr = expr;
						clasp.Clasp.nostatus = nostatus;
						clasp.Clasp.headers = clasp.ClaspHeaderMode.required;
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
						clstat.CLStat.nostatus = nostatus;
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
				var ofn = CliUtility.GetFilename(output);
				if (!string.IsNullOrEmpty(ofn)) {
					Console.Error.WriteLine($"Successfully wrote to {ofn}.");
				}
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
