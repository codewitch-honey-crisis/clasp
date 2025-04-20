using Cli;

using System.IO.Compression;
using System.Text;

namespace clstat
{

	internal class Program
	{
		
		static int Main(string[] args)
		{
#if !DEBUG
			try
			{
#endif
				using (var parsed = CliUtility.ParseAndSet(args, null, typeof(CLStat)))
				{
					var code = CLStat.Run();
					var ofn = CliUtility.GetFilename(CLStat.output);
					if (!string.IsNullOrEmpty(ofn))
					{
						Console.Error.WriteLine($"Successfully wrote to {ofn}.");
					}
					return code;
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

