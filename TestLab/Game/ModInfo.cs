using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TestLab.Game;

/// <summary>
/// SpecialRoles-Rollen lesen (Fenster) und setzen (Rollen-Auswahl). Per Reflection, damit TestLab auch ohne
/// SpecialRoles läuft. Namen ↔ SpecialRoles: Game/RoleState.cs (Get, Has, Apply, Settings), Core/Roles.cs (RoleCatalog,
/// RoleId, Modifier) und Core/Protocol.cs/RoleAssigner.cs (RoleSync, Assignment).
/// </summary>
internal static class ModInfo
{
    /// <summary>Fertige SpecialRoles-Rolle für die Rollen-Auswahl.</summary>
    public sealed record ModRole(byte Id, string Name, bool Impostor);

    private static bool _resolved;
    private static MethodInfo? _roleOf;
    private static MethodInfo? _hasModifier;
    private static MethodInfo? _catalogGet;
    private static object? _flash;
    private static Type? _roleId;
    private static Type? _modifier;
    private static Type? _assignment;
    private static Type? _sync;
    private static MethodInfo? _apply;
    private static PropertyInfo? _settings;
    private static IReadOnlyList<ModRole> _roles = Array.Empty<ModRole>();

    public static bool SpecialRolesLoaded
    {
        get
        {
            Resolve();
            return _roleOf != null && _catalogGet != null;
        }
    }

    /// <summary>Ob sich Rollen setzen lassen (SpecialRoles da und Apply/RoleSync gefunden).</summary>
    public static bool CanSetRoles => SpecialRolesLoaded && _apply != null && _settings != null && _sync != null
        && _assignment != null && _roleId != null && _modifier != null && _flash != null;

    /// <summary>Fertige Rollen (nur die vergibt SpecialRoles auch selbst), leer ohne SpecialRoles.</summary>
    public static IReadOnlyList<ModRole> Roles
    {
        get
        {
            Resolve();
            return _roles;
        }
    }

    /// <summary>SpecialRoles-Rolle („Sheriff“, leer = keine) und ob der Spieler den Modifikator Flash hat.</summary>
    public static (string Role, bool Flash) SpecialRole(byte playerId)
    {
        if (!SpecialRolesLoaded) return ("", false);
        try
        {
            var role = _roleOf!.Invoke(null, new object[] { playerId });
            var info = role == null ? null : _catalogGet!.Invoke(null, new[] { role });
            var name = info?.GetType().GetProperty("Name")?.GetValue(info) as string ?? "";
            return (name, HasFlash(playerId));
        }
        catch (Exception)
        {
            return ("?", false);
        }
    }

    /// <summary>RoleId der SpecialRoles-Rolle, 0 = keine.</summary>
    public static byte SpecialRoleId(byte playerId)
    {
        if (!SpecialRolesLoaded) return 0;
        try
        {
            return Convert.ToByte(_roleOf!.Invoke(null, new object[] { playerId }));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static bool HasFlash(byte playerId)
    {
        if (!SpecialRolesLoaded || _hasModifier == null || _flash == null) return false;
        try
        {
            return _hasModifier.Invoke(null, new[] { playerId, _flash }) is true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Setzt die SpecialRoles-Rolle eines Spielers (0 = keine) und Flash, die anderen behalten ihre. Geht über
    /// RoleState.Apply wie die Rollen-Nachricht des Hosts – nur bei dir, nicht übers Netz (die Bots laufen ja hier).
    /// </summary>
    public static bool SetSpecialRole(byte playerId, byte role, bool flash)
    {
        if (!CanSetRoles) return false;
        try
        {
            var none = Enum.ToObject(_modifier!, 0);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_assignment!))!;
            foreach (var player in Players.All())
            {
                var id = player.PlayerId;
                var playerRole = id == playerId ? role : SpecialRoleId(id);
                var playerFlash = id == playerId ? flash : HasFlash(id);
                if (playerRole == 0 && !playerFlash) continue;
                list.Add(Activator.CreateInstance(_assignment!, id, Enum.ToObject(_roleId!, playerRole), playerFlash ? _flash : none));
            }
            var sync = Activator.CreateInstance(_sync!, list, _settings!.GetValue(null));
            _apply!.Invoke(null, new[] { sync });
            return true;
        }
        catch (Exception exc)
        {
            TestLabPlugin.Logger.LogWarning($"SpecialRoles-Rolle setzen ging nicht: {(exc as TargetInvocationException)?.InnerException ?? exc}");
            return false;
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
        _modifier = assembly.GetType("SpecialRoles.Core.Modifier");
        _roleId = assembly.GetType("SpecialRoles.Core.RoleId");
        _assignment = assembly.GetType("SpecialRoles.Core.Assignment");
        _sync = assembly.GetType("SpecialRoles.Core.RoleSync");
        if (state == null || catalog == null)
        {
            TestLabPlugin.Logger.LogWarning("SpecialRoles gefunden, aber RoleState/RoleCatalog fehlen (umbenannt?).");
            return;
        }
        _roleOf = AccessTools.Method(state, "Get", new[] { typeof(byte) });
        _catalogGet = AccessTools.Method(catalog, "Get");
        if (_modifier != null && Enum.IsDefined(_modifier, "Flash"))
        {
            _hasModifier = AccessTools.Method(state, "Has", new[] { typeof(byte), _modifier });
            _flash = Enum.Parse(_modifier, "Flash");
        }
        if (_sync != null) _apply = AccessTools.Method(state, "Apply", new[] { _sync });
        _settings = AccessTools.Property(state, "Settings");
        _roles = ReadRoles(catalog);
        TestLabPlugin.Logger.LogInfo(_roleOf == null || _catalogGet == null
            ? "SpecialRoles gefunden, aber die Rollen lassen sich nicht lesen (Namen geändert?)."
            : CanSetRoles
                ? $"SpecialRoles gefunden: Rollen werden im Fenster angezeigt, {_roles.Count} lassen sich setzen."
                : "SpecialRoles gefunden: Rollen werden angezeigt, setzen geht nicht (RoleState.Apply/RoleSync geändert?).");
    }

    private static IReadOnlyList<ModRole> ReadRoles(Type catalog)
    {
        var roles = new List<ModRole>();
        try
        {
            if (AccessTools.Field(catalog, "All")?.GetValue(null) is not IEnumerable all) return roles;
            foreach (var info in all)
            {
                var type = info.GetType();
                if (type.GetProperty("Implemented")?.GetValue(info) is not true) continue;
                var id = Convert.ToByte(type.GetProperty("Id")?.GetValue(info));
                var name = type.GetProperty("Name")?.GetValue(info) as string ?? id.ToString();
                var impostor = type.GetProperty("Team")?.GetValue(info)?.ToString() == "Impostor";
                roles.Add(new ModRole(id, name, impostor));
            }
        }
        catch (Exception exc)
        {
            TestLabPlugin.Logger.LogWarning($"SpecialRoles-Rollenliste nicht lesbar: {exc.Message}");
        }
        return roles;
    }
}
