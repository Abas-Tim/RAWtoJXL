using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RAWtoJXL.Cli
{
    public static class FileFilter
    {
        public static IReadOnlyList<string> Apply(IEnumerable<string> files, ResolvedOptions options)
        {
            var includes = options.Include.Select(GlobToRegex).ToList();
            var excludes = options.Exclude.Select(GlobToRegex).ToList();
            var result = new List<string>();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);

                if (includes.Count > 0 && !includes.Any(r => r.IsMatch(name)))
                    continue;
                if (excludes.Any(r => r.IsMatch(name)))
                    continue;

                if (options.ModifiedAfter.HasValue || options.ModifiedBefore.HasValue)
                {
                    var mtime = File.GetLastWriteTime(file);
                    if (options.ModifiedAfter.HasValue && mtime < options.ModifiedAfter.Value)
                        continue;
                    if (options.ModifiedBefore.HasValue && mtime > options.ModifiedBefore.Value)
                        continue;
                }

                result.Add(file);
            }

            return result;
        }

        internal static Regex GlobToRegex(string glob)
        {
            var pattern = "^" + Regex.Escape(glob)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
