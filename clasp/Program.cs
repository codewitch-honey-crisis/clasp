using Cli;

using System.Security.Claims;
using System.Text;
namespace clasp
{
    internal class Program
    {
        [CmdArg(Ordinal =0,Optional = false)]
        static TextReader input;
        [CmdArg(Ordinal = 1,Optional = true)]
		static TextWriter output = Console.Out;
		[CmdArg(Name = "block", Optional = true, Description = "The function call to send a literal block to the client.")]
		static string block = "response_block";
		[CmdArg(Name = "expr", Optional = true, Description = "The function call to send an expression to the client.")]
		static string expr = "response_expr";
		[CmdArg(Name = "state", Optional = true, Description = "The variable name that holds the user state to pass to the response functions.")]
		static string state = "response_state";

		[CmdArg(Group ="help",Name ="?",Description ="Displays this screen")]
        static bool help = false;
		static string ToSZLiteral(string value)
		{
			int j;
			var sb = new StringBuilder((int)(value.Length*1.5));
			sb.Append('"');
			for (int i = 0; i < value.Length; ++i)
			{
				char ch = value[i];
				switch (ch)
				{
					case '"':
						sb.Append("\\\""); break;
					case '\r':
						sb.Append("\\r"); break;
					case '\n':
						sb.Append("\\n"); break;
					case '\t':
						sb.Append("\\t"); break;
					default:
						j = (int)ch;
						if (char.IsSurrogate(ch) || j > 255)
						{
							throw new Exception("Unicode not supported");
						}
						if (j < 31 || j > 126)
						{
							sb.Append("\\x");
							sb.Append(j.ToString("X2"));
						}
						else
						{
							sb.Append(ch);
						}

						break;
				}
			}
			sb.Append('"');
			return sb.ToString();
		}
		static void EmitResponseBlock(string resp)
		{
			if (resp == null)
			{
				output.Write(block + "(");
				output.Write(ToSZLiteral("0\r\n\r\n"));
				output.WriteLine($", 5, {state});");
				output.Flush();
				return;
			}
			if (resp.Length > 0)
			{
				int len = resp.Length;
				var str = len.ToString("X") + "\r\n";
				int strlen = str.Length;
				output.Write(block + "(");
				output.Write(ToSZLiteral(str + resp + "\r\n"));
				output.Write(", ");
				output.Write(len + strlen + 2);
				output.WriteLine($", {state});");
				output.Flush();
			}
		}
		static void EmitExpression(string resp)
		{
			output.Write(expr+"(");
			output.Write(resp);
			output.WriteLine($", {state});");
			output.Flush();
		}
		static void EmitCodeBlock(string resp)
		{
			output.WriteLine(resp);
			output.Flush();
		}

		static int Main(string[] args)
        {
#if !DEBUG
			try
			{
#endif
			using (var parsed = CliUtility.ParseAndSet(args, null, typeof(Program)))
			{
				int line = 1;
				var code = new StringBuilder();
				var literal = new StringBuilder();
				int i = input.Read();
				int s = 0;
				while(i!=-1)
				{
					char ch = (char)i;
					
					switch(s)
					{
						case 0: // in literal body
							if(ch=='<')
							{
								s = 1;
								break;
							}
							literal.Append(ch);
							break;
						case 1:
							if(ch!='%')
							{
								literal.Append('<');
								literal.Append(ch);
								s = 0;
								break;
							}
							EmitResponseBlock(literal.ToString());
							literal.Clear();
							s = 2;
							break;
						case 2:
							if(ch=='=')
							{
								s = 3;
							} else
							{
								code.Append(ch);
								s = 4;
							}
							break;
						case 3: // expression
							if (ch == '%')
							{
								s = 5;
								break;
							}
							code.Append(ch);
							break;
						case 4: // code block
							if(ch=='%')
							{
								s = 6;
								break;
							}
							code.Append(ch);
							break;
						case 5:
							if (ch == '>')
							{
								EmitExpression(code.ToString());
								code.Clear();
								s = 0;
								break;
							}
							code.Append('%');
							code.Append(ch);
							s = 3;	
							break;
						case 6:
							if (ch == '>')
							{
								EmitCodeBlock(code.ToString());
								code.Clear();
								s = 0;
								break;
							}
							code.Append('%');
							code.Append(ch);
							s = 4;
							break;
					}
					if (ch == '\n')
					{
						++line;
					}
					i = input.Read();
				}
				switch(s)
				{
					case 0:
						if(literal.Length>0)
						{
							EmitResponseBlock(literal.ToString());
						}
						break;
					case 1:
						literal.Append('<');
						EmitResponseBlock(literal.ToString());
						break;
					case 3:
						if (code.Length > 0)
						{
							EmitExpression(code.ToString());
						}
						break;
					case 4:
						if (code.Length > 0)
						{
							EmitCodeBlock(code.ToString());
						}
						break;
					case 5:
						code.Append('%');
						EmitExpression(code.ToString());
						break;
					case 6:
						code.Append('%');
						EmitCodeBlock(code.ToString());
						break;
					default:
						throw new Exception($"Invalid syntax in page on line {line}");
				}
				EmitResponseBlock(null);
			}
#if !DEBUG
			}
			catch(Exception ex)
			{
				Console.Error.WriteLine("Error: "+ex.Message);
				return 1;
			}
#endif
				if (help)
				{
					CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(Program)));
					return 0;
				}
				return 0;
		}
    }
}
