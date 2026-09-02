using LibGit2Sharp;
using Sparse_Smart_code_reviewer.Data;
using Sparse_Smart_code_reviewer.DTOs.Code;
using Sparse_Smart_code_reviewer.Models;
using Sparse_Smart_code_reviewer.Services.interfaces;
using System.IO.Compression;
using System.Net;

namespace Sparse_Smart_code_reviewer.Services.services
{
    public class CodeService : ICodeService
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CodeService> _logger;

        // Supported file extensions and their corresponding languages.
        // OrdinalIgnoreCase makes ".CS" and ".cs" behave the same.
        private static readonly Dictionary<string, string> ExtensionLanguageMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { ".cs", "C#" },
                { ".js", "JavaScript" },
                { ".ts", "TypeScript" },
                { ".py", "Python" },
                { ".java", "Java" },
                { ".php", "PHP" },
                { ".rb", "Ruby" },
                { ".go", "Go" },
                { ".cpp", "C++" },
                { ".c", "C" },
                { ".html", "HTML" },
                { ".css", "CSS" },
                { ".json", "JSON" },
                { ".xml", "XML" }
            };

        // Folders that should never be analyzed.
        // These folders usually contain dependencies, build artifacts,
        // IDE metadata, or Git internal files.
        private static readonly HashSet<string> IgnoredFolders =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".git",
                "node_modules",
                "bin",
                "obj",
                ".vs",
                ".idea"
            };

        // Maximum size of ONE source file.
        private const long MaxFileSizeBytes = 10_000_000; // 10 MB

        // Maximum number of files that can be processed from one source.
        private const int MaxFileCount = 5_000;

        // Maximum total extracted size from a ZIP/repository.
        // This protects against decompression bombs.
        private const long MaxTotalExtractedBytes = 100_000_000; // 100 MB

        // Maximum size of an uploaded ZIP file itself.
        private const long MaxZipFileSizeBytes = 50_000_000; // 50 MB

        // Maximum repository download size.
        private const long MaxRepositoryDownloadBytes = 100_000_000; // 100 MB


        public CodeService(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<CodeService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }


        // ============================================================
        // PASTE CODE
        // ============================================================

        public async Task<Code> CreateAsync(
            CreateCodeDTO dto,
            int userId,
            CancellationToken cancellationToken = default)
        {
            // DTO validation should already be handled by ASP.NET Core
            // through Data Annotation attributes.
            //
            // This method should therefore focus on business logic.

            var code = new Code
            {
                Content = dto.Content,
                Language = dto.Language,
                FileName = dto.FileName,
                UserId = userId
            };

            _context.Codes.Add(code);

            await _context.SaveChangesAsync(cancellationToken);

            return code;
        }


        // ============================================================
        // UPLOAD SINGLE FILE
        // ============================================================

        public async Task<Code> CreateFromFileAsync(
            IFormFile file,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty or null.");

            // Security:
            // Never read a file before checking its size.
            //
            // Otherwise an attacker could upload a very large file
            // and force the application to consume a lot of memory.
            if (file.Length > MaxFileSizeBytes)
                throw new ArgumentException(
                    $"File cannot exceed {MaxFileSizeBytes / 1_000_000} MB.");


            // Only allow supported source-code extensions.
            var extension = Path.GetExtension(file.FileName);

            if (!ExtensionLanguageMap.ContainsKey(extension))
                throw new ArgumentException(
                    "Unsupported file type.");


            // FileName supplied by the user should not be treated as a path.
            // GetFileName removes directory information such as:
            //
            // ../../secret/file.cs
            //
            // and keeps only:
            //
            // file.cs
            var safeFileName = Path.GetFileName(file.FileName);


            // StreamReader is used instead of loading the raw stream
            // manually.
            using var reader = new StreamReader(
                file.OpenReadStream());

            var content = await reader.ReadToEndAsync(
                cancellationToken);


            var dto = new CreateCodeDTO
            {
                Content = content,
                Language = DetectLanguage(safeFileName),
                FileName = safeFileName
            };


            return await CreateAsync(
                dto,
                userId,
                cancellationToken);
        }


        // ============================================================
        // UPLOAD ZIP
        // ============================================================

        public async Task<List<Code>> CreateFromZipAsync(
            IFormFile file,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("ZIP file is empty or null.");


            // IMPORTANT:
            // MaxFileSizeBytes applies to individual source files.
            //
            // A ZIP file needs its own limit because a small ZIP can
            // decompress into a huge amount of data.
            if (file.Length > MaxZipFileSizeBytes)
                throw new ArgumentException(
                    $"ZIP file cannot exceed {MaxZipFileSizeBytes / 1_000_000} MB.");


            var tempFolder = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString());


            Directory.CreateDirectory(tempFolder);


            try
            {
                var zipPath = Path.Combine(
                    tempFolder,
                    "upload.zip");


                // Do not trust the original file name when creating
                // a server-side file path.
                //
                // We use a fixed safe name instead.
                await using (var stream = new FileStream(
                    zipPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true))
                {
                    await file.CopyToAsync(
                        stream,
                        cancellationToken);
                }


                var extractFolder = Path.Combine(
                    tempFolder,
                    "extracted");

                Directory.CreateDirectory(extractFolder);


                // We intentionally do NOT use:
                //
                // ZipFile.ExtractToDirectory(...)
                //
                // because we want to inspect every ZIP entry first.
                await ExtractZipSafelyAsync(
                    zipPath,
                    extractFolder,
                    cancellationToken);


                return await ProcessDirectoryAsync(
                    extractFolder,
                    userId,
                    cancellationToken);
            }
            finally
            {
                // Temporary files must always be deleted,
                // even if extraction or database processing fails.
                SafeDeleteDirectory(tempFolder);
            }
        }


        // ============================================================
        // GIT REPOSITORY
        // ============================================================

        public async Task<List<Code>> CreateFromGitAsync(
            string repositoryUrl,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
                throw new ArgumentException(
                    "Repository URL is required.");


            // Validate the URL before allowing the server
            // to connect to an external resource.
            ValidateRepositoryUrl(repositoryUrl);


            var tempFolder = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString());


            try
            {
                _logger.LogInformation(
                    "Cloning repository for user {UserId}",
                    userId);


                // LibGit2Sharp exposes a synchronous Clone API.
                //
                // Task.Run prevents the synchronous operation from
                // directly blocking the ASP.NET request thread.
                //
                // For a large production system, this work should
                // eventually move to a background job/worker.
                await Task.Run(
                    () =>
                    {
                        Repository.Clone(
                            repositoryUrl,
                            tempFolder);
                    },
                    cancellationToken);


                return await ProcessDirectoryAsync(
                    tempFolder,
                    userId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process Git repository for user {UserId}",
                    userId);

                throw;
            }
            finally
            {
                SafeDeleteDirectory(tempFolder);
            }
        }


        // ============================================================
        // GITHUB REPOSITORY
        // ============================================================

        public async Task<List<Code>> CreateFromGitHubAsync(
            string repositoryUrl,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var (owner, repo) = ParseGitHubUrl(repositoryUrl);


            var client = _httpClientFactory.CreateClient();

            // GitHub requires a User-Agent header.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Sparse-Smart-Code-Reviewer");


            var zipUrl =
                $"https://api.github.com/repos/{owner}/{repo}/zipball";


            _logger.LogInformation(
                "Downloading GitHub repository {Owner}/{Repo}",
                owner,
                repo);


            // ResponseHeadersRead allows us to start reading the
            // response stream without buffering the entire response
            // into memory.
            using var response = await client.GetAsync(
                zipUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);


            response.EnsureSuccessStatusCode();


            // If GitHub provides Content-Length, reject the response
            // before downloading a huge file.
            if (response.Content.Headers.ContentLength
                is long contentLength
                && contentLength > MaxRepositoryDownloadBytes)
            {
                throw new ArgumentException(
                    "GitHub repository is too large.");
            }


            var tempFolder = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString());


            Directory.CreateDirectory(tempFolder);


            try
            {
                var zipPath = Path.Combine(
                    tempFolder,
                    "repo.zip");


                await DownloadWithSizeLimitAsync(
                    response.Content,
                    zipPath,
                    MaxRepositoryDownloadBytes,
                    cancellationToken);


                var extractFolder = Path.Combine(
                    tempFolder,
                    "extracted");

                Directory.CreateDirectory(extractFolder);


                await ExtractZipSafelyAsync(
                    zipPath,
                    extractFolder,
                    cancellationToken);


                return await ProcessDirectoryAsync(
                    extractFolder,
                    userId,
                    cancellationToken);
            }
            finally
            {
                SafeDeleteDirectory(tempFolder);
            }
        }


        // ============================================================
        // PROCESS DIRECTORY
        // ============================================================

        private async Task<List<Code>> ProcessDirectoryAsync(
            string rootFolder,
            int userId,
            CancellationToken cancellationToken)
        {
            var results = new List<Code>();


            // EnumerateFiles is better than GetFiles for large
            // directories because it does not create an array
            // containing every file before processing starts.
            var files = Directory
                .EnumerateFiles(
                    rootFolder,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path => !IsIgnored(path))
                .Where(path =>
                    ExtensionLanguageMap.ContainsKey(
                        Path.GetExtension(path)))
                .Take(MaxFileCount + 1);


            var processedFileCount = 0;


            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();


                processedFileCount++;


                // Protect the application from repositories containing
                // thousands/millions of source files.
                if (processedFileCount > MaxFileCount)
                {
                    throw new ArgumentException(
                        $"Repository cannot contain more than {MaxFileCount} supported files.");
                }


                var info = new FileInfo(filePath);


                // Never read a file before checking its size.
                if (info.Length > MaxFileSizeBytes)
                {
                    _logger.LogWarning(
                        "Skipping large file {FilePath}",
                        filePath);

                    continue;
                }


                string content;

                try
                {
                    content = await File.ReadAllTextAsync(
                        filePath,
                        cancellationToken);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not read file {FilePath}",
                        filePath);

                    continue;
                }


                // Store the path relative to the repository root.
                //
                // Example:
                //
                // Controllers/UserController.cs
                //
                // instead of:
                //
                // C:\Temp\1234\extracted\Controllers\UserController.cs
                var relativePath =
                    Path.GetRelativePath(
                        rootFolder,
                        filePath)
                    .Replace('\\', '/');


                var code = new Code
                {
                    Content = content,
                    Language = DetectLanguage(filePath),
                    FileName = relativePath,
                    UserId = userId
                };


                _context.Codes.Add(code);
                results.Add(code);


                // Batch database writes.
                //
                // Keeping thousands of entities tracked by one DbContext
                // can consume a lot of memory.
                //
                // Every 100 files we save the current batch.
                if (results.Count % 100 == 0)
                {
                    await _context.SaveChangesAsync(
                        cancellationToken);

                    // Detach tracked entities after saving.
                    //
                    // This reduces EF Core Change Tracker memory usage
                    // when processing large repositories.
                    foreach (var entity in results)
                    {
                        _context.Entry(entity).State =
                            Microsoft.EntityFrameworkCore.EntityState.Detached;
                    }
                }
            }


            // Save remaining entities that did not complete a batch.
            if (results.Count % 100 != 0)
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }


            return results;
        }


        // ============================================================
        // SAFE ZIP EXTRACTION
        // ============================================================

        private async Task ExtractZipSafelyAsync(
            string zipPath,
            string destinationFolder,
            CancellationToken cancellationToken)
        {
            using var archive = ZipFile.OpenRead(zipPath);


            var extractedBytes = 0L;
            var extractedFileCount = 0;


            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();


                // Skip directories.
                if (string.IsNullOrEmpty(entry.Name))
                    continue;


                extractedFileCount++;


                if (extractedFileCount > MaxFileCount)
                {
                    throw new ArgumentException(
                        $"ZIP cannot contain more than {MaxFileCount} files.");
                }


                // Prevent decompression bombs.
                //
                // entry.Length represents the uncompressed size.
                if (entry.Length > MaxFileSizeBytes)
                {
                    // We can skip unsupported/large files instead of
                    // extracting them.
                    continue;
                }


                extractedBytes += entry.Length;


                if (extractedBytes > MaxTotalExtractedBytes)
                {
                    throw new ArgumentException(
                        "ZIP extracted content exceeds the allowed limit.");
                }


                // Convert the entry path to a normalized full path.
                var fullPath = Path.GetFullPath(
                    Path.Combine(
                        destinationFolder,
                        entry.FullName));


                var fullDestinationPath =
                    Path.GetFullPath(destinationFolder)
                    + Path.DirectorySeparatorChar;


                // IMPORTANT SECURITY CHECK:
                //
                // Prevent ZIP Path Traversal.
                //
                // Malicious ZIP:
                //
                // ../../../../some-file
                //
                // must never escape destinationFolder.
                if (!fullPath.StartsWith(
                        fullDestinationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "ZIP contains an invalid file path.");
                }


                // We only extract supported source files.
                var extension =
                    Path.GetExtension(entry.Name);


                if (!ExtensionLanguageMap.ContainsKey(extension))
                    continue;


                var directory =
                    Path.GetDirectoryName(fullPath);


                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }


                await using var input =
                    entry.Open();


                await using var output =
                    new FileStream(
                        fullPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        useAsync: true);


                await input.CopyToAsync(
                    output,
                    cancellationToken);
            }
        }


        // ============================================================
        // DOWNLOAD WITH SIZE LIMIT
        // ============================================================

        private static async Task DownloadWithSizeLimitAsync(
            HttpContent content,
            string destinationPath,
            long maxBytes,
            CancellationToken cancellationToken)
        {
            await using var input =
                await content.ReadAsStreamAsync(
                    cancellationToken);


            await using var output =
                new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true);


            var buffer = new byte[64 * 1024];

            long totalBytes = 0;

            int bytesRead;


            while ((bytesRead =
                await input.ReadAsync(
                    buffer,
                    cancellationToken)) > 0)
            {
                totalBytes += bytesRead;


                // Content-Length is not always available.
                //
                // Therefore we ALSO check the actual number of
                // downloaded bytes while streaming.
                if (totalBytes > maxBytes)
                {
                    throw new ArgumentException(
                        "Downloaded repository is too large.");
                }


                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
            }
        }


        // ============================================================
        // IGNORE FOLDERS
        // ============================================================

        private static bool IsIgnored(string path)
        {
            return path
                .Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                .Any(segment =>
                    IgnoredFolders.Contains(segment));
        }


        // ============================================================
        // LANGUAGE DETECTION
        // ============================================================

        private static string DetectLanguage(string fileName)
        {
            var extension =
                Path.GetExtension(fileName);


            return ExtensionLanguageMap.TryGetValue(
                extension,
                out var language)
                    ? language
                    : "Unknown";
        }


        // ============================================================
        // GITHUB URL PARSER
        // ============================================================

        private static (string owner, string repo)
            ParseGitHubUrl(string url)
        {
            if (!Uri.TryCreate(
                    url,
                    UriKind.Absolute,
                    out var uri))
            {
                throw new ArgumentException(
                    "Invalid GitHub URL.");
            }


            // Only HTTPS GitHub URLs are allowed.
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "Only HTTPS GitHub URLs are allowed.");
            }


            // Make sure the URL actually belongs to GitHub.
            if (!string.Equals(
                    uri.Host,
                    "github.com",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "URL must belong to github.com.");
            }


            var segments =
                uri.AbsolutePath
                    .Trim('/')
                    .Split(
                        '/',
                        StringSplitOptions.RemoveEmptyEntries);


            if (segments.Length < 2)
            {
                throw new ArgumentException(
                    "Invalid GitHub repository URL.");
            }


            var owner = segments[0];

            var repo = segments[1];


            if (repo.EndsWith(
                    ".git",
                    StringComparison.OrdinalIgnoreCase))
            {
                repo = repo[..^4];
            }


            if (string.IsNullOrWhiteSpace(owner)
                || string.IsNullOrWhiteSpace(repo))
            {
                throw new ArgumentException(
                    "Invalid GitHub repository.");
            }


            return (owner, repo);
        }


        // ============================================================
        // GIT URL VALIDATION
        // ============================================================

        private static void ValidateRepositoryUrl(
            string repositoryUrl)
        {
            if (!Uri.TryCreate(
                    repositoryUrl,
                    UriKind.Absolute,
                    out var uri))
            {
                throw new ArgumentException(
                    "Invalid repository URL.");
            }


            // Only HTTPS URLs are allowed.
            //
            // Avoid accepting protocols such as:
            //
            // file://
            // ftp://
            // ssh://
            // etc.
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "Only HTTPS repository URLs are allowed.");
            }


            // Basic SSRF protection.
            //
            // Do not allow requests to localhost or loopback addresses.
            if (string.Equals(
                    uri.Host,
                    "localhost",
                    StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(
                    uri.Host,
                    out var ip)
                && IPAddress.IsLoopback(ip))
            {
                throw new ArgumentException(
                    "Local repository URLs are not allowed.");
            }
        }


        // ============================================================
        // TEMP DIRECTORY CLEANUP
        // ============================================================

        private void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(
                        path,
                        recursive: true);
                }
            }
            catch (Exception ex)
            {
                // Never silently swallow cleanup failures.
                //
                // Logging helps us investigate disk-space problems
                // and leftover temporary files.
                _logger.LogError(
                    ex,
                    "Failed to delete temporary directory {Path}",
                    path);
            }
        }
    }
}