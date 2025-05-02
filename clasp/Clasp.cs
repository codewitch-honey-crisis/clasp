using Cli;


using System;
using System.IO.Compression;
using System.Text;

namespace clasp
{
	internal enum ClaspCompressionType
	{
		none = 0,
		gzip = 1,
		deflate = 2,
		auto = 3
	}
	internal enum ClaspHeaderMode
	{
		auto = 0,
		none = 1,
		required = 2
	}
	internal class Clasp
	{
		[CmdArg(Ordinal = 0, Optional = false)]
		public static TextReader input = null;
		[CmdArg(Ordinal = 1, Optional = true)]
		public static TextWriter output = Console.Out;
		[CmdArg(Name = "block", ElementName = "block", Optional = true, Description = "The function call to send a literal block to the client")]
		public static string block = "response_block";
		[CmdArg(Name = "expr", ElementName = "expr", Optional = true, Description = "The function call to send an expression to the client")]
		public static string expr = "response_expr";
		[CmdArg(Name = "state", ElementName = "state", Optional = true, Description = "The variable name that holds the user state to pass to the response functions")]
		public static string state = "response_state";
		[CmdArg(Name = "nostatus", Optional = true, Description = "Suppress the status headers")]
		public static bool nostatus = false;
		[CmdArg(Name = "headers", Optional =true,ElementName ="headers", Description ="Indicates which headers should be generated (auto, none or required). Defaults to auto")]
		public static ClaspHeaderMode headers = ClaspHeaderMode.auto;
		[CmdArg("compress", Optional = true, ElementName = "compress", Description = "Indicates the type of compression to use on static content: none, gzip, deflate, or auto.")]
		public static ClaspCompressionType compress = ClaspCompressionType.auto;

		[CmdArg(Group = "help", Name = "?", Description = "Displays this screen")]
		public static bool help = false;
		
