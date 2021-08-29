namespace BlazeBin.Shared
{
    public record FileData(string Id, string Filename, string Data)
    {
        public static FileData Empty => new(string.Empty, "newfile.cs", string.Empty);
    };
}
