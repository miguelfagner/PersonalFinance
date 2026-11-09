// Only the FilesDir surface used by the real DatabaseService constructor is replaced.
namespace Android.Content
{
    internal sealed class Context(string directory)
    {
        public TestDirectory FilesDir { get; } = new(directory);
    }

    internal sealed class TestDirectory(string directory)
    {
        public string AbsolutePath { get; } = directory;
    }
}

namespace Android.App
{
    internal static class Application
    {
        public static Content.Context Context { get; set; } = null!;
    }
}
