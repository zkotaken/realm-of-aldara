using UnityEditor;
using UnityEngine;

// Music streams from disk; the short combat sounds load decompressed so they start without a delay.
public class AldaraAudioImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        var ai = (AudioImporter)assetImporter; var s = ai.defaultSampleSettings;
        if (assetPath.Contains("/Resources/Music/")) { s.loadType = AudioClipLoadType.Streaming; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.6f; ai.loadInBackground = true; }
        else if (assetPath.Contains("/Resources/Sound/")) { s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f; }
        else return;
        ai.defaultSampleSettings = s;
    }
}
