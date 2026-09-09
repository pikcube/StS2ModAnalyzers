using System.Collections.Generic;

namespace ModAnalyzers;

internal record LocAliasInfo(string BasePath, params List<string> AliasPaths)
{
    public string BasePath { get; } = BasePath;
    public List<string> AliasPaths { get; } = AliasPaths;
}