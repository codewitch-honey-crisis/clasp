using Cli;

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
				var code = Clasp.Run();
				var ofn = CliUtility.GetFilename(Clasp.output);
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
