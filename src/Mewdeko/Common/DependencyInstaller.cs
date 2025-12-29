using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Mewdeko.Common;

/// <summary>
///     Dependency installer for PostgreSQL and Redis across different operating systems.
///     Handles detection, installation, and configuration of required services.
/// </summary>
/// <remarks>
///     Core functionality:
///     - Detects and installs dependencies on supported Linux distributions
///     - Provides guided installation for Windows systems
///     - Configures database users and permissions
///     - Generates connection strings
///     - Verifies service status
/// </remarks>
public static class DependencyInstaller
{
    private static readonly Dictionary<LinuxDistro, string[]> InstallCommands = new()
    {
        {
            LinuxDistro.Ubuntu, [
                "sudo apt update",
                "sudo apt install -y postgresql-17 postgresql-contrib",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo apt install -y redis-server",
                "sudo systemctl start redis-server",
                "sudo systemctl enable redis-server"
            ]
        },
        {
            LinuxDistro.Debian, [
                "sudo apt update",
                "sudo apt install -y postgresql-17 postgresql-contrib",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo apt install -y redis-server",
                "sudo systemctl start redis-server",
                "sudo systemctl enable redis-server"
            ]
        },
        {
            LinuxDistro.Fedora, [
                "sudo dnf update -y",
                "sudo dnf install -y postgresql postgresql-server postgresql-contrib",
                "sudo postgresql-setup --initdb",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo dnf install -y redis",
                "sudo systemctl start redis",
                "sudo systemctl enable redis"
            ]
        },
        {
            LinuxDistro.Rhel, [
                "sudo yum update -y",
                "sudo yum install -y postgresql postgresql-server postgresql-contrib",
                "sudo postgresql-setup --initdb",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo yum install -y redis",
                "sudo systemctl start redis",
                "sudo systemctl enable redis"
            ]
        },
        {
            LinuxDistro.CentOs, [
                "sudo yum update -y",
                "sudo yum install -y postgresql postgresql-server postgresql-contrib",
                "sudo postgresql-setup --initdb",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo yum install -y redis",
                "sudo systemctl start redis",
                "sudo systemctl enable redis"
            ]
        },
        {
            LinuxDistro.Arch, [
                "sudo pacman -Syu --noconfirm",
                "sudo pacman -S --noconfirm postgresql",
                "sudo -u postgres initdb -D /var/lib/postgres/data",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo pacman -S --noconfirm redis",
                "sudo systemctl start redis",
                "sudo systemctl enable redis"
            ]
        },
        {
            LinuxDistro.OpenSuse, [
                "sudo zypper refresh",
                "sudo zypper install -y postgresql postgresql-server",
                "sudo systemctl start postgresql",
                "sudo systemctl enable postgresql",
                "sudo zypper install -y redis",
                "sudo systemctl start redis",
                "sudo systemctl enable redis"
            ]
        },
        {
            LinuxDistro.Alpine, [
                "sudo apk update",
                "sudo apk add postgresql postgresql-contrib",
                "sudo mkdir -p /var/lib/postgresql/data",
                "sudo chown postgres:postgres /var/lib/postgresql/data",
                "sudo -u postgres initdb -D /var/lib/postgresql/data",
                "sudo rc-service postgresql start",
                "sudo rc-update add postgresql default",
                "sudo apk add redis",
                "sudo rc-service redis start",
                "sudo rc-update add redis default"
            ]
        }
    };

    private static readonly Dictionary<LinuxDistro, (string pgCheck, string redisCheck)> PackageChecks = new()
    {
        {
            LinuxDistro.Ubuntu, ("dpkg -l | grep postgresql", "dpkg -l | grep redis-server")
        },
        {
            LinuxDistro.Debian, ("dpkg -l | grep postgresql", "dpkg -l | grep redis-server")
        },
        {
            LinuxDistro.Fedora, ("rpm -q postgresql-server", "rpm -q redis")
        },
        {
            LinuxDistro.Rhel, ("rpm -q postgresql-server", "rpm -q redis")
        },
        {
            LinuxDistro.CentOs, ("rpm -q postgresql-server", "rpm -q redis")
        },
        {
            LinuxDistro.Arch, ("pacman -Qi postgresql", "pacman -Qi redis")
        },
        {
            LinuxDistro.OpenSuse, ("rpm -q postgresql-server", "rpm -q redis")
        },
        {
            LinuxDistro.Alpine, ("apk info postgresql", "apk info redis")
        }
    };

