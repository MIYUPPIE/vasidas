using OpenCvSharp;

namespace AutomationStudio.Vision.TemplateMatching;

/// <summary>
/// Whether OpenCvSharp4's native library actually loaded — cheap and real, not just a file-existence
/// check, since the exact failure mode this project already hit once (Sdcb.PaddleSharp's stale
/// OpenCvSharp4 Linux runtime, see the OCR comment in AutomationStudio.Vision.csproj) was a
/// DllNotFoundException at first use, not a missing-file error at startup.
/// </summary>
public static class TemplateMatchingCapabilityCheck
{
    public static bool IsAvailable()
    {
        try
        {
            _ = Cv2.GetVersionString();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }
}
