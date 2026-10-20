using UnityEngine;

// Vibración del celular con duración personalizada.
// Uso desde cualquier script:   Haptics.Vibrate(200);   // 200 milisegundos
// No hace falta ponerlo en ningún objeto de la escena.
public static class Haptics
{
    // Para poder apagarla desde un botón de opciones si quieres
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt("vibracion", 1) == 1;
        set => PlayerPrefs.SetInt("vibracion", value ? 1 : 0);
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static int sdk;
    private static bool initialized;

    private static void Init()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                sdk = version.GetStatic<int>("SDK_INT");
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Haptics: no se pudo iniciar el vibrador: " + e.Message);
        }
    }
#endif

    // milliseconds: duración. amplitude: 1-255 (fuerza), -1 = la normal del celular
    public static void Vibrate(long milliseconds = 150, int amplitude = -1)
    {
        if (!Enabled) return;

#if UNITY_EDITOR
        Debug.Log("[Haptics] Vibra " + milliseconds + " ms");
#elif UNITY_ANDROID
        Init();
        try
        {
            if (vibrator != null)
            {
                if (sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
                return;
            }
        }
        catch (System.Exception) { }

        // Respaldo. Además hace que Unity agregue solo el permiso VIBRATE al APK
        Handheld.Vibrate();
#else
        Handheld.Vibrate();
#endif
    }
}