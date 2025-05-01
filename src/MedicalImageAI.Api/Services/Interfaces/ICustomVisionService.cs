using MedicalImageAI.Api.Models;
using System.Threading.Tasks;

namespace MedicalImageAI.Api.Services;

public interface ICustomVisionService
{
    Task<AnalysisResult> AnalyzeImageAsync(string imageUrl);
}
