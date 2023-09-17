using System.Net;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class Logger
{
    public string logFilePath;

    public Logger(string logFileName)
    {
        this.logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);
        Console.WriteLine($"Logging to {logFilePath}");
    }

    public void Log(string message)
    {
        try
        {
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred while logging: {ex.Message}");
        }
    }
}

class Program
{
    private static Logger logger;

    public Program(string logFileName)
    {
        logger = new Logger(logFileName);
    }

    private static string stand_dll;
    private static int gta_pid = 0;
    private static bool game_was_open = false;
    private static bool injected = false;

    private static Random random = new Random();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] buffer, uint size, int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttribute, IntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

    static string[] GetLatestStandVersion()
    {
        try
        {
            logger.Log("Checking for latest Stand version...");
            using (WebClient client = new WebClient())
            {
                string url = "https://stand.gg/versions.txt";
                string response = client.DownloadString(url);

                string[] versionInfo = response.Split(':');

                if (versionInfo.Length >= 2)
                {
                    string standFullVersion = versionInfo[0].Trim();
                    string standDllVersion = versionInfo[1].Trim();
                    logger.Log($"Latest Stand version: {standFullVersion} ({standDllVersion})");
                    return new string[] { standFullVersion, standDllVersion };
                }
                else
                {
                    logger.Log("Invalid version data format");
                    throw new InvalidOperationException("Invalid version data format");
                }
            }
        }
        catch (WebException ex)
        {
            logger.Log($"WebException: {ex.Message}");
            Console.WriteLine($"WebException: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.Log($"Error: {ex.Message}");
            Console.WriteLine($"Error: {ex.Message}");
        }

        return null;
    }

    private static async Task<bool> DownloadStandDllAsync(string standDllVersion)
    {
        string downloadUrl = $"https://stand.gg/Stand {standDllVersion}.dll";
        string destinationPath = stand_dll;

        logger.Log($"Downloading Stand DLL version {standDllVersion} from {downloadUrl} to {destinationPath}");

        if (check_installed())
        {
            logger.Log($"Deleting old Stand DLL ({stand_dll})");
            File.Delete(stand_dll);
        }

        try
        {
            logger.Log($"Creating directory {destinationPath}");
            logger.Log($"Ensuring Stand folder exists");
            string directoryPath = Path.GetDirectoryName(destinationPath);
            Directory.CreateDirectory(directoryPath);
            EnsureStandFolderExists();

            using (WebClient webClient = new WebClient())
            {
                Console.Clear();
                logger.Log($"Downloading Stand DLL version {standDllVersion}");
                Console.WriteLine($"Downloading Stand DLL version {standDllVersion}\n ");
                webClient.DownloadProgressChanged += OnDownloadProgress;
                await webClient.DownloadFileTaskAsync(new Uri(downloadUrl), destinationPath + ".tmp");
            }

            if (File.Exists(destinationPath))
            {
                logger.Log($"Deleting old Stand DLL ({stand_dll})");
                File.Delete(destinationPath);
            }

            logger.Log($"Moving downloaded Stand DLL to {destinationPath}");
            File.Move(destinationPath + ".tmp", destinationPath);

            FileInfo fileInfo = new FileInfo(destinationPath);

            if (fileInfo.Length < 1024)
            {
                File.Delete(destinationPath);
                logger.Log($"It appears that the DLL download for version {standDllVersion} has failed. Ensure there is no interference from antivirus software.");
                Console.WriteLine($"It appears that the DLL download for version {standDllVersion} has failed. Ensure there is no interference from antivirus software.");
                return false;
            }

            logger.Log($"Downloaded Stand DLL version {standDllVersion} to {destinationPath}");
            Console.WriteLine($"\nDownloaded Stand DLL version {standDllVersion} to {destinationPath}\n");
            return true;
        }
        catch (Exception ex)
        {
            logger.Log($"Error downloading Stand DLL: {ex.Message}");
            logger.Log($"Stack Trace: {ex.StackTrace}");
            logger.Log($"Error downloading Stand DLL: {ex.ToString()}");

            Console.WriteLine($"Error downloading Stand DLL: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine($"Error downloading Stand DLL: {ex.ToString()}");
            return false;
        }
    }

    private static int lastLineLength = 0;

    private static void OnDownloadProgress(object sender, DownloadProgressChangedEventArgs e)
    {
        string progressText = $"Downloaded {e.BytesReceived} of {e.TotalBytesToReceive} bytes ({e.ProgressPercentage}% complete)";

        int clearLength = Math.Max(lastLineLength - progressText.Length, 0);

        Console.Write(progressText + new string(' ', clearLength) + "\r");

        lastLineLength = progressText.Length;
    }

    static bool check_installed()
    {
        logger.Log($"Checking if Stand is installed - " + File.Exists(stand_dll));
        return File.Exists(stand_dll);
    }

    static void delete_temp()
    {
        logger.Log($"Deleting Temp folder");
        string temp_folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand", "Bin", "Temp");
        if (Directory.Exists(temp_folder))
        {
            try
            {
                Directory.Delete(temp_folder, true);
                logger.Log($"Deleted Temp folder");
            }
            catch (UnauthorizedAccessException)
            {
                logger.Log("Error: Couldnt delete Temp folder make sure stand isnt running");
                Console.WriteLine("\nError: Couldnt delete Temp folder make sure stand isnt running\n");
            }
        }
        else
        {
            logger.Log($"Temp folder doesnt exist");
        }
    }

    static void EnsureStandFolderExists()
    {
        logger.Log($"Ensuring Stand folder exists");
        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stand"))
            logger.Log($"Creating Stand folder");
        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stand");
        if (!Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stand\\Bin"))
            logger.Log($"Creating Stand Bin folder");
        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Stand\\Bin");
    }

    static bool StandFiles(string delete = "false")
    {
        string standFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand");
        logger.Log($"Checking if Stand folder exists - " + Directory.Exists(standFolderPath));

        if (delete == "true")
        {
            Console.Clear();
            Console.WriteLine("Are you sure this will delete everything in your stand folder\nex: configs, logs,themes, etc (y/n)");
            string choice = Console.ReadLine().ToLower();
            logger.Log($"User chose to delete Stand folder - " + choice);
            if (choice == "y")
            {
                try
                {
                    if (Directory.Exists(standFolderPath))
                    {
                        logger.Log($"Deleting Stand folder");
                        Directory.Delete(standFolderPath, true);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    logger.Log("Error: Couldnt delete Stand folder make sure stand isnt running");
                    Console.WriteLine("\nError: Couldnt delete Temp folder make sure stand isnt running\n");
                }

                return true;
            }
            else
            {
                Console.WriteLine("Canceled Deletion\n");
                return false;
            }
        }

        return Directory.Exists(standFolderPath);
    }


    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        StringBuilder randomString = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            randomString.Append(chars[random.Next(chars.Length)]);
        }
        logger.Log($"Generated random string - " + randomString.ToString());
        return randomString.ToString();
    }


    private static void GetGtaPid()
    {
        Console.Clear();
        Process[] processes = Process.GetProcessesByName("GTA5");
        logger.Log($"Checking if GTA5 is running - " + processes.Length);

        if (processes.Length > 0)
        {
            Process gtaProcess = processes[0];

            if (gta_pid == gtaProcess.Id)
            {
                return;
            }

            gta_pid = gtaProcess.Id;
            game_was_open = true;

            logger.Log($"GTA5 found: PID " + gta_pid);
            Console.WriteLine($"GTA5 found: PID {gta_pid}\n");
        }
        else
        {
            logger.Log($"GTA5 not found");
            Console.Write("Are you sure GTA5 is running");

            for (int i = 0; i < 5; i++)
            {
                Console.Write(".");
                Thread.Sleep(1000);
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);
            }

            Console.Clear();
            gta_pid = 0;
            game_was_open = false;
        }
    }

    private static unsafe void Inject(string standDllVersion)
    {
        int num = 0;
        GetGtaPid();

        if (injected)
        {
            Console.WriteLine("Stand is already injected. Do you want to reinject? (y/n)");
            string choice = Console.ReadLine().ToLower();
            logger.Log($"User chose to reinject Stand - " + choice);

            if (choice == "y")
            {
                injected = false;
            }
            else
            {
                Console.WriteLine("\n");
                return;
            }
        }

        if (gta_pid == 0)
        {
            logger.Log($"GTA5 not found");
            Console.WriteLine("Failed to get a hold of the game's process.");
            return;
        }

        IntPtr hProcess = OpenProcess(1082u, 1, (uint)gta_pid);
        if (hProcess != IntPtr.Zero)
        {
            try
            {
                logger.Log($"Injecting Stand");
                Console.WriteLine("Injecting Stand...");
                delete_temp();
                IntPtr loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                if (loadLibraryAddress == IntPtr.Zero)
                {
                    logger.Log($"Failed to find LoadLibraryW");
                    Console.WriteLine("Failed to find LoadLibraryW.");
                    return;
                }

                string temp_folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand", "Bin", "Temp");
                if (!Directory.Exists(temp_folder))
                {
                    logger.Log($"Creating Temp Folder");
                    Directory.CreateDirectory(temp_folder);
                    Console.WriteLine("Created Temp Folder");
                }

                if (!File.Exists(stand_dll))
                {
                    logger.Log($"Downloading Stand DLL");
                    DownloadStandDllAsync(standDllVersion).Wait();
                }

                Thread.Sleep(1000);

                string dllRandomized = Path.Combine(temp_folder, "SL_" + GenerateRandomString(5) + ".dll");
                File.Copy(stand_dll, dllRandomized);

                byte[] bytes = Encoding.Unicode.GetBytes(dllRandomized);

                IntPtr allocatedMemory = VirtualAllocEx(hProcess, (IntPtr)(void*)null, (IntPtr)bytes.Length, 12288u, 64u);

                if (allocatedMemory == IntPtr.Zero)
                {
                    logger.Log($"Failed to allocate memory in the game's process");
                    Console.WriteLine("Failed to allocate memory in the game's process.");
                    return;
                }

                if (WriteProcessMemory(hProcess, allocatedMemory, bytes, (uint)bytes.Length, 0) == 0)
                {
                    logger.Log($"Couldn't write to allocated memory");
                    Console.WriteLine("Couldn't write to allocated memory.");
                    return;
                }

                IntPtr remoteThread = CreateRemoteThread(hProcess, IntPtr.Zero, IntPtr.Zero, loadLibraryAddress, allocatedMemory, 0, IntPtr.Zero);

                if (remoteThread == IntPtr.Zero)
                {
                    logger.Log($"Failed to create a remote thread for " + stand_dll);
                    Console.WriteLine("Failed to create a remote thread for " + stand_dll);
                    return;
                }

                num++;
                Console.WriteLine("Your a Stand user now!\n");
                logger.Log($"Injected Stand");
                injected = true;
                targetProcess = Process.GetProcessById(gta_pid);
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }
        else
        {
            logger.Log($"Failed to get a handle to the game's process");
            Console.WriteLine("Failed to get a handle to the game's process.");
        }
    }

    static void Disclaimer()
    {
        Console.Clear();
        Console.Title = "Stand CLI Disclaimer";
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("Disclaimer: this was forked from https://github.com/larsl2005/StandConsoleInjector");
        Console.WriteLine("Im not sure if injecting stand this way is safe so use at your own risk");
        Console.WriteLine("If you have any issues with this fork make a issue on github or dm me on discord @ Creations\n\n");
        Console.WriteLine("Known problems:");
        Console.WriteLine("> injecting before the game has fully loaded will crash your game");
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.ResetColor();
        Console.Clear();
    }

    static List<string> FindGtaDirectories(string baseDirectory)
    {
        List<string> gtaDirectories = new List<string>();

        try
        {
            string[] topLevelDirectories = Directory.GetDirectories(baseDirectory).Where(dir => !dir.EndsWith("lost+found")).ToArray();

            foreach (string topLevelDir in topLevelDirectories)
            {
                string gtaPath = Path.Combine(topLevelDir, "SteamLibrary", "steamapps", "common", "Grand Theft Auto V");
                if (Directory.Exists(gtaPath))
                {
                    gtaDirectories.Add(gtaPath);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("Access denied to certain directories in the base directory.");
        }

        return gtaDirectories;
    }

    static void CreateLauncher(string path = "", bool userFound = true)
    {
        Console.Clear();
        string gtaPath = "";

        if (path == "")
        {
            string linuxEx = $"/home/{Environment.UserName}/.local/share/Steam/steamapps/common/Grand Theft Auto V/";
            string windowsEx = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V";
            string baseDirectory = "Z:\\mnt";


            List<string> defaultInstallations = new List<string>
            {
                linuxEx,
                windowsEx,
                $"/home/{Environment.UserName}/Games/Heroic/GTAV/",
                "/usr/local/games/Grand Theft Auto V/",
                "/opt/games/Grand Theft Auto V/"
            };

            if (Directory.Exists(baseDirectory))
            {
                List<string> gtaDirectories = FindGtaDirectories(baseDirectory);
                foreach (string gtaDir in gtaDirectories)
                {
                    defaultInstallations.Add(gtaDir);
                }
            }

            if (userFound)
            {
                List<string> validInstallations = defaultInstallations
                    .Where(installationPath => File.Exists(Path.Combine(installationPath, "PlayGTAV.exe")))
                    .ToList();

                if (validInstallations.Count > 0)
                {
                    Console.WriteLine("Found the following Grand Theft Auto V installations");

                    for (int i = 0; i < validInstallations.Count; i++)
                    {
                        Console.WriteLine($"{i + 1}. {validInstallations[i]}");
                    }

                    Console.WriteLine("\nEnter the number for the folder you want to use or enter 'c' to use a custom path:\n");
                    string input = Console.ReadLine().ToLower();

                    if (int.TryParse(input, out int selection) && selection >= 1 && selection <= validInstallations.Count)
                    {
                        gtaPath = validInstallations[selection - 1];
                        logger.Log($"User selected option {selection} - {gtaPath}");
                    }
                    else if (input == "c")
                    {
                        CreateLauncher("", false);
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Invalid option. Please select a valid option (1-3).");
                        Console.WriteLine("User selected an invalid option.");
                    }
                }
            }
            else
            {
                Console.WriteLine("Enter Gta5.exe path");
                Console.WriteLine($"Windows ex: {windowsEx}");
                Console.WriteLine($"Linux ex: {linuxEx}\n");
                gtaPath = Console.ReadLine();
            }
        }
        else
        {
            gtaPath = path;
        }

        string gtaLauncher = Path.Combine(gtaPath, "PlayGTAV.exe");

        if (File.Exists(gtaLauncher))
        {
            if (File.Exists(gtaPath + "_PlayGTAV.exe"))
            {
                Console.WriteLine("\nLauncher already installed");
                Console.WriteLine("Would you like to: ");
                Console.WriteLine("1. Delete the launcher");
                Console.WriteLine("2. Reinstall the launcher");
                Console.WriteLine("3. Cancel");

                string choice = Console.ReadLine();
                logger.Log("User chose option - " + choice);

                switch (choice)
                {
                    case "1":
                        File.Delete(gtaLauncher);
                        File.Copy(gtaPath + "_PlayGTAV.exe", Path.Combine(gtaPath, "PlayGTAV.exe"));
                        File.Delete(Path.Combine(gtaPath, "_PlayGTAV.exe"));
                        Console.WriteLine("Deleted the launcher\n");
                        logger.Log("Deleted the launcher");
                        break;
                    case "2":
                        File.Delete(gtaLauncher);
                        File.Copy(gtaPath + "_PlayGTAV.exe", Path.Combine(gtaPath, "PlayGTAV.exe"));
                        File.Delete(Path.Combine(gtaPath, "_PlayGTAV.exe"));
                        Thread.Sleep(500);
                        logger.Log("Reinstalling the launcher");
                        CreateLauncher(gtaPath);
                        break;
                    case "3":
                        Console.WriteLine("Cancelled\n");
                        logger.Log("Cancelled");
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please select a valid option (1-3).");
                        logger.Log("Invalid option. Please select a valid option (1-3).");
                        break;
                }
            }
            else
            {
                File.Copy(gtaLauncher, gtaPath + "_PlayGTAV.exe");
                File.Delete(gtaLauncher);
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, Path.Combine(gtaPath, "PlayGTAV.exe"));
                Console.WriteLine("Created launcher\n");
                logger.Log("Created launcher");
                Thread.Sleep(1000);
            }
        }
    }




    private static Process targetProcess = null;
    static void Main(string[] args)
    {
        Program program = new Program("log.txt");
        string exeName = Process.GetCurrentProcess().ProcessName + ".exe";

        if (exeName == "PlayGTAV.exe")
        {
            string[] allArgs = Environment.GetCommandLineArgs();

            /*for (int i = 0; i < allArgs.Length; i++)
            {
                Console.WriteLine($"Argument {i}: {allArgs[i]}");
            }

            Thread.Sleep(2000); -- arguments for the epic games launcher */

            string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(exeFolder, "_PlayGTAV.exe")))
            {
                Process.Start(Path.Combine(exeFolder, "_PlayGTAV.exe"), string.Join(" ", allArgs.Skip(1)));
            }
        }

        Disclaimer();

        Console.ForegroundColor = ConsoleColor.Red;

        Console.Clear();
        string[] latestStandVersion = GetLatestStandVersion();

        if (latestStandVersion != null && latestStandVersion.Length >= 2)
        {
            string standFullVersion = latestStandVersion[0];
            string standDllVersion = latestStandVersion[1];

            stand_dll = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand", "Bin", $"Stand {standDllVersion}.dll");

            Thread.Sleep(1000);
            Console.Clear();

            while (true)
            {

                string installed_line = check_installed() ? "Reinstall Stand DLL" : "Install Stand DLL";
                string injected_line = injected ? "Reinject Stand" : "Inject Stand";
                bool stand_exists = StandFiles();
                Console.Title = "Stand CLI - " + standFullVersion + ":" + standDllVersion;

                Console.WriteLine("Stand CLI - " + standFullVersion + ":" + standDllVersion);
                Console.WriteLine("Stand DLL version - " + standDllVersion + "\n\n");

                Console.WriteLine("Select an option:\n");
                Console.WriteLine("1. " + installed_line);
                Console.WriteLine("2. " + injected_line);
                Console.WriteLine("3. Open Logs File");
                Console.WriteLine("4. Create Launcher");

                if (stand_exists)
                {
                    Console.WriteLine("5. Delete Stand");
                    Console.WriteLine("6. Exit");
                }
                else
                {
                    Console.WriteLine("5. Exit");
                }

                string choice = Console.ReadLine();
                logger.Log($"User chose option - " + choice);

                switch (choice)
                {
                    case "1":
                        Task.Run(async () => await DownloadStandDllAsync(standDllVersion)).Wait();
                        break;
                    case "2":
                        Inject(standDllVersion);
                        break;
                    case "3":
                        if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand", "Logs", "Stand.log")))
                        {
                            logger.Log($"Opening Stand log file");
                            Process.Start("notepad.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stand", "Log.txt"));
                        }
                        if (File.Exists(logger.logFilePath))
                        {
                            logger.Log($"Opening Stand CLI log file");
                            Process.Start("notepad.exe", logger.logFilePath);
                        }
                        break;
                    case "4":
                        CreateLauncher();
                        break;
                    case "5":
                        if (stand_exists)
                        {
                            StandFiles("true");
                        }
                        else
                        {
                            Console.WriteLine("\nExiting...");
                            if (injected)
                            {
                                if (targetProcess != null)
                                {
                                    targetProcess.Kill();
                                }
                            }
                            Environment.Exit(0);
                        }
                        break;
                    case "6":
                        if (stand_exists)
                        {
                            Console.WriteLine("\nExiting...");
                            if (injected)
                            {
                                if (targetProcess != null)
                                {
                                    targetProcess.Kill();
                                }
                            }
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("Invalid option. Please select a valid option (1-3).");
                        }
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please select a valid option (1-4).");
                        break;
                }
            }

        }
        else
        {
            Console.WriteLine("Failed to retrieve valid version data. Exiting the program.");
        }
    }
}
