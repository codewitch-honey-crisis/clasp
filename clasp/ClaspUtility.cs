﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace clasp
{
	internal static class ClaspUtility
	{
		public static string ToSZLiteral(byte[] ba, int startSpacing = 0 )
		{
			var sb = new StringBuilder((int)(ba.Length * 1.5));
			sb.Append('"');
			int j = startSpacing;
			for (int i = 0; i < ba.Length; ++i,++j)
			{
				if (i > 0 && 0 == (j % 80) && i < ba.Length - 1)
				{
					sb.Append("\"\r\n    \"");
				}
				var b = ba[i];
				switch ((char)b)
				{
					case '\"':
						sb.Append("\\\""); break;
					case '\r':
						sb.Append("\\r"); break;
					case '\n':
						sb.Append("\\n"); break;
					case '\t':
						sb.Append("\\t"); break;
					case '\\':
                        sb.Append("\\\\"); break;
                    default:
						if (b >= ' ' && b < 128)
						{
							sb.Append((char)b);
						}
						else
						{

							sb.Append("\\x");
							sb.Append(b.ToString("X2"));

						}

						break;
				}

			}
			sb.Append('\"');
			return sb.ToString();
		}
		public static string ToSZLiteral(string value, int startSpacing=0)
		{
			var ba = Encoding.UTF8.GetBytes(value);
			return ToSZLiteral(ba);
		}
		public static string GenerateChunked(string resp)
		{
			if (resp == null)
			{
				return "0\r\n\r\n";
			}
			if (resp == "")
			{
				return "";
			}
			int len = Encoding.UTF8.GetByteCount(resp);
			var str = len.ToString("X") + "\r\n";
			return str + resp + "\r\n";

		}
	}
}
