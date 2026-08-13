namespace RAWtoJXL.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int PartialFailure = 1;
    public const int Usage = 2;
    public const int NoFiles = 3;
    public const int ToolMissing = 4;
    public const int Cancelled = 130;
}
