using System;
using System.IO;

// Kept independent of Unity so editor/virtual-player/build paths can be regression-tested.
public static class ExternalContentPath
{
    public static string Resolve(string dataPath, bool isEditor)
    {
        var project = Directory.GetParent(Path.GetFullPath(dataPath));
        if (project == null) throw new ArgumentException("Invalid application data path.", nameof(dataPath));

        // Multiplayer Play Mode 2.x virtual projects live at <project>/Library/VP/<id>.
        // Use the original external content, not a copied (and potentially stale) virtual copy.
        if (isEditor && string.Equals(project.Parent?.Name, "VP", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(project.Parent?.Parent?.Name, "Library", StringComparison.OrdinalIgnoreCase))
        {
            var original = project.Parent.Parent.Parent;
            if (original != null && Directory.Exists(Path.Combine(original.FullName, "Assets")) &&
                Directory.Exists(Path.Combine(original.FullName, "ProjectSettings")))
                return Path.Combine(original.FullName, "Content");
        }

        // Normal Editor: <project>/Content. Windows/Linux player: <exe directory>/Content.
        return Path.Combine(project.FullName, "Content");
    }
}
