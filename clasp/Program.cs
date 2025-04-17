﻿using Cli;

using System.Text;
namespace clasp
{
	internal class Program
	{
		
		static int Main(string[] args)
		{
#if !DEBUG
			try
			{
#endif
			using (var parsed = CliUtility.ParseAndSet(args, null, typeof(Clasp)))
			{
				return Clasp.Run();
			}
#if !DEBUG
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Error: " + ex.Message);
				return 1;
			}
#endif
		}
	}
}
