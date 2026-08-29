namespace AutomationStudio.Vision.OCR;

/// <summary>
/// Whether OCR is actually usable here, cheaply and without loading anything (section 39: the core
/// engine must stay lightweight, no continuous/expensive AI inference — including at startup just to
/// answer a capability question).
/// </summary>
public static class OcrCapabilityCheck
{
    private static readonly string[] RequiredPrimaryModelFiles =
    [
        "models/v5/ch_PP-OCRv5_mobile_det.onnx",
        "models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
        "models/v5/latin_PP-OCRv5_rec_mobile_infer.onnx",
        "models/v5/ppocrv5_latin_dict.txt"
    ];

    /// <summary>File-existence check only — doesn't construct an ONNX session. RapidOcrNet's NuGet build target copies these next to the app automatically; true here means <see cref="RapidOcrEngine"/> will actually load on first use.</summary>
    public static bool ArePrimaryModelsPresent() =>
        RequiredPrimaryModelFiles.All(relative => File.Exists(Path.Combine(AppContext.BaseDirectory, relative)));

    /// <summary>Available if either the bundled PaddleOCR models are present or the Tesseract CLI fallback is installed.</summary>
    public static bool IsAvailable() => ArePrimaryModelsPresent() || TesseractOcrEngine.IsAvailable();
}
