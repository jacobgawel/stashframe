namespace JSG.API.Stashframe.Core.Constants;

public class MediaLimits
{
    // free tier
    public const long FreeVideoMaxBytes = 500L ; // 500MB
    public const long FreeScreenshotMaxBytes = 50L * 1024 * 1024; // 50MB
    public const int FreeMaxUploadsPerDay = 20;

    // pro tier
    public const long ProVideoMaxBytes = 2L * 1024 * 1024 * 1024;  // 2 GB
    public const long ProScreenshotMaxBytes = 100L * 1024 * 1024;   // 100 MB
    public const int ProMaxUploadsPerDay = 100;
}
