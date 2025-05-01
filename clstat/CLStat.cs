using Cli;

using System.IO.Compression;
using System.Text;
namespace clstat
{
	internal enum CLStatCompressionType
	{
		none = 0,
		gzip = 1,
		deflate = 2,
		auto = 3
	}
	internal class CLStat
	{
		[CmdArg(Ordinal = 0, Optional = false, ElementName = "input", Description = "The input file to process.")]
		public static FileInfo input = null;
		[CmdArg(Ordinal = 1, Optional = true, ElementName = "output", Description = "The output to produce")]
		public static TextWriter output = Console.Out;
		[CmdArg("code", Optional = true, ElementName = "code", Description = "Indicates the HTTP status code.")]
		public static int code = 200;
		[CmdArg("status", Optional = true, ElementName = "status", Description = "Indicates the HTTP status text.")]
		public static string status = "OK";
		[CmdArg("nostatus", Optional = true, ElementName = "nostatus", Description = "Indicates that the HTTP status line should be surpressed")]
		public static bool nostatus = false;
		[CmdArg("type", Optional = true, ElementName = "type", Description = "Indicates the content type of the data.")]
		public static string type = null;
		[CmdArg("compress", Optional = true, ElementName = "compress", Description = "Indicates the type of compression to use: none, gzip, deflate, or auto.")]
		public static CLStatCompressionType compress = CLStatCompressionType.auto;
		[CmdArg(Name = "block", ElementName = "block", Optional = true, Description = "The function call to send a literal block to the client.")]
		public static string block = "response_block";
		[CmdArg(Name = "state", ElementName = "state", Optional = true, Description = "The variable name that holds the user state to pass to the response functions.")]
		public static string state = "response_state";

		[CmdArg(Group = "help", Name = "?", Description = "Displays this screen")]
		public static bool help = false;

