using System;
using System.IO;
using System.Threading;
using System.CommandLine;
using RAWtoJXL.Cli.Options;

namespace RAWtoJXL.Cli
{
    public static class CliCommandFactory
    {
        public static RootCommand Create(IServiceProvider services, TextWriter stdout, TextWriter stderr)
        {
            var root = new RootCommand("Convert RAW camera files to JPEG-XL, JPEG or PNG without the GUI.")
            {
                TreatUnmatchedTokensAsErrors = true
            };

            var convertOptions = new CommandOptions();
            var convert = new Command("convert", "Convert RAW files or folders to the target format");
            convert.Add(convertOptions.Paths);
            AddOptions(convert, convertOptions);
            convert.SetAction(async (parse, ct) =>
                await CliHandlers.ConvertAsync(parse, convertOptions, services, stdout, stderr, ct));

            var listOptions = new CommandOptions();
            var list = new Command("list", "List the files that would be converted (no conversion is performed)");
            list.Add(listOptions.Paths);
            AddOptions(list, listOptions);
            list.SetAction(async (parse, ct) =>
                await CliHandlers.ListAsync(parse, listOptions, services, stdout, stderr, ct));

            var presetsJsonOption = new Option<bool>("--json")
            {
                Description = "Machine-readable JSON output"
            };
            var presets = new Command("presets", "List named conversion presets from GUI settings");
            presets.Add(presetsJsonOption);
            presets.SetAction(parse =>
                CliHandlers.PresetsAsync(parse, presetsJsonOption, stdout, stderr));

            root.Add(convert);
            root.Add(list);
            root.Add(presets);

            root.SetAction(parse =>
            {
                WriteUsage(root, stderr);
                return ExitCodes.Usage;
            });

            return root;
        }

        public static void AddOptions(Command command, CommandOptions options)
        {
            command.Add(options.Format);
            command.Add(options.Quality);
            command.Add(options.Conflict);
            command.Add(options.Recursive);
            command.Add(options.OutputDirectory);
            command.Add(options.NoSubfolder);
            command.Add(options.Subfolder);
            command.Add(options.Preset);
            command.Add(options.Extensions);
            command.Add(options.Include);
            command.Add(options.Exclude);
            command.Add(options.ModifiedAfter);
            command.Add(options.ModifiedBefore);
            command.Add(options.SkipMetadata);
            command.Add(options.Effort);
            command.Add(options.Threads);
            command.Add(options.Jobs);
            command.Add(options.DryRun);
            command.Add(options.Json);
            command.Add(options.Quiet);
            command.Add(options.Verbose);
        }

        private static void WriteUsage(Command command, TextWriter writer)
        {
            writer.WriteLine(command.Description);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine("  rawtojxl-cli convert <files-or-folders...> [options]   Convert RAW files");
            writer.WriteLine("  rawtojxl-cli list <files-or-folders...> [options]     List files that would be converted");
            writer.WriteLine("  rawtojxl-cli presets [--json]                         List named conversion presets");
            writer.WriteLine();
            writer.WriteLine("Run 'rawtojxl-cli <command> --help' for details.");
        }
    }
}
