using Microsoft.Maui.Layouts;
using ProjetSKE.Core.Models;

namespace ProjetSKE.App.Ui;

/// <summary>
/// Image de la banque (chargée depuis son lien) cadrée dans son conteneur selon le point central et le zoom
/// de la fiche. Le même cadrage marche pour un cadre portrait, carré ou large : l'image couvre toujours le cadre,
/// et le point choisi est placé le plus près possible du centre.
/// </summary>
public sealed class FramedImage : ContentView
{
    private readonly AbsoluteLayout _canvas = new() { IsClippedToBounds = true };
    private readonly Image _image;
    private PortraitDef _portrait;

    public FramedImage(PortraitDef portrait)
    {
        _portrait = portrait;
        _image = new Image { Aspect = Aspect.Fill, Source = Source(portrait.Url) };
        _canvas.Add(_image);
        Content = _canvas;
        IsClippedToBounds = true;
        SizeChanged += (_, _) => Layout();
    }

    /// <summary>Change le cadrage (ou l'image) et redessine : sert à l'aperçu de l'éditeur.</summary>
    public void Update(PortraitDef portrait)
    {
        if (portrait.Url != _portrait.Url) _image.Source = Source(portrait.Url);
        _portrait = portrait;
        Layout();
    }

    private static ImageSource? Source(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new UriImageSource { Uri = uri, CachingEnabled = true, CacheValidity = TimeSpan.FromDays(30) }
            : null;

    /// <summary>Taille et position de l'image dans un cadre, pour une fiche donnée.</summary>
    public static Rect Placement(PortraitDef p, double frameW, double frameH)
    {
        var aspect = p.Aspect > 0 ? p.Aspect : frameW / Math.Max(1, frameH);
        // Taille « couvrante » : l'image remplit tout le cadre, puis on applique le zoom.
        var w = Math.Max(frameW, frameH * aspect);
        var h = w / aspect;
        var zoom = Math.Clamp(p.Zoom, 1, 6);
        w *= zoom;
        h *= zoom;
        var x = Math.Clamp(frameW / 2 - p.FocusX * w, frameW - w, 0);
        var y = Math.Clamp(frameH / 2 - p.FocusY * h, frameH - h, 0);
        return new Rect(x, y, w, h);
    }

    private void Layout()
    {
        if (Width <= 0 || Height <= 0) return;
        AbsoluteLayout.SetLayoutBounds(_image, Placement(_portrait, Width, Height));
    }
}
