using ShadPs4Plus.PKGExtractor.GUI.Updates;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;

namespace ShadPs4Plus.PKGExtractor.GUI;

public partial class MainWindow : Window
{
    private readonly UpdateChecker _updateChecker = new();
    private bool _isCheckingForUpdates;
    private readonly ObservableCollection<PkgItem> _packages = [];
    private string? _destinationPath;
    private bool _isExtracting;
    private bool _isClosing;
    private bool _isCancelling;
    private Process? _activeProcess;

    public MainWindow()
    {
        InitializeComponent();

        PackagesGrid.ItemsSource = _packages;

        AddLog("ShadPs4Plus PKG Extractor 1.1 iniciado.");
        AddLog(@"Motor oficial: Engine\pkg_extractor.exe");
        AddLog("Clasificación visual por nombre de archivo activada.");

        UpdatePackageCount();

        Closing += MainWindow_Closing;
    }

    private async void UpdateButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;

        try
        {
            await CheckForUpdatesAsync();
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isCheckingForUpdates)
            return;

        _isCheckingForUpdates = true;

        try
        {
            AddLog("Comprobando actualizaciones...");

            GitHubReleaseInfo? release =
                await _updateChecker.CheckForUpdateAsync();

            if (release is null)
            {
                AddLog(
                    "No hay ninguna versión publicada todavía " +
                    "o GitHub no devolvió una versión válida.");

                System.Windows.MessageBox.Show(
                    "Todavía no hay una versión publicada para " +
                    "esta aplicación.\n\n" +
                    "La comprobación de actualizaciones estará disponible " +
                    "cuando se publique la primera versión en GitHub Releases.",
                    "Sin versiones publicadas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            Version installedVersion =
                Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(1, 0, 0);

            int comparison = release.Version.CompareTo(installedVersion);

            if (comparison > 0)
            {
                System.Windows.MessageBox.Show(
                    $"Hay una nueva versión disponible.\n\n" +
                    $"Versión instalada: {installedVersion}\n" +
                    $"Nueva versión: {release.Version}\n\n" +
                    $"{release.Name}",
                    "Actualización disponible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                AddLog($"Nueva versión disponible: {release.Version}");
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Estás utilizando la última versión de " +
                    $"ShadPs4Plus PKG Extractor GUI.\n\n" +
                    $"Versión instalada: {installedVersion}",
                    "Estás actualizado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                AddLog($"La aplicación está actualizada: {installedVersion}");
            }
        }

        catch (InvalidDataException ex)
        {
            AddLog($"Respuesta de actualización inválida: {ex.Message}");

            System.Windows.MessageBox.Show(
                "GitHub devolvió información de actualización inválida.\n\n" +
                "Inténtalo de nuevo más tarde.",
                "Respuesta inválida",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (HttpRequestException ex)
        {
            AddLog($"Error de conexión al comprobar actualizaciones: {ex.Message}");

            System.Windows.MessageBox.Show(
                "No se pudo conectar con GitHub para comprobar actualizaciones.\n\n" +
                "Comprueba tu conexión a Internet e inténtalo de nuevo.",
                "Error de conexión",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (TaskCanceledException ex)
        {
            AddLog($"Tiempo de espera agotado al comprobar actualizaciones: {ex.Message}");

            System.Windows.MessageBox.Show(
                "La comprobación de actualizaciones tardó demasiado.\n\n" +
                "Inténtalo de nuevo más tarde.",
                "Tiempo de espera agotado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AddLog($"Error inesperado al comprobar actualizaciones: {ex}");

            System.Windows.MessageBox.Show(
                "Ocurrió un error inesperado al comprobar actualizaciones.\n\n" +
                ex.Message,
                "Error de actualización",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecciona archivos PKG",
            Filter = "Paquetes de PlayStation (*.pkg)|*.pkg|Todos los archivos (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = Directory.Exists(_lastSelectedDirectory)
                ? _lastSelectedDirectory
                : Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog() != true)
            return;

        SaveLastSelectedDirectory(
        Path.GetDirectoryName(dialog.FileNames[0])
        ?? _lastSelectedDirectory);

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

    private void Window_DragOver(
    object sender,
    System.Windows.DragEventArgs e)
    {
        if (_isExtracting || _isClosing)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Window_Drop(
    object sender,
    System.Windows.DragEventArgs e)
    {
        if (_isExtracting || _isClosing)
            return;

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop)
            is not string[] files)
            return;

        int added = 0;

        foreach (string file in files)
        {
            if (Directory.Exists(file))
            {
                try
                {
                    foreach (string pkg in Directory.GetFiles(
                        file,
                        "*.pkg",
                        SearchOption.AllDirectories))
                    {
                        int previousCount = _packages.Count;
                        AddPackage(pkg);

                        if (_packages.Count > previousCount)
                            added++;
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"Error al explorar {file}: {ex.Message}");
                }
            }
            else if (File.Exists(file) &&
                     string.Equals(
                         Path.GetExtension(file),
                         ".pkg",
                         StringComparison.OrdinalIgnoreCase))
            {
                int previousCount = _packages.Count;
                AddPackage(file);

                if (_packages.Count > previousCount)
                    added++;
            }
        }

        UpdatePackageCount();
        AddLog($"Arrastre completado: {added} PKG nuevos añadidos.");
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



    private static readonly string LastDirectoryConfigPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ShadPs4Plus.PKGExtractor.GUI",
            "last-pkg-directory.txt");

    private string _lastSelectedDirectory = LoadLastSelectedDirectory();

    private static string LoadLastSelectedDirectory()
    {
        try
        {
            if (File.Exists(LastDirectoryConfigPath))
            {
                string path = File.ReadAllText(
                    LastDirectoryConfigPath).Trim();

                if (Directory.Exists(path))
                    return path;
            }
        }
        catch
        {
            // Si no se puede leer la configuración, usar el Escritorio.
        }

        return Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
    }

    private void SaveLastSelectedDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                return;

            string? configDirectory =
                Path.GetDirectoryName(LastDirectoryConfigPath);

            if (configDirectory is null)
                return;

            Directory.CreateDirectory(configDirectory);

            File.WriteAllText(LastDirectoryConfigPath, path);

            _lastSelectedDirectory = path;
        }
        catch (Exception ex)
        {
            AddLog(
                $"No se pudo guardar la última carpeta: {ex.Message}");
        }
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
        _isCancelling = false;

        SetControlsEnabled(false);
        CancelButton.IsEnabled = true;

        int total = _packages.Count;
        bool failed = false;

        ExtractionProgress.Value = 0;
        ProgressText.Text = $"0% — 0 de {total}";

        AddLog($"Inicio de extracción: {total} paquete(s).");

        try
        {
            for (int i = 0; i < total; i++)
            {
                if (_isClosing)
                    break;


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

                    Process? process = null;

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

                        process = new Process
                        {
                            StartInfo = startInfo
                        };

                        _activeProcess = process;
                        process.Start();




                        Task<string> stdoutTask = ReadExtractorOutputAsync(
                            process.StandardOutput);

                        Task<string> stderrTask = ReadExtractorOutputAsync(
                            process.StandardError);

                        await process.WaitForExitAsync();

                        await Task.WhenAll(stdoutTask, stderrTask);

                        if (_isClosing)
                            break;

                        if (_isCancelling)
                        {
                            item.Status = "Cancelado";
                            AddLog($"Extracción cancelada: {item.FileName}");
                            break;
                        }

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
                    finally
                    {
                        if (ReferenceEquals(_activeProcess, process))
                            _activeProcess = null;

                        process?.Dispose();
                    }
                }

                ExtractionProgress.Value =
                    (i + 1) * 100.0 / total;

                ProgressText.Text =
                    $"{(i + 1) * 100 / total}% — {i + 1} de {total}";

                if (failed || _isCancelling)
                {
                    for (int j = i + 1; j < total; j++)
                        _packages[j].Status = "No ejecutado";

                    if (failed)
                        AddLog("Cola detenida debido a un error.");
                    else if (_isCancelling)
                        AddLog("Cola cancelada por el usuario.");

                    break;
                }
            }

            if (!failed && !_isClosing)
                AddLog("Cola de extracción completada.");
        }
        finally
        {
            _isExtracting = false;
            _isCancelling = false;

            SetControlsEnabled(true);
            CancelButton.IsEnabled = false;
        }
    }


    private void ProcessExtractorOutputLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        AddLog(line);

        // El motor informa: "Extracting file X of Y to ..."
        const string marker = "Extracting file ";

        int markerIndex = line.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
            return;

        string progressPart = line[(markerIndex + marker.Length)..];

        int ofIndex = progressPart.IndexOf(
            " of ",
            StringComparison.OrdinalIgnoreCase);

        if (ofIndex < 0)
            return;

        string currentText = progressPart[..ofIndex].Trim();

        string remaining = progressPart[(ofIndex + 4)..];

        int toIndex = remaining.IndexOf(
            " to ",
            StringComparison.OrdinalIgnoreCase);

        if (toIndex >= 0)
            remaining = remaining[..toIndex];

        if (!int.TryParse(currentText, out int currentFile) ||
            !int.TryParse(remaining.Trim(), out int totalFiles) ||
            totalFiles <= 0)
        {
            return;
        }

        currentFile = Math.Clamp(currentFile, 0, totalFiles);

        double percentage = currentFile * 100.0 / totalFiles;

        Dispatcher.Invoke(() =>
        {
            ExtractionProgress.Value = percentage;

            ProgressText.Text =
                $"{percentage:0.#}% — Archivo {currentFile} de {totalFiles}";
        });
    }



    private async Task<string> ReadExtractorOutputAsync(
        StreamReader reader)
    {
        var output = new System.Text.StringBuilder();

        while (await reader.ReadLineAsync() is { } line)
        {
            output.AppendLine(line);

            await Dispatcher.InvokeAsync(() =>
            {
                ProcessExtractorOutputLine(line);
            });
        }

        return output.ToString();
    }



    private void MainWindow_Closing(
    object? sender,
    CancelEventArgs e)
    {
        _isClosing = true;

        Process? process = _activeProcess;

        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso ya había terminado.
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"No se pudo detener el motor de extracción:\n{ex.Message}",
                "Error al cerrar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isExtracting || _isCancelling)
            return;

        _isCancelling = true;
        ExtractionProgress.Value = 0;
        ProgressText.Text = "0% — Cancelado...";
        CancelButton.IsEnabled = false;

        AddLog("Cancelación solicitada por el usuario.");

        Process? process = _activeProcess;

        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                AddLog("Motor de extracción detenido.");
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso ya había terminado.
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            AddLog($"ERROR al detener el motor: {ex.Message}");
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