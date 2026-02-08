using UnityEngine;
using UnityEngine.InputSystem.HID;

namespace Dawn.Editor.Extensions;

public static class TransformExtensions
{
    public static bool FindRecursive(this Transform parent, string n, out Transform result)
    {
        if (parent.name == n)
        {
            result = parent;
            return true;
        }


        foreach (Transform child in parent)
            if (child.FindRecursive(n, out result)) return true;

        result = null!;
        return false;
    }

    public static Transform FindPathRecursive(this Transform parent, string n, out string path)
    {
        if (parent.name == n)
        {
            path = n;
            return parent;
        }


        foreach (Transform child in parent)
            if (child.FindPathRecursive(n, out string childPath)) //kinda not feeling comfortable about creating new strings in foreach every time
            {
                path = parent.name + "/" + childPath;
                return child;
            }

        path = "";
        return null!;
    }
}
