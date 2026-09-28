namespace ProjetSKE.Core.Data;

/// <summary>Lit la taille d'une image (PNG, JPEG, GIF, WebP) à partir de ses premiers octets, sans la décoder.</summary>
public static class ImageSize
{
    public static (int Width, int Height)? Read(byte[] b)
    {
        try
        {
            // PNG : signature, puis bloc IHDR (largeur et hauteur sur 4 octets, gros-boutiste).
            if (b.Length >= 24 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
                return (BigEndian(b, 16), BigEndian(b, 20));

            // GIF : largeur et hauteur sur 2 octets, petit-boutiste.
            if (b.Length >= 10 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F')
                return (b[6] | b[7] << 8, b[8] | b[9] << 8);

            // WebP : conteneur RIFF avec un bloc VP8, VP8L ou VP8X.
            if (b.Length >= 30 && b[0] == 'R' && b[1] == 'I' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            {
                var chunk = System.Text.Encoding.ASCII.GetString(b, 12, 4);
                if (chunk == "VP8 ") return ((b[26] | b[27] << 8) & 0x3FFF, (b[28] | b[29] << 8) & 0x3FFF);
                if (chunk == "VP8L")
                {
                    var bits = b[21] | b[22] << 8 | b[23] << 16 | b[24] << 24;
                    return ((bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
                }
                if (chunk == "VP8X") return ((b[24] | b[25] << 8 | b[26] << 16) + 1, (b[27] | b[28] << 8 | b[29] << 16) + 1);
            }

            // JPEG : parcours des segments jusqu'à un marqueur SOF (début d'image).
            if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                var i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var marker = b[i + 1];
                    if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                        return (b[i + 7] << 8 | b[i + 8], b[i + 5] << 8 | b[i + 6]);
                    var length = b[i + 2] << 8 | b[i + 3];
                    i += 2 + length;
                }
            }
        }
        catch (IndexOutOfRangeException) { }
        return null;
    }

    private static int BigEndian(byte[] b, int i) => b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3];
}
