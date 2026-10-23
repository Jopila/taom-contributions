using System;
using TAOM.Core.Logging;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// Runs the notice's window and message loop on the calling STA thread until stopRequested or the 30-minute cap.
/// WinForms, so TAOM.Client's (ADR-013).
/// </summary>
public interface IShaderCompileNoticeWindowHost
{
    void Run(ShaderCompileNoticeTexts texts, IModLogger logger, Func<bool> stopRequested);
}
