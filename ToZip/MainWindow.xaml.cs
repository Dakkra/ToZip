using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ToZip;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string SevenZipExecutable = "7z";
    private string? _sevenZipPath;

    private Brush defaultColor = Brushes.White;
    private Brush hoverColor = Brushes.LightSkyBlue;
    private Brush successColor = Brushes.LightGreen;
    private Brush errorColor = Brushes.IndianRed;

    public MainWindow()
    {
        InitializeComponent();
        this.Background = defaultColor;
        this.AllowDrop = true;
        _sevenZipPath = Find7ZipExecutable();
        Console.WriteLine(_sevenZipPath);
        Console.WriteLine(Get7ZipVersion());
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDrop(e);
        this.Background = hoverColor;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDrop(e);
        this.Background = defaultColor;
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        var fileDropData = e.Data.GetData(DataFormats.FileDrop) as string[];
        switch (fileDropData)
        {
            case { Length: 0 }:
                Fail("Failed to get path for dropped file");
                break;
            case { Length: > 1 }:
                Fail("Too many files, please only use one");
                break;
            default:
                if (fileDropData != null) Process_Input(fileDropData[0]);
                else Fail("Failed to get path for dropped file");
                break;
        }
    }

    private void Process_Input(string path)
    {
        Console.WriteLine("File received: " + path);

        if (string.IsNullOrEmpty(_sevenZipPath) || !File.Exists(_sevenZipPath))
        {
            Fail("7-Zip executable not found.");
            return;
        }

        try
        {
            // 1. Detect with 7z if the input path is a valid archive
            if (!IsArchiveValid(path))
            {
                Fail("The file is not a valid archive");
                return;
            }

            // 2. Unpack it
            string tempDirectory = Path.Combine(Path.GetTempPath(), "ToZip_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);

            try
            {
                if (!ExtractArchive(path, tempDirectory))
                {
                    Fail("Failed to unpack the archive.");
                    return;
                }

                // 3. Repack in zip format and place next to the original
                string originalDirectory = Path.GetDirectoryName(path) ?? string.Empty;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                string destinationZip = Path.Combine(originalDirectory, fileNameWithoutExtension + ".zip");

                // Ensure unique name if .zip already exists
                int count = 1;
                while (File.Exists(destinationZip))
                {
                    destinationZip = Path.Combine(originalDirectory, $"{fileNameWithoutExtension}_{count++}.zip");
                }

                if (!CreateZipArchive(tempDirectory, destinationZip))
                {
                    Fail("Failed to create zip archive.");
                    return;
                }

                // 4. Success message
                Success("The file was converted");
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }
        catch (Exception ex)
        {
            Fail($"An error occurred: {ex.Message}");
        }
    }

    private bool IsArchiveValid(string path)
    {
        return Run7ZipCommand($"t \"{path}\"") == 0;
    }

    private bool ExtractArchive(string archivePath, string destinationDirectory)
    {
        return Run7ZipCommand($"x \"{archivePath}\" -o\"{destinationDirectory}\" -y") == 0;
    }

    private bool CreateZipArchive(string sourceDirectory, string destinationZip)
    {
        // -tzip specifies zip format
        // sourceDirectory\* extracts all files from sourceDirectory into the archive
        return Run7ZipCommand($"a -tzip \"{destinationZip}\" \"{Path.Combine(sourceDirectory, "*")}\"") == 0;
    }

    private int Run7ZipCommand(string arguments)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _sevenZipPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null) return -1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private void Success(string message)
    {
        this.Background = successColor;
        MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Fail(string message)
    {
        this.Background = errorColor;
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private string? Find7ZipExecutable()
    {
        // Check common installation paths
        string[] commonPaths = new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Check PATH environment variable
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable != null)
        {
            var paths = pathVariable.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, "7z.exe");
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        // Try just "7z" as fallback (might work if in PATH)
        return SevenZipExecutable;
    }

    private string? Get7ZipVersion()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _sevenZipPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                // Skip empty line
                process.StandardOutput.ReadLine();
                string? output = process.StandardOutput.ReadLine();
                process.WaitForExit();
                return output;
            }
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }

        return "7z not found";
    }
}