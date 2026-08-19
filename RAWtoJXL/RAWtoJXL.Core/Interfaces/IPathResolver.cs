namespace RAWtoJXL.Core.Interfaces
{
    public interface IPathResolver
    {
        string ResolveCjxlPath();
        string ResolveDjxlPath();
        string GetTempPath();
    }
}
