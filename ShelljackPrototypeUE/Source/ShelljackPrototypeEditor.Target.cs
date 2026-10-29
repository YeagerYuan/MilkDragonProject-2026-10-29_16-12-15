using UnrealBuildTool;
public class ShelljackPrototypeEditorTarget : TargetRules
{
    public ShelljackPrototypeEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.V6;
        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
        ExtraModuleNames.Add("ShelljackPrototype");
    }
}
