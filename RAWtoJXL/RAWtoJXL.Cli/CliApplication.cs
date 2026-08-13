using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine;

namespace RAWtoJXL.Cli
{
    public static class CliApplication
    {
        public static async Task<int> RunAsync(
            string[] args,
            TextWriter stdout,
            TextWriter stderr,
            IServiceProvider services)
        {
            var root = CliCommandFactory.Create(services, stdout, stderr);

            var parseResult = root.Parse(args, new ParserConfiguration());
            if (parseResult.Errors.Count > 0)
            {
                foreach (var error in parseResult.Errors)
                {
                    await stderr.WriteLineAsync($"error: {error.Message}");
                }
                await stderr.WriteLineAsync("Run 'rawtojxl-cli --help' for usage.");
                return ExitCodes.Usage;
            }

            var invocation = new InvocationConfiguration
            {
                Output = stdout,
                Error = stderr
            };

            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler? cancelHandler = null;
            try
            {
                cancelHandler = (_, e) =>
                {
                    e.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;
            }
            catch (IOException)
            {
                // Console may be unavailable (e.g. redirected stdin); cancellation still
                // works via the pipeline token.
            }

            try
            {
                return await parseResult.InvokeAsync(invocation, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return ExitCodes.Cancelled;
            }
            finally
            {
                if (cancelHandler != null)
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            }
        }
    }
}
