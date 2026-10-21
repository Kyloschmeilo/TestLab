using System;
using System.Reflection;
using HarmonyLib;

namespace TestLab.Game;

/// <summary>
/// Zeigt im Fenster die SpecialRoles-Rolle jedes Spielers (der Host kennt alle). Liest per Reflection, damit TestLab
/// auch ohne SpecialRoles läuft. Namen ↔ SpecialRoles: Game/RoleState.cs (Get, Has) und Core/Roles.cs (RoleCatalog).
/// </summary>
internal static class ModInfo
{
    private static bool _resolved;
    private static MethodInfo? _roleOf;
    private static MethodInfo? _hasModifier;
    private static MethodInfo? _catalogGet;
    private static object? _flash;

    public static bool SpecialRolesLoaded
    {
        get
        {
            Resolve();
            return _roleOf != null && _catalogGet != null;
        }
    }

    /// <summary>„Sheriff“, „Sheriff + Flash“ oder leer.</summary>
    public static string SpecialRole(byte playerId)
    {
        if (!SpecialRolesLoaded) return "";
        try
        {
            var role = _roleOf!.Invoke(null, new object[] { playerId });
            var info = role == null ? null : _catalogGet!.Invoke(null, new[] { role });
            var name = info?.GetType().GetProperty("Name")?.GetValue(info) as string ?? "";
            if (_hasModifier != null && _flash != null && _hasModifier.Invoke(null, new[] { playerId, _flash }) is true)
                name = name.Length == 0 ? "Flash" : name + " + Flash";
            return name;
        }
        catch (Exception)
        {
            return "?";
        }
    }

    private static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;
        var assembly = CompatGuard.FindAssembly("SpecialRoles");
        if (assembly == null) return;
        var state = assembly.GetType("SpecialRoles.Game.RoleState");
        var catalog = assembly.GetType("SpecialRoles.Core.RoleCatalog");
        var modifier = assembly.GetType("SpecialRoles.Core.Modifier");
        if (state == null || catalog == null)
        {
            TestLabPlugin.Logger.LogWarning("SpecialRoles gefunden, aber RoleState/RoleCatalog fehlen (umbenannt?).");
            return;
        }
        _roleOf = AccessTools.Method(state, "Get", new[] { typeof(byte) });
        _catalogGet = AccessTools.Method(catalog, "Get");
        if (modifier != null && Enum.IsDefined(modifier, "Flash"))
        {
            _hasModifier = AccessTools.Method(state, "Has", new[] { typeof(byte), modifier });
            _flash = Enum.Parse(modifier, "Flash");
        }
        TestLabPlugin.Logger.LogInfo(_roleOf != null && _catalogGet != null
            ? "SpecialRoles gefunden: Rollen werden im Fenster angezeigt."
            : "SpecialRoles gefunden, aber die Rollen lassen sich nicht lesen (Namen geändert?).");
    }
}
