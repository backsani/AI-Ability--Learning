using System.Text;

namespace NextorialTrainer.Common;

public static class Utf8
{
    /// <summary>
    /// UTF-8 without a byte order mark. Encoding.UTF8 emits one, which makes the files
    /// this app writes unreadable to strict JSON parsers and shows as a stray character
    /// in some markdown viewers.
    /// </summary>
    public static readonly UTF8Encoding NoBom = new(encoderShouldEmitUTF8Identifier: false);
}
