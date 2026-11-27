# Editor-only Open XML SDK dependencies

Installed from official NuGet packages, pinned for reproducible battle workbook import/export.

| Assembly | NuGet version | Selected target | License |
| --- | --- | --- | --- |
| DocumentFormat.OpenXml | 3.5.1 | netstandard2.0 | MIT; OpenXml.LICENSE.txt |
| DocumentFormat.OpenXml.Framework | 3.5.1 | netstandard2.0 | MIT; OpenXml.LICENSE.txt |
| System.IO.Packaging | 8.0.1 | netstandard2.0 | MIT; System.IO.Packaging.LICENSE.txt |

Sources:

- https://api.nuget.org/v3-flatcontainer/documentformat.openxml/3.5.1/documentformat.openxml.3.5.1.nupkg
- https://api.nuget.org/v3-flatcontainer/documentformat.openxml.framework/3.5.1/documentformat.openxml.framework.3.5.1.nupkg
- https://api.nuget.org/v3-flatcontainer/system.io.packaging/8.0.1/system.io.packaging.8.0.1.nupkg
- Open XML license: https://github.com/dotnet/Open-XML-SDK/blob/v3.5.1/LICENSE

DLL SHA-256 (Managed/):

```
DocumentFormat.OpenXml.dll           af82d277ef66cba76a4cb7fe9fb6d52abcbed2d7e85fea8fb000945f7faa977b
DocumentFormat.OpenXml.Framework.dll 3a0471bee5b2f2fc428d303d0f36a022d4b17fd014394023ffc68e2894434d6c
System.IO.Packaging.dll              b3ad6781c5afd6a034d216799908d0948682ed0d2d7ab74d3bdd3110bac9d7f8
```

System.IO.Packaging declares System.Memory >= 4.5.5 for netstandard2.0. Unity 6000.3.23f1 supplies the compatible System.Memory facade/runtime implementation; no duplicate System.Memory DLL is added to Assets. The importer and tests execute on Unity's editor Mono runtime, not the installed desktop .NET runtime.

All DLL PluginImporter settings disable Any platform and enable Editor only. Code remains in Assets/Editor. No player runtime references to these dependencies are introduced. Keep the .meta files alongside DLLs.
