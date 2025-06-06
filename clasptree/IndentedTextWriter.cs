﻿using System.Text;

namespace clasptree
{
	
	class IndentedTextWriter : TextWriter
	{
		bool _needIndent;
		TextWriter _writer;
		public IndentedTextWriter(TextWriter writer)
		{
			if (writer == null) throw new ArgumentNullException();
			_writer = writer;
			_needIndent = false;
		}
		public override Encoding Encoding => _writer.Encoding;
		public override void Write(char value)
		{
			if (_needIndent)
			{
				_writer.Write(_Indent(IndentLevel));
				_needIndent = false;
			}
			if (value == '\n')
			{
				_needIndent = true;
				_writer.Write("\n");
			}
			else
				_writer.Write(value);
		}
		public int IndentLevel { get; set; } = 0;
		public string Indent { get; set; } = "    ";
		string _Indent(int level)
		{
			if (level <= 0) return "";
			var len = level * Indent.Length;
			var sb = new StringBuilder(len, len);
			for (var i = 0; i < level; ++i)
			{
				sb.Append(Indent);
			}
			return sb.ToString();
		}
	}
}
