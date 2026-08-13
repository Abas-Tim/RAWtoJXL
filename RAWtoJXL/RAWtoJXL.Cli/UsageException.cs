using System;
using System.IO;

namespace RAWtoJXL.Cli
{
    public sealed class UsageException : Exception
    {
        public UsageException(string message) : base(message)
        {
        }
    }
}
