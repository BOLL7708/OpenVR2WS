using TypeGen.Core.SpecGeneration;

namespace OpenVR2WS;

public class TypeGenSpec : GenerationSpec
{
    public override void OnBeforeBarrelGeneration(OnBeforeBarrelGenerationArgs args)
    {
        AddBarrel(".");
    }
}