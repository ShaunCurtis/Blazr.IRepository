/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

using System.Reflection;

namespace Blazr.Core;

public class ReportServiceSettings
{
    public IEnumerable<Assembly> ReportAssemblies { get; set; } = Enumerable.Empty<Assembly>();
}
