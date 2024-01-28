using Unity.Networking.Transport;
using Unity.Networking.Transport.TLS;
using UnityEngine;

public class SecureConnectionBehaviour : MonoBehaviour
{
    [Multiline] public string clientCa;
    public string serverCommonName;

    [Multiline] public string serverCert;
    [Multiline] public string serverPrivate;


    public bool ShouldApply()
    {
        if (LaunchArgUtility.TryGetArg("-secureConnection", out var str))
        {
            return bool.Parse(str);
        }

        return true;
    }

    public void ApplyClient(ref NetworkSettings settings)
    {
        if (!ShouldApply())
        {
            return;
        }

        settings.WithSecureClientParameters(clientCa, serverCommonName);
    }

    public void ApplyServer(ref NetworkSettings settings)
    {
        if (!ShouldApply())
        {
            return;
        }

        settings.WithSecureServerParameters(serverCert, serverPrivate);
    }
}