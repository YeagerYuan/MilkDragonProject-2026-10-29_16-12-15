using UnrealBuildTool;
public class ShelljackPrototypeTarget : TargetRules
{
    public ShelljackPrototypeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Game;
        DefaultBuildSettings = BuildSettingsVersion.V6;
        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_7;
        ExtraModuleNames.Add("ShelljackPrototype");
    }
}