		public static int Run()
		{
			if (help)
			{
				CliUtility.PrintUsage(CliUtility.GetSwitches(null, typeof(CLStat)));
				return 0;
			}

			FillMimeType();

			using (var stm = ProcessCompression())
			{
				var enc = "";
				switch (compress)
				{
					case CLStatCompressionType.deflate:
						enc = "Content-Encoding: deflate\r\n";
						break;
					case CLStatCompressionType.gzip:
						enc = "Content-Encoding: gzip\r\n";
						break;
				}
				var prologue = new StringBuilder();
				var txt = IsText();
				string txtStr = null;
				if (txt)
				{
					txtStr = new StreamReader(stm, Encoding.ASCII).ReadToEnd();

				}
				var len = txt ? txtStr.Length : checked((int)stm.Length);
				if (!nostatus)
				{
					prologue.Append($"HTTP/1.1 {code} {status}\r\n");
				}
				prologue.Append($"Content-Type: {type}\r\n");
				prologue.Append(enc);
				prologue.Append($"Content-Length: {len}\r\n");
				prologue.Append("\r\n");
				if (!txt)
				{
					var rdr = new StringReader(prologue.ToString());
					string line;
					while (null != (line = rdr.ReadLine()))
					{
						output.Write($"// {line}\r\n");
					}
					EmitDataFieldDecl(prologue.ToString(), stm);
					output.Write($"{block}((const char*)http_response_data,sizeof(http_response_data), {state});\r\n");
					output.Flush();
				}
				else
				{
					EmitText(prologue + txtStr);
				}
			}
			return 0;
		}
		public static bool IsText()
		{
			if (compress != CLStatCompressionType.none)
			{
				return false;
			}
			if (string.IsNullOrEmpty(type))
			{
				return false;
			}
			if (type.StartsWith("text/"))
			{
				return true;
			}
			switch (type)
			{
				case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
				case "application/json":
				case "application/vnd.openxmlformats-officedocument.presentationml.presentation":
				case "application/rtf":
				case "image/svg+xml":
				case "application/xhtml+xml":
				case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
				case "application/xml":
					return true;

			}
			return false;
		}
		public static void FillMimeType()
		{
			if (!string.IsNullOrEmpty(type))
			{
				return;
			}
			var ext = Path.GetExtension(input.Name).ToLowerInvariant();
			if (string.IsNullOrEmpty(type))
			{
				switch (ext)
				{
					case ".aac":
						type = "audio/aac";
						break;
					case ".avif":
						type = "image/avif";
						break;
					case ".bin":
						type = "application/octet-stream";
						break;
					case ".bmp":
						type = "image/bmp";
						break;
					case ".css":
						type = "text/css";
						break;
					case ".csv":
						type = "text/csv";
						break;
					case ".doc":
						type = "application/msword";
						break;
					case ".docx":
						type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
						break;
					case ".epub":
						type = "application/epub+zip";
						break;
					case ".gz":
						type = "application/gzip";
						break;
					case ".gif":
						type = "image/gif";
						break;
					case ".ico":
						type = "image/x-icon";
						break;
					case ".jar":
						type = "application/java-archive";
						break;
					case ".js":
					case ".mjs":
						type = "text/javascript";
						break;
					case ".json":
						type = "application/json";
						break;
					case ".mid":
					case ".midi":
						type = "audio/midi";
						break;
					case ".mp3":
						type = "audio/mpeg";
						break;
					case ".mp4":
						type = "video/mp4";
						break;
					case ".mpeg":
						type = "video/mpeg";
						break;
					case ".ogg":
						type = "audio/ogg";
						break;
					case ".otf":
						type = "font/otf";
						break;
					case ".pdf":
						type = "application/pdf";
						break;
					case ".ppt":
						type = "application/vnd.ms-powerpoint";
						break;
					case ".pptx":
						type = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
						break;
					case ".rar":
						type = "application/vnd.rar";
						break;
					case ".rtf":
						type = "application/rtf";
						break;
					case ".jpg":
					case ".jpeg":
						type = "image/jpeg";
						break;
					case ".png":
						type = "image/png";
						break;
					case ".apng":
						type = "image/apng";
						break;
					case ".htm":
					case ".html":
						type = "text/html";
						break;
					case ".svg":
						type = "image/svg+xml";
						break;
					case ".tar":
						type = "application/x-tar";
						break;
					case ".tif":
					case ".tiff":
						type = "image/tiff";
						break;
					case ".ttf":
						type = "font/ttf";
						break;
					case ".txt":
						type = "text/plain";
						break;
					case ".vsd":
						type = "application/vnd.visio";
						break;
					case ".wav":
						type = "audio/wav";
						break;
					case ".weba":
						type = "audio/webm";
						break;
					case ".webm":
						type = "video/webm";
						break;
					case ".webp":
						type = "image/webp";
						break;
					case ".woff":
						type = "font/woff";
						break;
					case ".woff2":
						type = "font/woff2";
						break;
					case ".xhtml":
						type = "application/xhtml+xml";
						break;
					case ".xls":
						type = "application/vnd.ms-excel";
						break;
					case ".xlsx":
						type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
						break;
					case ".xml":
						type = "application/xml";
						break;
					case ".zip":
						type = "application/zip";
						break;
					case ".7z":
						type = "application/x-7z-compressed";
						break;
					default:
						throw new ArgumentException($"File type unrecognized. Please specify {CliUtility.SwitchPrefix}type.");
				}
			}
		}
		public static Stream ProcessCompression()
		{
			if (compress == CLStatCompressionType.auto)
			{
				var defl = new MemoryStream();
				var gzip = new MemoryStream();
				var uncomplen = checked((int)input.Length);
				var inputstm = new MemoryStream();
				using (var tmp = input.OpenRead())
				{
					tmp.CopyTo(inputstm);
					inputstm.Position = 0;
				}

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
						compress = CLStatCompressionType.gzip;
						gzip.Position = 0;
						return gzip;
					}
				}
				else
				{
					if (defl.Length < uncomplen)
					{
						compress = CLStatCompressionType.deflate;
						defl.Position = 0;
						return defl;
					}
				}
				compress = CLStatCompressionType.none;
				inputstm.Position = 0;
				return inputstm;
			}
			else
			{
				var comp = new MemoryStream();
				var inputstm = input.OpenRead();

				if (compress == CLStatCompressionType.none)
				{
					return inputstm;
				}
				else if (compress == CLStatCompressionType.deflate)
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
		
		public static void EmitText(string text)
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
		
	}
}