		public static void EmitResponseBlock(string resp)
		{
			resp = clasp.ClaspUtility.GenerateChunked(resp);
			if (resp.Length > 0)
			{
				var ba = Encoding.UTF8.GetBytes(resp);
				output.Write(block + "(");
				output.Write(clasp.ClaspUtility.ToSZLiteral(ba,block.Length+1));
				output.Write(", ");
				output.Write(ba.Length);
				output.Write($", {state});\r\n");
				output.Flush();
			}
		}
		public static void EmitExpression(string resp)
		{
			output.Write(expr + "(");
			output.Write(resp);
			output.Write($", {state});\r\n");
			output.Flush();
		}
		public static void EmitCodeBlock(string resp)
		{
			output.Write(resp+"\r\n");
			output.Flush();
		}
		public static void Emit(string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				var ba = Encoding.UTF8.GetBytes(text);
				output.Write(block + "(");
				output.Write(clasp.ClaspUtility.ToSZLiteral(ba,block.Length+1));
				output.Write($", {ba.Length}, {state});\r\n");
				output.Flush();
			}
		}
		public static bool ScanForCodeBlocks(string s)
		{
			for (int i = 0; i < s.Length; i++)
			{
				char ch = s[i];
				if (s.Length > i + 3)
				{
					if (ch == '<')
					{
						++i;
						ch = s[i];
						if (ch == '%')
						{
							++i;
							ch = s[i];
							if (ch != '@')
							{
								return true;
							}
						}
					}
				}
			}
			return false;
		}
		public static int StaticLen(string s)
		{
			var i = s.LastIndexOf("%>");
			if (i == -1)
			{
				i = 0;
			}
			else
			{
				i += 2;
			}
			return Encoding.UTF8.GetByteCount(s.Substring(i));
		}
		public static Stream ProcessCompression(string inp)
		{
			var inpba = Encoding.UTF8.GetBytes(inp);
			var inputstm = new MemoryStream();
			inputstm.Write(inpba, 0, inpba.Length);
			inputstm.Position = 0;

			if (compress == ClaspCompressionType.auto)
			{
				var defl = new MemoryStream();
				var gzip = new MemoryStream();
				var uncomplen = inpba.Length;
				
				var deflsrc = new DeflateStream(defl, CompressionLevel.SmallestSize, true);
				inputstm.Position = 0;
				inputstm.CopyTo(deflsrc);
				deflsrc.Flush();
				defl.Position = 0;
				var gzipsrc = new GZipStream(gzip, CompressionLevel.SmallestSize, true);
				inputstm.Position = 0;
				inputstm.CopyTo(gzipsrc);
				gzipsrc.Flush();
				gzip.Position = 0;
				if (gzip.Length < defl.Length)
				{
					if (gzip.Length < uncomplen)
					{
						compress = ClaspCompressionType.gzip;
						gzip.Position = 0;
						return gzip;
					}
				}
				else
				{
					if (defl.Length < uncomplen)
					{
						compress = ClaspCompressionType.deflate;
						defl.Position = 0;
						return defl;
					}
				}
				compress = ClaspCompressionType.none;
				inputstm.Position = 0;
				return inputstm;
			}
			else
			{
				var comp = new MemoryStream();
				
				if (compress == ClaspCompressionType.none)
				{
					return inputstm;
				}
				else if (compress == ClaspCompressionType.deflate)
				{
					using (var src = new DeflateStream(comp, CompressionLevel.SmallestSize, true))
					{
						inputstm.CopyTo(src);
						src.Flush();
						inputstm.Close();
					}
				}
				else
				{
					using (var src = new DeflateStream(comp, CompressionLevel.SmallestSize, true))
					{
						inputstm.CopyTo(src);
						src.Flush();
						inputstm.Close();
					}
				}
				comp.Position = 0;
				return comp;

			}
		}
		public static void EmitDataFieldDecl(string prologue, Stream stm)
		{
			var len = checked((int)stm.Length);
			stm.Position = 0;
			output.Write($"static const unsigned char http_response_data[] = {{");
			int i = 0;
			var ba = Encoding.ASCII.GetBytes(prologue, 0, prologue.Length);
			for (; i < ba.Length; ++i)
			{
				if ((i % 20) == 0)
				{
					output.Write("\r\n");
					if (i < (ba.Length + len) - 1)
					{
						output.Write("    ");
					}
				}
				var entry = "0x" + ba[i].ToString("X2");
				if (i < (ba.Length + len) - 1)
				{
					entry += ", ";
				}
				output.Write(entry);
			}
			len += prologue.Length;
			int sb = stm.ReadByte();
			while (sb != -1)
			{
				if ((i % 20) == 0)
				{
					output.Write("\r\n");
					if (i < len - 1)
					{
						output.Write("    ");
					}
				}
				var entry = "0x" + sb.ToString("X2");
				if (i < len - 1)
				{
					entry += ", ";
				}
				output.Write(entry);

				++i;
				sb = stm.ReadByte();
			}
			if (0 != (i % 20))
			{
				output.Write(" ");
			}
			output.Write("};\r\n");
		}
		public static int Run()
		{
			if (help)
			{
				CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(Clasp)));
				return 0;
			}
			var hasStatus = false;
			var statusCode = 0;
			string statusText = null;
			var headerBuilder = new StringBuilder();
			var pastDirectives = false;
			var line = 1;
			var current = new StringBuilder();
			var dirArgs = new Dictionary<string, string>();
			string dirName = null;
			string dirTmp = null;
			bool inQuot = false;
			bool wasPastDirectives = false;
			string headerText = null;
			var inputString = input.ReadToEnd();
			var inputBuffer = new StringReader(inputString);
			var autoHeaders = true;
			var hasContentLength = false;
			var hasTransferEncodingChunked = false;
			var isStatic = !ScanForCodeBlocks(inputString);
			if(!isStatic)
			{
				if(headers==ClaspHeaderMode.required)
				{
					hasTransferEncodingChunked=true;
					headerBuilder.Append("Transfer-Encoding: chunked\r\n");
				} 
			} 
			
			var len = StaticLen(inputString);
			var i = inputBuffer.Read();
			var s = 0;
			while (i != -1)
			{
				char ch = (char)i;
				switch (s)
				{
					case 0: // in literal body
						if (pastDirectives && !wasPastDirectives)
						{
							wasPastDirectives = true;
							var str = "";
							if (hasStatus)
							{
								str = $"HTTP/1.1 {statusCode} {statusText}\r\n";
							}
							if (headerBuilder.Length > 0)
							{
								headerText = str + $"{headerBuilder.ToString().TrimEnd()}\r\n";
							}
							else
							{
								headerText = str;
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
								if (headerBuilder.Length > 0)
								{
									headerText = str + $"{headerBuilder.ToString().TrimEnd()}\r\n";
								}
								else
								{
									headerText = str;
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
								if (autoHeaders)
								{
									if (!isStatic)
									{
										if (!hasContentLength && !hasTransferEncodingChunked)
										{
											headerText += "Transfer-Encoding: chunked\r\n";
											hasTransferEncodingChunked = true;
										}
									}
								}
								if (headers != ClaspHeaderMode.none)
								{
									if (isStatic && !hasContentLength)
									{
										using (var stm = ProcessCompression(current.ToString())) {
											hasContentLength = true;
											headerText += $"Content-Length: {stm.Length}\r\n";
											switch (compress)
											{
												case ClaspCompressionType.deflate:
													headerText += "Content-Encoding: deflate\r\n";
													break;
												case ClaspCompressionType.gzip:
													headerText += "Content-Encoding: gzip\r\n";
													break;
											}
											foreach (var h in headerText.Split("\r\n")) {
												output.Write($"// {h}\r\n");
											}
											EmitDataFieldDecl(headerText + "\r\n", stm);
											output.Write($"{block}((const char*)http_response_data,sizeof(http_response_data), {state});\r\n");
											output.Flush();
										}

									} else {
										Emit(headerText + "\r\n" + clasp.ClaspUtility.GenerateChunked(current.ToString()));
									}
								}
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
								if (headerBuilder.Length > 0)
								{
									headerText = str + $"{headerBuilder.ToString().TrimEnd()}\r\n";
								}
								else
								{
									headerText = str;
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
							if (!string.IsNullOrEmpty(headerText))
							{
								if (autoHeaders)
								{
									if (!hasContentLength && !hasTransferEncodingChunked)
									{
										headerText += "Transfer-Encoding: chunked\r\n";
										hasTransferEncodingChunked = true;
									}
								}
								if (headers != ClaspHeaderMode.none)
								{
									Emit(headerText+"\r\n");
								}
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
								if (autoHeaders)
								{
									if (!hasContentLength && !hasTransferEncodingChunked)
									{
										headerText += "Transfer-Encoding: chunked\r\n";
										hasTransferEncodingChunked = true;
									}
								}
								if (headers != ClaspHeaderMode.none)
								{
									Emit(headerText + "\r\n");
								}
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

						if (ch == '%' || char.IsWhiteSpace((char)ch))
						{
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
						if (ch == '%')
						{
							s = 14;
							break;
						}
						s = 13;
						break;
					case 13:
						if (!char.IsWhiteSpace(ch))
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
						switch (dirName)
						{
							case "status":
								if (!dirArgs.ContainsKey("code"))
								{
									throw new Exception($"Status directive missing required \"code\" argument on line {line}");
								}
								if (!dirArgs.ContainsKey("text"))
								{
									throw new Exception($"Status directive missing required \"text\" argument on line {line}");
								}
								var c = dirArgs["code"];
								int cc;
								if (!int.TryParse(c, out cc) || cc < 0 || cc > 999)
								{
									throw new Exception($"Illegal code argument in status directive on line {line}");
								}
								var t = dirArgs["text"];
								if (string.IsNullOrEmpty(t))
								{
									throw new Exception($"Text argument must not be empty in status directive on line {line}");
								}
								string ah;
								bool b;
								if (dirArgs.TryGetValue("auto-headerBuilder", out ah) && bool.TryParse(ah, out b) && !b)
								{
									autoHeaders = false;
								}
								hasStatus = !nostatus;
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
								if (0 == string.Compare(n, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
								{
									if (0 != string.Compare(v, "chunked", StringComparison.OrdinalIgnoreCase))
									{
										throw new NotSupportedException($"Only chunked transfer encoding is supported {line}");
									}
									hasTransferEncodingChunked = true;
								}
								if (0 == string.Compare(n, "Content-Length", StringComparison.OrdinalIgnoreCase))
								{
									hasContentLength = true;
								}
								headerBuilder.Append($"{n}: {v}\r\n");
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

				i = inputBuffer.Read();
			}
			var emittedTerminator = false;
			switch (s)
			{
				case 0:
					if (current.Length > 0)
					{
						
						if (!string.IsNullOrEmpty(headerText))
						{
							Stream stm = isStatic?ProcessCompression(current.ToString()):null;
							
							if (autoHeaders)
							{
								if (!hasContentLength && !hasTransferEncodingChunked)
								{
									if (!isStatic)
									{
										headerText += "Transfer-Encoding: chunked\r\n";
										hasTransferEncodingChunked = true;
									}
									else
									{
										headerText += $"Content-Length: {stm.Length}\r\n";
										switch(compress)
										{
											case ClaspCompressionType.deflate:
												headerText += "Content-Encoding: deflate\r\n";
												break;
											case ClaspCompressionType.gzip:
												headerText += "Content-Encoding: gzip\r\n";
												break;
										}
										hasContentLength = true;
									}
								}
							}
							if (!isStatic)
							{
								if (headers != ClaspHeaderMode.none)
								{
									Emit(headerText + "\r\n" + clasp.ClaspUtility.GenerateChunked(current.ToString()));
								} else
								{
									EmitResponseBlock(current.ToString());
								}
							}
							else
							{
								if (headers != ClaspHeaderMode.none)
								{
									foreach (var h in headerText.Split("\r\n"))
									{
										output.Write($"// {h}\r\n");
									}
									EmitDataFieldDecl(headerText + "\r\n", stm);
								} else
								{
									EmitDataFieldDecl("", stm);
								}

								output.Write($"{block}((const char*)http_response_data,sizeof(http_response_data), {state});\r\n");
								output.Flush();
							}
							if(stm!=null)
							{
								stm.Close();
								stm = null;
							}
							headerText = null;
						}
						else
						{
							if (!isStatic)
							{
								Emit(clasp.ClaspUtility.GenerateChunked(current.ToString())+clasp.ClaspUtility.GenerateChunked(null));
								emittedTerminator = true;
							}
							else
							{
								Emit(current.ToString());
							}
						}
					}
					else
					{
						if (!string.IsNullOrEmpty(headerText))
						{
							if (autoHeaders)
							{
								if (!hasContentLength && !hasTransferEncodingChunked)
								{
									headerText += "Transfer-Encoding: chunked\r\n";
									hasTransferEncodingChunked = true;
								}
							}
							if (headers != ClaspHeaderMode.none)
							{
								Emit(headerText + "\r\n");
							}
							headerText = null;
						}
					}
					break;
				case 1:
					current.Append('<');
					if (isStatic)
					{
						Emit(current.ToString());
					}
					else
					{
						Emit(clasp.ClaspUtility.GenerateChunked(current.ToString()) + clasp.ClaspUtility.GenerateChunked(null));
						emittedTerminator = true;
					}
					break;
				case 3:
					if (!string.IsNullOrEmpty(headerText))
					{
						if (autoHeaders)
						{
							if (!hasContentLength && !hasTransferEncodingChunked)
							{
								headerText += "Transfer-Encoding: chunked\r\n";
								hasTransferEncodingChunked = true;
							}
						}
						if (headers != ClaspHeaderMode.none)
						{
							Emit(headerText + "\r\n");
						}
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
						if (autoHeaders)
						{
							if (!hasContentLength && !hasTransferEncodingChunked)
							{
								headerText += "Transfer-Encoding: chunked\r\n";
								hasTransferEncodingChunked = true;
							}
						}
						if (headers != ClaspHeaderMode.none)
						{
							Emit(headerText + "\r\n");
						}
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
						if (autoHeaders)
						{
							if (!hasContentLength && !hasTransferEncodingChunked)
							{
								headerText += "Transfer-Encoding: chunked\r\n";
								hasTransferEncodingChunked = true;
							}
						}
						if (headers != ClaspHeaderMode.none)
						{
							Emit(headerText + "\r\n");
					
						}
						headerText = null;
					}
					current.Append('%');
					EmitExpression(current.ToString());
					break;
				case 6:
					if (!string.IsNullOrEmpty(headerText))
					{
						if (autoHeaders)
						{
							if (!hasContentLength && !hasTransferEncodingChunked)
							{
								headerText += "Transfer-Encoding: chunked\r\n";
								hasTransferEncodingChunked = true;
							}
						}
						if (headers != ClaspHeaderMode.none)
						{
							Emit(headerText + "\r\n");
						}
						headerText = null;
					}
					current.Append('%');
					EmitCodeBlock(current.ToString());
					break;
				default:
					throw new Exception($"Invalid syntax in page on line {line}");
			}
			if (hasTransferEncodingChunked && !emittedTerminator)
			{
				EmitResponseBlock(null);
			}
			
			return 0;
		}
	}
}
