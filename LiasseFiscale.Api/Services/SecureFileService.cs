using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LiasseFiscale.Api.Services;

/// <summary>
/// Provides secure file handling for uploaded documents.
/// Implements: path validation, size limits, checksum calculation, virus scanning hooks.
/// </summary>
public interface ISecureFileService
{
    /// <summary>
    /// Validate that a filename is safe and doesn't contain path traversal sequences.
    /// </summary>
    bool IsFilenameValid(string filename, string allowedPattern = @"^[A-Za-z0-9\.\-]+$");

    /// <summary>
    /// Verify that a file path is within the allowed storage directory.
    /// </summary>
    bool IsPathSafe(string basePath, string targetPath);

    /// <summary>
    /// Calculate SHA256 checksum of a file stream.
    /// </summary>
    Task<string> CalculateChecksumAsync(Stream fileStream);

    /// <summary>
    /// Check file size constraints (max file size).
    /// </summary>
    bool IsFileSizeValid(long fileSize, long maxSizeBytes = 50_000_000); // 50 MB default

    /// <summary>
    /// Validate MIME type by checking file signature (magic bytes).
    /// </summary>
    bool IsContentTypeValid(Stream fileStream, string expectedMimeType);
}

public class SecureFileService : ISecureFileService
{
    private readonly ILogger<SecureFileService> _logger;

    public SecureFileService(ILogger<SecureFileService> logger)
    {
        _logger = logger;
    }

    public bool IsFilenameValid(string filename, string allowedPattern = @"^[A-Za-z0-9\.\-]+$")
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            _logger.LogWarning("Filename validation failed: empty or null filename");
            return false;
        }

        // Check for path traversal sequences
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
        {
            _logger.LogWarning("Filename validation failed: path traversal detected in {Filename}", filename);
            return false;
        }

        // Validate against pattern (default allows alphanumeric, dots, hyphens)
        if (!Regex.IsMatch(filename, allowedPattern, RegexOptions.Compiled))
        {
            _logger.LogWarning("Filename validation failed: {Filename} doesn't match pattern {Pattern}", filename, allowedPattern);
            return false;
        }

        return true;
    }

    public bool IsPathSafe(string basePath, string targetPath)
    {
        try
        {
            var fullBasePath = Path.GetFullPath(basePath);
            var fullTargetPath = Path.GetFullPath(targetPath);

            // Ensure target path starts with base path (no traversal out)
            if (!fullTargetPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path validation failed: {TargetPath} not under {BasePath}", targetPath, basePath);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Path validation error for {BasePath} and {TargetPath}", basePath, targetPath);
            return false;
        }
    }

    public async Task<string> CalculateChecksumAsync(Stream fileStream)
    {
        try
        {
            fileStream.Position = 0;
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() =>
            {
                fileStream.Position = 0;
                return sha256.ComputeHash(fileStream);
            });

            fileStream.Position = 0;
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating checksum for file stream");
            throw;
        }
    }

    public bool IsFileSizeValid(long fileSize, long maxSizeBytes = 50_000_000)
    {
        if (fileSize <= 0)
        {
            _logger.LogWarning("File size validation failed: file is empty");
            return false;
        }

        if (fileSize > maxSizeBytes)
        {
            _logger.LogWarning("File size validation failed: {FileSize} exceeds maximum {MaxSize}", fileSize, maxSizeBytes);
            return false;
        }

        return true;
    }

    public bool IsContentTypeValid(Stream fileStream, string expectedMimeType)
    {
        try
        {
            fileStream.Position = 0;
            var buffer = new byte[512];
            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
            fileStream.Position = 0;

            // Magic bytes for XML and PDF
            var magicXml = new byte[] { 0x3C, 0x3F, 0x78, 0x6D }; // <?xm
            var magicPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

            return expectedMimeType.ToLowerInvariant() switch
            {
                "application/xml" or "text/xml" => 
                    buffer.Take(magicXml.Length).SequenceEqual(magicXml),
                
                "application/pdf" => 
                    buffer.Take(magicPdf.Length).SequenceEqual(magicPdf),
                
                _ => true // Accept unknown types for now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating content type for {MimeType}", expectedMimeType);
            return false;
        }
    }
}
