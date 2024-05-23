using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsEnum]
internal enum TypeEnum
{
    None,
    String,
    Bool,
    Float,
    Matrix34,
    Uint64,
    Int32,
    Binary,
    Array,
    Vector3
}