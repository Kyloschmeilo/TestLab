using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TestLab.Game;
using TestLab.Ui;

namespace TestLab;

/// <summary>
/// Testlabor für Among-Us-Mods: füllt eine lokale Lobby mit Bots, lässt dich jeden Spieler steuern und die Bots in
/// Meetings abstimmen – so lassen sich Mods wie SpecialRoles allein testen. Nur in lokalen Spielen als Host aktiv,
/// online ändert die Mod nichts. Bedienung über ein Fenster (F1).
/// </summary>
[BepInAutoPlugin("de.spielenetzwerk.testlab")]
[BepInProcess("Among Us.exe")]
[BepInDependency(CompatGuard.CrewStatsId, BepInDependency.DependencyFlags.SoftDependency)]
public partial class TestLabPlugin : BasePlugin
{
    internal static ManualLogSource Logger { get; private set; } = null!;

    public Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        Logger = Log;
        Settings.Bind(Config);
        Harmony.PatchAll();
        CompatGuard.Apply(Harmony);
        AddComponent<TestLabWindow>();
        Log.LogInfo($"TestLab {Version} geladen.");
    }
}
