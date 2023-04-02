/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

[System.AttributeUsage(System.AttributeTargets.Class)]
public class ReportHandlerAttribute : System.Attribute
{
    public string Name;

    public ReportHandlerAttribute(string name)
        => this.Name = name;
}