    /// <summary>
    ///     Detects operating system, checks for existing installations, and manages dependency setup.
    /// </summary>
    /// <remarks>
    ///     For Linux:
    ///     - Automatically detects distribution
    ///     - Installs using appropriate package manager
    ///     - Configures services and database
    ///     For Windows:
    ///     - Checks existing installations
    ///     - Provides manual installation guidance
    ///     - Assists with database setup
    ///     Throws no exceptions - all errors are logged.
    /// </remarks>
    /// <param name="psqlString">The psqlstring string.</param>
    /// <param name="isMasterInstance">Whether this is the master instance.</param>
    /// <param name="setupCompleted">Whether PostgreSQL setup has been completed before.</param>
    public static void CheckAndInstallDependencies(string psqlString, bool isMasterInstance,
        bool setupCompleted = false)
    {
        if (setupCompleted)
            return;

        if (!isMasterInstance)
        {
            Log.Information("This is not the master instance. Skipping dependency installation.");
            return;
        }

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Unix:
            {
                var distro = DetectLinuxDistro();
                if (distro != LinuxDistro.Unknown)
                {
                    var (postgresInstalled, redisInstalled) = CheckLinuxDependencies(distro);
                    if (!postgresInstalled || !redisInstalled)
                    {
                        InstallOnLinux(distro, postgresInstalled, redisInstalled);
                    }
                    else
                    {
                        Log.Information("PostgreSQL and Redis are already installed.");
                        if (!string.IsNullOrWhiteSpace(psqlString) && setupCompleted)
                        {
                            Log.Information("PSQL string is already set and setup is completed.");
                            return;
                        }

                        if (!setupCompleted && PromptForDatabaseSetup())
                        {
                            var (dbName, dbUser, dbPassword) = GetDatabaseDetails();
                            SetupPostgresDb(dbName, dbUser, dbPassword);
                            MarkSetupCompleted();
                        }
                    }
                }
                else
                {
                    Log.Error("Unable to detect Linux distribution. Please install PostgreSQL and Redis manually.");
                    ShowManualInstructions();
                }

                break;
            }
            case PlatformID.Win32NT:
            {
                var (postgresInstalled, redisInstalled) = CheckWindowsDependencies();
                if (!postgresInstalled || !redisInstalled)
                {
                    ShowWindowsInstructions(postgresInstalled, redisInstalled);
                }
                else
                {
                    Log.Information("PostgreSQL and Redis are already installed.");
                    if (!string.IsNullOrWhiteSpace(psqlString) && setupCompleted)
                    {
                        Log.Information("PSQL string is already set and setup is completed.");
                        return;
                    }

                    if (!setupCompleted && PromptForDatabaseSetup())
                    {
                        var (dbName, dbUser, dbPassword) = GetDatabaseDetails();
                        ShowWindowsDatabaseSetup(dbName, dbUser, dbPassword);
                        MarkSetupCompleted();
                    }
                }

                break;
            }
            default:
                Log.Error("Unsupported operating system.");
                ShowManualInstructions();
                break;
        }
    }

    private static LinuxDistro DetectLinuxDistro()
    {
        try
        {
            if (!File.Exists("/etc/os-release"))
                return LinuxDistro.Unknown;

            var osRelease = File.ReadAllText("/etc/os-release").ToLower();

            if (osRelease.Contains("ubuntu")) return LinuxDistro.Ubuntu;
            if (osRelease.Contains("debian")) return LinuxDistro.Debian;
            if (osRelease.Contains("fedora")) return LinuxDistro.Fedora;
            if (osRelease.Contains("rhel")) return LinuxDistro.Rhel;
            if (osRelease.Contains("centos")) return LinuxDistro.CentOs;
            if (osRelease.Contains("arch")) return LinuxDistro.Arch;
            if (osRelease.Contains("opensuse")) return LinuxDistro.OpenSuse;
            return File.Exists("/etc/alpine-release") ? LinuxDistro.Alpine : LinuxDistro.Unknown;
        }
        catch (Exception ex)
        {
            Log.Error($"Error detecting Linux distribution: {ex.Message}");
            return LinuxDistro.Unknown;
        }
    }

    private static (bool postgresInstalled, bool redisInstalled) CheckLinuxDependencies(LinuxDistro distro)
    {
        var (pgCheck, redisCheck) = PackageChecks[distro];

        var postgresInstalled = CheckPackage(pgCheck) && CheckService("postgresql");
        var redisInstalled = CheckPackage(redisCheck) && CheckService("redis");

        Log.Information($"PostgreSQL installed and running: {postgresInstalled}");
        Log.Information($"Redis installed and running: {redisInstalled}");

        return (postgresInstalled, redisInstalled);

        bool CheckPackage(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        bool CheckService(string serviceName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = distro == LinuxDistro.Alpine
                            ? $"-c \"rc-service {serviceName} status\""
                            : $"-c \"systemctl is-active {serviceName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return distro == LinuxDistro.Alpine
                    ? output.Contains("started")
                    : output == "active";
            }
            catch
            {
                return false;
            }
        }
    }

    private static (bool postgresInstalled, bool redisInstalled) CheckWindowsDependencies()
    {
        bool CheckService(string serviceName)
        {
            try
            {
#pragma warning disable CA1416
                var service = ServiceController.GetServices()
                    .FirstOrDefault(s => s.ServiceName.ToLower().Contains(serviceName));
                return service is { Status: ServiceControllerStatus.Running };
#pragma warning restore CA1416
            }
            catch
            {
                return false;
            }
        }

        var postgresInstalled = CheckService("postgresql");
        var redisInstalled = CheckService("redis");

        Log.Information($"PostgreSQL installed and running: {postgresInstalled}");
        Log.Information($"Redis installed and running: {redisInstalled}");

        return (postgresInstalled, redisInstalled);
    }

    private static void InstallOnLinux(LinuxDistro distro, bool postgresInstalled, bool redisInstalled)
    {
        var commands = InstallCommands[distro].ToList();

        if (postgresInstalled)
        {
            commands.RemoveAll(cmd => cmd.Contains("postgresql"));
            Log.Information("PostgreSQL is already installed, skipping installation steps.");
        }

        if (redisInstalled)
        {
            commands.RemoveAll(cmd => cmd.Contains("redis"));
            Log.Information("Redis is already installed, skipping installation steps.");
        }

        foreach (var command in commands)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error($"Error executing {command}: {error}");
                    return;
                }

                Log.Information($"Successfully executed: {command}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute {command}: {ex.Message}");
                return;
            }
        }

        if (!postgresInstalled || !redisInstalled)
        {
            Log.Information("Dependencies installed successfully!");
        }

        if (PromptForDatabaseSetup())
        {
            var (dbName, dbUser, dbPassword) = GetDatabaseDetails();
            SetupPostgresDb(dbName, dbUser, dbPassword);
            MarkSetupCompleted();
        }
    }

    private static bool PromptForDatabaseSetup()
    {
        Log.Information("Would you like to set up a PostgreSQL database? (y/n) [timeout: 10s]");

        var task = Task.Run(() => Console.ReadLine()?.ToLower());
        if (task.Wait(TimeSpan.FromSeconds(10)))
        {
            var response = task.Result;
            return response is "y" or "yes";
        }

        Log.Information("No response received within 10 seconds. Skipping database setup.");
        return false;
    }

    private static (string dbName, string dbUser, string dbPassword) GetDatabaseDetails()
    {
        Log.Information("Enter database name (default: mewdeko):");
        var dbName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(dbName)) dbName = "mewdeko";

        Log.Information("Enter database user (default: mewdeko):");
        var dbUser = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(dbUser)) dbUser = "mewdeko";

        Log.Information("Enter database password:");
        var dbPassword = string.Empty;
        while (string.IsNullOrWhiteSpace(dbPassword))
        {
            dbPassword = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(dbPassword))
            {
                Log.Warning("Password cannot be empty. Please enter a valid password:");
            }
        }

        return (dbName, dbUser, dbPassword);
    }

    private static void SetupPostgresDb(string dbName, string dbUser, string dbPassword)
    {
        var commands = new[]
        {
            $"sudo -u postgres psql -c \"CREATE USER {dbUser} WITH PASSWORD '{dbPassword}';\"",
            $"sudo -u postgres psql -c \"CREATE DATABASE {dbName} WITH OWNER {dbUser};\"",
            $"sudo -u postgres psql -c \"ALTER USER {dbUser} WITH CREATEDB;\"",
            $"sudo -u postgres psql -d {dbName} -c \"GRANT ALL ON SCHEMA public TO {dbUser};\"",
            $"sudo sh -c 'echo \"host    {dbName}    {dbUser}    127.0.0.1/32    md5\" >> /etc/postgresql/*/main/pg_hba.conf'",
            "sudo systemctl restart postgresql"
        };

        foreach (var command in commands)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Log.Error($"Error executing {command}: {error}");
                    return;
                }

                Log.Information($"Successfully executed: {command}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to execute {command}: {ex.Message}");
                return;
            }
        }

        Log.Information($"Database {dbName} created successfully with user {dbUser}");
        Log.Information(
            $"Your connection string will be: \"Host=localhost;Database={dbName};Username={dbUser};Password={dbPassword}\"");
    }

    private static void ShowWindowsInstructions(bool postgresInstalled = false, bool redisInstalled = false)
    {
        if (!postgresInstalled)
        {
            Log.Information("""
                            PostgreSQL is not installed. Please install:
                            1. Download from: https://www.postgresql.org/download/windows/
                            2. Run installer and follow setup wizard
                            3. Note down postgres user password
                            4. Verify by opening Command Prompt and typing: psql -V
                            """);
        }

        if (!redisInstalled)
        {
            Log.Information("""
                            Redis is not installed. Please install:
                            1. Download from: https://github.com/microsoftarchive/redis/releases
                            2. Run the .msi installer
                            3. Service should start automatically
                            4. Verify by opening Command Prompt and typing: redis-cli ping
                            """);
        }

        if (!postgresInstalled || !redisInstalled)
        {
            Log.Information("""
                            After installing, ensure services are running:
                            1. Open Services (services.msc)
                            2. Look for 'postgresql' and 'redis' services
                            3. Set both to 'Automatic' startup
                            4. Ensure both are 'Running'
                            """);
        }

        if (PromptForDatabaseSetup())
        {
            ShowWindowsDatabaseSetup();
            MarkSetupCompleted();
        }
    }

    private static void ShowWindowsDatabaseSetup(string? dbName = null, string? dbUser = null,
        string? dbPassword = null)
    {
        if (dbName == null || dbUser == null || dbPassword == null)
        {
            (dbName, dbUser, dbPassword) = GetDatabaseDetails();
        }

        Log.Information($"""
                         To create your database:
                         1. Open Command Prompt as Administrator
                         2. Type: psql -U postgres
                         3. Enter postgres password
                         4. Run these commands:

                             CREATE USER {dbUser} WITH PASSWORD '{dbPassword}';
                             CREATE DATABASE {dbName} OWNER {dbUser};
                             GRANT ALL PRIVILEGES ON DATABASE {dbName} TO {dbUser};
                             \c {dbName}
                             GRANT ALL ON SCHEMA public TO {dbUser};

                         5. Type \q to exit

                         Connection string: Host=localhost;Database={dbName};Username={dbUser};Password={dbPassword}
                         """);
    }

    private static void ShowManualInstructions()
    {
        Log.Information("""
                        Please install PostgreSQL and Redis manually:

                        1. PostgreSQL:
                           - Visit: https://www.postgresql.org/download/
                           - Follow installation instructions for your system
                           - Start and enable PostgreSQL service

                        2. Redis:
                           - Visit: https://redis.io/download
                           - Follow installation instructions for your system
                           - Start and enable Redis service

                        After installation, verify:
                        1. PostgreSQL is running: sudo systemctl status postgresql
                        2. Redis is running: sudo systemctl status redis
                        """);
    }

    /// <summary>
    ///     Marks the PostgreSQL setup as completed in the credentials file.
    /// </summary>
    private static void MarkSetupCompleted()
    {
        try
        {
            var credsFileName = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");
            if (!File.Exists(credsFileName))
            {
                Log.Warning("credentials.json not found, cannot mark setup as completed");
                return;
            }

            var json = File.ReadAllText(credsFileName);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var options = new JsonWriterOptions
            {
                Indented = true
            };
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, options);

            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "PostgresSetupCompleted")
                {
                    writer.WriteBoolean(property.Name, true);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            // Add the property if it doesn't exist
            if (!root.TryGetProperty("PostgresSetupCompleted", out _))
            {
                writer.WriteBoolean("PostgresSetupCompleted", true);
            }

            writer.WriteEndObject();
            writer.Flush();

            var updatedJson = Encoding.UTF8.GetString(stream.ToArray());
            File.WriteAllText(credsFileName, updatedJson);
            Log.Information("Marked PostgreSQL setup as completed in credentials.json");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to mark setup as completed in credentials.json");
        }
    }

    private enum LinuxDistro
    {
        Ubuntu,
        Debian,
        Fedora,
        Rhel,
        CentOs,
        Arch,
        OpenSuse,
        Alpine,
        Unknown
    }
}