using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace TarteelMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int RecordAudioRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestMicrophonePermissionIfNeeded();
    }

    private void RequestMicrophonePermissionIfNeeded()
    {
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio)
            != Permission.Granted)
        {
            ActivityCompat.RequestPermissions(
                this,
                new[] { Android.Manifest.Permission.RecordAudio },
                RecordAudioRequestCode);
        }
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RecordAudioRequestCode)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            if (!granted)
            {
                // User denied — the AudioService will surface a clear error when recording is
                // attempted. No silent crash; the service checks minBufferSize and throws.
                Android.Util.Log.Warn("TarteelMobile",
                    "RECORD_AUDIO permission denied. Microphone functionality will be unavailable.");
            }
        }
    }
}
