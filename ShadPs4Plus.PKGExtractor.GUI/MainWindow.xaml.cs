using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ShadPs4Plus.PKGExtractor.GUI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PkgItem> _packages = [];
    private string? _destinationPath;
    private bool _isExtracting;

    public MainWindow()
    {
        InitializeComponent();

        PackagesGrid.ItemsSource = _packages;

        AddLog("ShadPs4Plus PKG Extractor 1.1 iniciado.");
        AddLog(@"Motor oficial: Engine\pkg_extractor.exe");
        AddLog("Clasificación visual por nombre de archivo activada.");

        UpdatePackageCount();
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecciona archivos PKG",
            Filter = "Paquetes de PlayStation (*.pkg)|*.pkg|Todos los archivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = _packages.Count > 0
                ? Path.GetDirectoryName(_packages[^1].FilePath)
                    ?? Environment.GetFolderPath(
                        Environment.SpecialFolder.DesktopDirectory)
                : Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (string file in dialog.FileNames)
            AddPackage(file);

        UpdatePackageCount();
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Selecciona una carpeta que contenga archivos PKG",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        try
        {
            string[] files = Directory.GetFiles(
                dialog.SelectedPath,
                "*.pkg",
                SearchOption.AllDirectories);

            foreach (string file in files)
                AddPackage(file);

            AddLog($"Se encontraron {files.Length} archivos PKG.");
        }
        catch (Exception ex)
        {
            AddLog($"Error al explorar la carpeta: {ex.Message}");
        }

        UpdatePackageCount();
    }

    private void ChooseDestinationButton_Click(
        object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta donde extraer los PKG",
            UseDescriptionForTitle = true,
            SelectedPath = _destinationPath ??
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        _destinationPath = dialog.SelectedPath;
        DestinationText.Text = $"Destino: {_destinationPath}";

        AddLog($"Carpeta de destino: {_destinationPath}");
    }

    private void AddPackage(string file)
    {
        if (_packages.Any(p => string.Equals(
            p.FilePath, file, StringComparison.OrdinalIgnoreCase)))
        {
            AddLog($"Ya está en la lista: {Path.GetFileName(file)}");
            return;
        }

        string fileName = Path.GetFileName(file);
        string type = DetectPkgType(fileName);

        _packages.Add(new PkgItem
        {
            FilePath = file,
            FileName = fileName,
            Type = type,
            TitleId = "—",
            Status = "Pendiente"
        });

        AddLog($"Añadido: {fileName} — Tipo: {type}");
    }

    private static string DetectPkgType(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);

        bool isGame = name.Contains(
            "game", StringComparison.OrdinalIgnoreCase);

        bool isUpdate = name.Contains(
            "update", StringComparison.OrdinalIgnoreCase);

        bool isDlc = name.Contains(
            "dlc", StringComparison.OrdinalIgnoreCase);

        int matches =
            (isGame ? 1 : 0) +
            (isUpdate ? 1 : 0) +
            (isDlc ? 1 : 0);

        if (matches != 1)
            return "Desconocido";

        if (isGame)
            return "Juego";

        if (isUpdate)
            return "Update";

        return "DLC";
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (PkgItem item in PackagesGrid.SelectedItems
                     .Cast<PkgItem>().ToList())
        {
            _packages.Remove(item);
        }

        UpdatePackageCount();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isExtracting)
            return;

        _packages.Clear();
        UpdatePackageCount();
        AddLog("Lista de paquetes limpiada.");
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedPackage(-1);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedPackage(1);
    }

    private void MoveSelectedPackage(int direction)
    {
        if (_isExtracting ||
            PackagesGrid.SelectedItem is not PkgItem item)
            return;

        int oldIndex = _packages.IndexOf(item);
        int newIndex = oldIndex + direction;

        if (newIndex < 0 || newIndex >= _packages.Count)
            return;

        _packages.Move(oldIndex, newIndex);
        PackagesGrid.SelectedItem = item;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isExtracting)
            return;

        if (_packages.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Añade al menos un archivo PKG.",
                "Sin paquetes",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_destinationPath))
        {
            System.Windows.MessageBox.Show(
                "Selecciona primero una carpeta de destino.",
                "Falta el destino",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string enginePath;

        try
        {
            enginePath = FindExtractorPath();
            Directory.CreateDirectory(_destinationPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                "No se puede iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _isExtracting = true;
        SetControlsEnabled(false);

        int total = _packages.Count;
        bool failed = false;

        ExtractionProgress.Value = 0;
        ProgressText.Text = $"0% — 0 de {total}";

        AddLog($"Inicio de extracción: {total} paquete(s).");

        try
        {
            for (int i = 0; i < total; i++)
            {
                PkgItem item = _packages[i];

                if (!File.Exists(item.FilePath))
                {
                    item.Status = "Archivo no encontrado";
                    AddLog($"ERROR: no existe {item.FilePath}");
                    failed = true;
                }
                else
                {
                    item.Status = "Extrayendo...";
                    AddLog($"[{i + 1}/{total}] {item.FileName}");
                    AddLog($"Tipo: {item.Type}");

                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = enginePath,
                            WorkingDirectory =
                                Path.GetDirectoryName(enginePath)!,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        startInfo.ArgumentList.Add(item.FilePath);
                        startInfo.ArgumentList.Add(_destinationPath);

                        using var process = new Process
                        {
                            StartInfo = startInfo
                        };

                        process.Start();

                        Task<string> stdoutTask =
                            process.StandardOutput.ReadToEndAsync();

                        Task<string> stderrTask =
                            process.StandardError.ReadToEndAsync();

                        await process.WaitForExitAsync();

                        string stdout = await stdoutTask;
                        string stderr = await stderrTask;
                        string output = stdout + Environment.NewLine + stderr;

                        bool validPkg = output.Contains(
                            "is a valid PKG",
                            StringComparison.OrdinalIgnoreCase);

                        bool reportedError =
                            output.Contains(
                                "doesn't appear to be a valid PKG file",
                                StringComparison.OrdinalIgnoreCase) ||
                            output.Contains(
                                "Cannot open PKG file",
                                StringComparison.OrdinalIgnoreCase) ||
                            output.Contains(
                                "Could not read SFO",
                                StringComparison.OrdinalIgnoreCase) ||
                            output.Contains(
                                "Cannot extract PKG file",
                                StringComparison.OrdinalIgnoreCase);

                        if (reportedError || !validPkg)
                        {
                            item.Status = "Error";

                            AddLog(
                                $"ERROR: no se pudo confirmar la extracción de {item.FileName}.");

                            AddLog($"Código de salida: {process.ExitCode}");

                            if (!string.IsNullOrWhiteSpace(output))
                                AddLog(output.Trim());

                            failed = true;
                        }
                        else
                        {
                            item.Status = "Completado";

                            if (process.ExitCode != 0)
                            {
                                AddLog(
                                    $"AVISO: el motor devolvió el código {process.ExitCode}, " +
                                    "pero confirmó que el PKG es válido.");
                            }

                            AddLog($"Proceso finalizado: {item.FileName}");

                            try
                            {
                                string[] sfoFiles = Directory.GetFiles(
                                    _destinationPath!,
                                    "param.sfo",
                                    SearchOption.AllDirectories);

                                string? sfoPath = sfoFiles
                                    .OrderByDescending(
                                        File.GetLastWriteTimeUtc)
                                    .FirstOrDefault();

                                if (sfoPath is null)
                                {
                                    item.VerificationStatus =
                                        "SFO no encontrado";

                                    AddLog(
                                        "AVISO: no se encontró param.sfo en el destino.");
                                }
                                else
                                {
                                    ParamSfoData sfo =
                                        ParamSfoReader.Read(sfoPath);

                                    item.Title = sfo.Title;
                                    item.TitleId = sfo.TitleId;
                                    item.AppVersion = sfo.AppVersion;

                                    // No sobrescribir el tipo visual
                                    // con la categoría técnica del SFO.
                                    item.VerificationStatus = "SFO legible";

                                    AddLog($"Título: {sfo.Title}");
                                    AddLog($"TITLE_ID: {sfo.TitleId}");
                                    AddLog($"Versión: {sfo.AppVersion}");
                                    AddLog($"Categoría SFO: {sfo.Category}");
                                }
                            }
                            catch (Exception ex)
                            {
                                item.VerificationStatus =
                                    "Error de lectura";

                                AddLog(
                                    $"No se pudo leer param.sfo: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Error";
                        AddLog($"ERROR: {ex.Message}");
                        failed = true;
                    }
                }

                ExtractionProgress.Value =
                    (i + 1) * 100.0 / total;

                ProgressText.Text =
                    $"{(i + 1) * 100 / total}% — {i + 1} de {total}";

                if (failed)
                {
                    for (int j = i + 1; j < total; j++)
                        _packages[j].Status = "No ejecutado";

                    AddLog("Cola detenida debido a un error.");
                    break;
                }
            }

            if (!failed)
                AddLog("Cola de extracción completada.");
        }
        finally
        {
            _isExtracting = false;
            SetControlsEnabled(true);
        }
    }

    private static string FindExtractorPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Engine",
                "pkg_extractor.exe");

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            @"No se encontró Engine\pkg_extractor.exe. Comprueba que la carpeta Engine contiene el ejecutable oficial y sus DLL.");
    }

    private void SetControlsEnabled(bool enabled)
    {
        AddFilesButton.IsEnabled = enabled;
        AddFolderButton.IsEnabled = enabled;
        RemoveButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        ChooseDestinationButton.IsEnabled = enabled;
        MoveUpButton.IsEnabled = enabled;
        MoveDownButton.IsEnabled = enabled;
        StartButton.IsEnabled = enabled;
    }

    private void AddLog(string message)
    {
        LogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void UpdatePackageCount()
    {
        PackageCountText.Text =
            $"{_packages.Count} paquete{(_packages.Count == 1 ? "" : "s")}";
    }
}

public sealed class PkgItem : INotifyPropertyChanged
{
    private string _type = "Desconocido";
    private string _titleId = "";
    private string _status = "";
    private string _title = "";
    private string _appVersion = "";
    private string _verificationStatus = "Pendiente";

    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";

    public string Type
    {
        get => _type;
        set
        {
            if (_type == value)
                return;

            _type = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(nameof(Type)));
        }
    }

    public string TitleId
    {
        get => _titleId;
        set
        {
            if (_titleId == value)
                return;

            _titleId = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(nameof(TitleId)));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
                return;

            _title = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public string AppVersion
    {
        get => _appVersion;
        set
        {
            if (_appVersion == value)
                return;

            _appVersion = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(nameof(AppVersion)));
        }
    }

    public string VerificationStatus
    {
        get => _verificationStatus;
        set
        {
            if (_verificationStatus == value)
                return;

            _verificationStatus = value;
            PropertyChanged?.Invoke(
                this, new PropertyChangedEventArgs(
                    nameof(VerificationStatus)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}