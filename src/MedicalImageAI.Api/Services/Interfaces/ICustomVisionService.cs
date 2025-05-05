using MedicalImageAI.Api.Models;

namespace MedicalImageAI.Api.Services;

public interface ICustomVisionService
{
    Task<AnalysisResult> AnalyzeImageAsync(string imageUrl);
}
