namespace MyApp.E2E.Infrastructure;

/// <summary>
/// The files a test uploads: "logo.png" and "notes.txt" are small files under <c>TestData/</c> (copied next
/// to the test assembly); "large-photo.png" is written when asked for, because 6 MB does not belong in the
/// repository.
/// </summary>
public static class TestFiles
{
    /// <summary>"logo.png": a small image.</summary>
    public static string LogoPng => Path.Combine(AppContext.BaseDirectory, "TestData", "logo.png");

    /// <summary>"notes.txt": a text file, not an image.</summary>
    public static string NotesTxt => Path.Combine(AppContext.BaseDirectory, "TestData", "notes.txt");

    /// <summary>
    /// "large-photo.png" (6 MB): written under the temporary folder with a PNG signature and padding, which
    /// is all the browser checks when it reads the type and the size of the chosen file.
    /// </summary>
    public static string LargePhotoPng()
    {
        var folder = Path.Combine(Path.GetTempPath(), "myapp-e2e-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "large-photo.png");

        var bytes = new byte[6 * 1024 * 1024];
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        signature.CopyTo(bytes, 0);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
