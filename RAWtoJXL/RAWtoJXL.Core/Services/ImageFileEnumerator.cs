using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RAWtoJXL.Core.Services
{
    public static class ImageFileEnumerator
    {
        public static IReadOnlyList<string> Enumerate(
            IEnumerable<string> paths,
            bool recursive,
            IEnumerable<string> allowedExtensions)
        {
            var extensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
            var results = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    if (extensions.Contains(Path.GetExtension(path)))
                    {
                        results.Add(Path.GetFullPath(path));
                    }
                }
                else if (Directory.Exists(path))
                {
                    var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (var file in Directory.GetFiles(path, "*.*", option))
                    {
                        if (extensions.Contains(Path.GetExtension(file)))
                        {
                            results.Add(Path.GetFullPath(file));
                        }
                    }
                }
            }

            return results.ToList();
        }
    }
}
