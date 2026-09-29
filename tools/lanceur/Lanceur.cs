// Lanceur du jeu sur PC : « Projet SKE.exe », seul fichier à côté du dossier « fichiers » (le jeu et ses centaines
// de fichiers). Compilé par GitHub Actions avec le compilateur C# du .NET Framework intégré à Windows
// (pas de dépendance : quelques Ko). Il démarre fichiers\ProjetSKE.App.exe et se ferme.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class Lanceur
{
    [STAThread]
    private static int Main(string[] args)
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        var exe = Path.Combine(Path.Combine(dir, "fichiers"), "ProjetSKE.App.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show("Le dossier « fichiers » est introuvable à côté de « Projet SKE.exe ».\n"
                + "Décompresse à nouveau ProjetSKE-Windows.zip (sans déplacer « Projet SKE.exe » tout seul).",
                "Projet SKE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }
        var start = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe),
            UseShellExecute = false,
            Arguments = string.Join(" ", args.Select(a => "\"" + a.Replace("\"", "\\\"") + "\"").ToArray()),
        };
        Process.Start(start);
        return 0;
    }
}
