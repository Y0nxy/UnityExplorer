using UnityEngine.EventSystems;
using UniverseLib.Input;

#nullable enable

namespace UnityExplorer.UI;

/// <summary>
/// UniverseLib builds the InputActionAsset / InputActionReference objects behind its UI input module at runtime
/// (they are plain ScriptableObjects). When the game loads a new scene, Unity unloads every asset nothing in the
/// scene references, which includes these, so the UI stops receiving clicks and re-showing the UI does not help.
/// Flagging them with <see cref="HideFlags.DontUnloadUnusedAsset"/> keeps them alive across scene loads.
/// </summary>
internal static class InputAssetProtector
{
    private static readonly string[] actionProperties =
    {
        "actionsAsset", "point", "leftClick", "middleClick", "rightClick", "scrollWheel", "move", "submit", "cancel"
    };

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

    internal static void Protect()
    {
        try
        {
            if (InputManager.CurrentType != InputType.InputSystem)
                return;

            BaseInputModule? module = EventSystemHelper.UIInput;
            Type? moduleType = ReflectionUtility.GetTypeByName("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (module == null || moduleType == null)
                return;

            object? typedModule = module.TryCast(moduleType);
            if (typedModule == null)
                return;

            int protectedCount = 0;
            foreach (string propertyName in actionProperties)
            {
                object? value = moduleType.GetProperty(propertyName, Flags)?.GetValue(typedModule, null);
                protectedCount += ProtectObject(value);

                // InputActionReference -> also keep the asset it points to alive.
                if (value != null)
                {
                    Type? refType = ReflectionUtility.GetTypeByName("UnityEngine.InputSystem.InputActionReference");
                    if (refType != null)
                    {
                        object? typedRef = value.TryCast(refType);
                        object? asset = refType.GetProperty("asset", Flags)?.GetValue(typedRef, null);
                        protectedCount += ProtectObject(asset);
                    }
                }
            }

            ExplorerCore.Log($"Protected {protectedCount} input object(s) from being unloaded on scene change.");
        }
        catch (Exception ex)
        {
            ExplorerCore.LogWarning($"Unable to protect the UI input assets from scene unloads: {ex.Message}");
        }
    }

    private static int ProtectObject(object? value)
    {
        if (value is UnityEngine.Object obj && obj)
        {
            obj.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            return 1;
        }
        return 0;
    }
}
