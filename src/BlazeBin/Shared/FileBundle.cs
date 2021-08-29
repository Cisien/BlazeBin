namespace BlazeBin.Shared
{
    public record FileBundle(string Id, List<FileData> Files)
    {
        public string? LastServerId { get; set; }

        public static FileBundle Empty => new(string.Empty, new List<FileData> { FileData.Empty });

        public static FileBundle New(string id, string fileId)
        {
            return Empty with { Id = id, Files = new List<FileData> { FileData.Empty with { Id = fileId } } };
        }
    }
}
