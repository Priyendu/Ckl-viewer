using System.IO;
using CklViewer.Models;

namespace CklViewer.Parsing;

/// <summary>Loads a checklist from disk, detecting CKL (XML) vs CKLB (JSON) by extension and content.</summary>
public static class ChecklistLoader
{
    public static ChecklistDocument Load(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".cklb" || extension == ".json")
        {
            return CklbParser.ParseFile(path);
        }

        if (extension == ".ckl" || extension == ".xml")
        {
            return CklParser.ParseFile(path);
        }

        // Unknown extension: sniff the first non-whitespace character.
        using (var reader = new StreamReader(path))
        {
            int ch;
            while ((ch = reader.Read()) != -1 && char.IsWhiteSpace((char)ch))
            {
            }

            if (ch == '{')
            {
                return CklbParser.ParseFile(path);
            }
        }

        return CklParser.ParseFile(path);
    }
}
