using Cli;

using System.Text;
namespace clasp
{
	internal class Program
	{
		[CmdArg(Ordinal = 0, Optional = false)]
		static TextReader input;
		[CmdArg(Ordinal = 1, Optional = true)]
		static TextWriter output = Console.Out;
		[CmdArg(Name = "block", ElementName = "block", Optional = true, Description = "The function call to send a literal block to the client.")]
		static string block = "response_block";
		[CmdArg(Name = "expr", ElementName = "expr", Optional = true, Description = "The function call to send an expression to the client.")]
		static string expr = "response_expr";
		[CmdArg(Name = "state", ElementName = "state", Optional = true, Description = "The variable name that holds the user state to pass to the response functions.")]
		static string state = "response_state";
		[CmdArg(Name = "method", ElementName = "method", Optional = true, Description = "The method to wrap the code in, if specified.")]
		static string method = null;
		[CmdArg(Group = "help", Name = "?", Description = "Displays this screen")]
		static bool help = false;
		static string ToSZLiteral(string value)
		{
			int j;
			var sb = new StringBuilder((int)(value.Length * 1.5));
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
		static string GenerateChunked(string resp)
		{
			int len = resp.Length;
			var str = len.ToString("X") + "\r\n";
			int strlen = str.Length;
			return str + resp + "\r\n";

		}
		static void EmitResponseBlock(string resp)
		{
			var tab = !string.IsNullOrEmpty(method) ? "    " : "";
			if (resp == null)
			{
				output.Write(tab);
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
				output.Write(tab);
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
			var tab = !string.IsNullOrEmpty(method) ? "    " : "";
			output.Write(tab);
			output.Write(expr + "(");
			output.Write(resp);
			output.WriteLine($", {state});");
			output.Flush();
		}
		static void EmitCodeBlock(string resp)
		{
			var tab = !string.IsNullOrEmpty(method) ? "    " : "";
			output.Write(tab);
			output.WriteLine(resp);
			output.Flush();
		}
		static void Emit(string text)
		{
			var tab = !string.IsNullOrEmpty(method) ? "    " : "";
			if (!string.IsNullOrEmpty(text))
			{
				output.Write(tab);
				output.Write(block + "(");
				output.Write(ToSZLiteral(text));
				output.WriteLine($", {text.Length}, {state});");
				output.Flush();
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
				var hasStatus = false;
				var statusCode = 0;
				string statusText = null;
				var headers = new StringBuilder();
				var pastDirectives = false;
				var line = 1;
				var current = new StringBuilder();
				var dirArgs = new Dictionary<string, string>();
				string dirName = null;
				string dirTmp = null;
				bool inQuot = false;
				bool wasPastDirectives = false;
				string headerText = null;
				var i = input.Read();
				var s = 0;
				if (!string.IsNullOrEmpty(method))
				{
					output.WriteLine($"void {method}(void* {state}) {{");
				}
				while (i != -1)
				{
					char ch = (char)i;

					switch (s)
					{
						case 0: // in literal body
							if(pastDirectives && !wasPastDirectives)
							{
								wasPastDirectives = true;
								var str = "";
								if (hasStatus)
								{
									str = $"HTTP/1.1 {statusCode} {statusText}\r\n";
								}
								if (headers.Length > 0)
								{
									headerText = str + $"{headers.ToString().TrimEnd()}\r\n\r\n";
								}
								else
								{
									headerText = str + "\r\n";
								}
							}
							if (ch == '<')
							{
								s = 1;
								break;
							}
							if (!pastDirectives && !char.IsWhiteSpace(ch))
							{
								pastDirectives = true;
							}
							current.Append(ch);
							break;
						case 1:
							if (ch != '%')
							{
								if (!wasPastDirectives)
								{
									wasPastDirectives = true;
									var str = "";
									if (hasStatus)
									{
										str = $"HTTP/1.1 {statusCode} {statusText}\r\n";
									}
									if (headers.Length > 0)
									{
										headerText=str + $"{headers.ToString().TrimEnd()}\r\n\r\n";
									}
									else if(hasStatus)
									{
										headerText=str + "\r\n";
									}
								}
								pastDirectives = true;
								current.Append('<');
								current.Append(ch);
								s = 0;
								break;
							}
							if (pastDirectives && current.Length > 0)
							{
								if (!string.IsNullOrEmpty(headerText))
								{
									Emit(headerText + GenerateChunked(current.ToString()));
									headerText = null;
								}
								else
								{
									EmitResponseBlock(current.ToString());
								}
								current.Clear();
							}
							s = 2;
							break;
						case 2:
							if (ch == '=')
							{
								if (!wasPastDirectives)
								{
									wasPastDirectives = true;
									var str = "";
									if (hasStatus)
									{
										str = $"HTTP/1.1 {statusCode} {statusText}\r\n";
									}
									if (headers.Length > 0)
									{
										headerText = str + $"{headers.ToString().TrimEnd()}\r\n\r\n";
									}
									else
									{
										headerText = str + "\r\n";
									}
								}
								pastDirectives = true;
								s = 3;
								break;
							}
							else if (ch == '@')
							{
								if (pastDirectives)
								{
									throw new Exception($"Illegal directive on line {line}. Directives must precede any content");
								}
								s = 7;
								break;
							}
							pastDirectives = false;
							current.Append(ch);
							s = 4;
							break;
						case 3: // expression
							if (ch == '%')
							{
								s = 5;
								break;
							}
							current.Append(ch);
							break;
						case 4: // code block
							if (ch == '%')
							{
								s = 6;
								break;
							}
							current.Append(ch);
							break;
						case 5:
							if (ch == '>')
							{
								if(!string.IsNullOrEmpty(headerText))
								{
									Emit(headerText);
									headerText = null;
								}
								EmitExpression(current.ToString());
								current.Clear();
								s = 0;
								break;
							}
							current.Append('%');
							current.Append(ch);
							s = 3;
							break;
						case 6:
							if (ch == '>')
							{
								if (!string.IsNullOrEmpty(headerText))
								{
									Emit(headerText);
									headerText = null;
								}
								EmitCodeBlock(current.ToString());
								current.Clear();
								s = 0;
								break;
							}
							current.Append('%');
							current.Append(ch);
							s = 4;
							break;
						case 7: // directive
							if (!char.IsWhiteSpace(ch))
							{
								if (ch == '%')
								{
									throw new Exception($"Illegal % found in directive on line {line}");
								}
								s = 8;
								dirArgs.Clear();
								current.Clear();
								current.Append(ch);
							}
							break;
						case 8:
							if (ch != '%' && !char.IsWhiteSpace(ch))
							{
								current.Append(ch);
								break;
							}
							dirName = current.ToString();
							current.Clear();
							if (ch == '%') { s = 14; break; }
							s = 9;
							break;
						case 9: // name part of directive name value pair
							if (ch != '=' && !char.IsWhiteSpace(ch))
							{
								current.Append(ch);
								break;
							}
							dirTmp = current.ToString();
							current.Clear();
							s = 10;
							break;
						case 10:
							if (!char.IsWhiteSpace(ch))
							{
								inQuot = false;
								if (ch == '\"')
								{
									inQuot = true;
								}
								else
								{
									current.Append(ch);
								}
								s = 11;
							}
							break;
						case 11:
							if (inQuot)
							{
								if (ch == '\"')
								{
									inQuot = false;
									s = 12;
									break;
								}
								current.Append(ch); 
								break;
							}
							
							if(ch=='%' || char.IsWhiteSpace((char)ch)) {
								s = 12;
								break;
							}
							current.Append(ch);
							break;
						case 12:
							dirArgs.Add(dirTmp, current.ToString());
							dirTmp = null;
							inQuot = false;
							current.Clear();
							if(ch=='%')
							{
								s = 14;
								break;
							}
							s = 13;
							break;
						case 13:
							if(!char.IsWhiteSpace(ch))
							{
								s = 9;
								current.Append(ch);
							}
							break;
						case 14: // end directive
							if (ch != '>')
							{
								throw new Exception($"Illegal % in directive on line {line}.");
							}
							switch(dirName)
							{
								case "status":
									if(!dirArgs.ContainsKey("code"))
									{
										throw new Exception($"Status directive missing required \"code\" argument on line {line}");
									}
									if (!dirArgs.ContainsKey("text"))
									{
										throw new Exception($"Status directive missing required \"text\" argument on line {line}");
									}
									var c = dirArgs["code"];
									int cc;
									if(!int.TryParse(c, out cc) || cc<0|| cc>999) 
									{
										throw new Exception($"Illegal code argument in status directive on line {line}");
									}
									var t = dirArgs["text"];
									if (string.IsNullOrEmpty(t))
									{
										throw new Exception($"Text argument must not be empty in status directive on line {line}");
									}
									hasStatus = true;
									statusCode = cc;
									statusText = t;
									break;
								case "header":
									if (!dirArgs.ContainsKey("name"))
									{
										throw new Exception($"Header directive missing required \"name\" argument on line {line}");
									}
									if (!dirArgs.ContainsKey("value"))
									{
										throw new Exception($"Header directive missing required \"value\" argument on line {line}");
									}
									var n = dirArgs["name"];
									if (string.IsNullOrEmpty(n))
									{
										throw new Exception($"Name argument must not be empty in header directive on line {line}");
									}
									var v = dirArgs["value"];
									if (string.IsNullOrEmpty(v))
									{
										throw new Exception($"Value argument must not be empty in status directive on line {line}");
									}
									headers.Append($"{n}: {v}\r\n");
									break;
							}
							
							dirArgs = new Dictionary<string, string>();
							s = 0;
							break;
					}
					if (ch == '\n')
					{
						++line;
					}
					i = input.Read();
				}
				switch (s)
				{
					case 0:
						if (current.Length > 0)
						{
							if (!string.IsNullOrEmpty(headerText))
							{
								Emit(headerText + GenerateChunked(current.ToString()));
								headerText = null;
							}
							else
							{
								EmitResponseBlock(current.ToString());
							}
						} else
						{
							if (!string.IsNullOrEmpty(headerText))
							{
								Emit(headerText);
								headerText = null;
							}
						}
						break;
					case 1:
						current.Append('<');
						EmitResponseBlock(current.ToString());
						break;
					case 3:
						if (!string.IsNullOrEmpty(headerText))
						{
							Emit(headerText);
							headerText = null;
						}
						if (current.Length > 0)
						{
							EmitExpression(current.ToString());
						}
						break;
					case 4:
						if (!string.IsNullOrEmpty(headerText))
						{
							Emit(headerText);
							headerText = null;
						}
						if (current.Length > 0)
						{
							EmitCodeBlock(current.ToString());
						}
						break;
					case 5:
						if (!string.IsNullOrEmpty(headerText))
						{
							Emit(headerText);
							headerText = null;
						}
						current.Append('%');
						EmitExpression(current.ToString());
						break;
					case 6:
						if (!string.IsNullOrEmpty(headerText))
						{
							Emit(headerText);
							headerText = null;
						}
						current.Append('%');
						EmitCodeBlock(current.ToString());
						break;
					default:
						throw new Exception($"Invalid syntax in page on line {line}");
				}
				EmitResponseBlock(null);
				if (!string.IsNullOrEmpty(method))
				{
					output.WriteLine("}");
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
