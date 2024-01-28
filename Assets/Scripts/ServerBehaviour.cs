using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport;
using UnityEngine;

[BurstCompile]
public class ServerBehaviour : MonoBehaviour
{
    private NetworkDriver _driver;

    private void OnEnable()
    {
        var receiveQueueCapacity = 1024 * 50;
        var sendQueueCapacity = 1024 * 25;

        if (LaunchArgUtility.TryGetArg("-receiveCapacity", out var capString))
        {
            receiveQueueCapacity = int.Parse(capString);
        }

        if (LaunchArgUtility.TryGetArg("-sendCapacity", out capString))
        {
            sendQueueCapacity = int.Parse(capString);
        }

        ushort port = 9000;
        if (LaunchArgUtility.TryGetArg("-port", out var portString))
        {
            port = ushort.Parse(portString);
        }


        var settings = new NetworkSettings(Allocator.Temp);

        if (TryGetComponent(out SecureConnectionBehaviour secure))
        {
            secure.ApplyServer(ref settings);
        }

        settings.WithNetworkConfigParameters(receiveQueueCapacity: receiveQueueCapacity,
            sendQueueCapacity: sendQueueCapacity);
        _driver = NetworkDriver.Create(settings);


        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(port);
        _driver.Bind(endpoint);
        _driver.Listen();
    }

    private void OnDisable()
    {
        _prevHandle.Complete();
        _driver.Dispose();
    }


    private int _tick;
    private JobHandle _prevHandle;

    private void FixedUpdate()
    {
        _tick++;
        _prevHandle.Complete();
        _prevHandle = _driver.ScheduleUpdate();

        _prevHandle = new UpdateConnectionsJob
        {
            driver = _driver
        }.Schedule(_prevHandle);
        JobHandle.ScheduleBatchedJobs();
    }

    [BurstCompile]
    private struct UpdateConnectionsJob : IJob
    {
        public NetworkDriver driver;

        public void Execute()
        {
            NetworkConnection acceptedConn;
            while ((acceptedConn = driver.Accept()) != default)
            {
                Debug.Log($"Accepted {acceptedConn.ToFixedString()}");
            }

            NetworkEvent.Type data;
            while ((data = driver.PopEvent(out var conn, out var stream, out _)) != NetworkEvent.Type.Empty)
            {
                if (data is NetworkEvent.Type.Disconnect)
                {
                    Debug.Log($"{conn.ToFixedString()} disconnected.");
                }
                else if (data is NetworkEvent.Type.Data)
                {
                    driver.BeginSend(conn, out var writer, stream.Length);
                    var arr = new NativeArray<byte>(stream.Length, Allocator.Temp);
                    writer.WriteBytes(arr);
                    driver.EndSend(writer);
                }
            }
        }
    }
}

public struct ConnectionHandle
{
    [NativeDisableContainerSafetyRestriction]
    public NetworkDriver driver;

    public NetworkConnection connection;
}