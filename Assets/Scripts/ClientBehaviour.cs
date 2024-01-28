using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;
using UnityEngine;

[BurstCompile]
public class ClientBehaviour : MonoBehaviour
{
    public int connectionsCount;

    public int randomDataSendInterval;
    private NativeList<ConnectionHandle> _clients;

    private void OnEnable()
    {
        if (LaunchArgUtility.TryGetArg("-connectionCount", out var capString))
        {
            connectionsCount = int.Parse(capString);
        }

        if (LaunchArgUtility.TryGetArg("-sendInterval", out capString))
        {
            randomDataSendInterval = int.Parse(capString);
        }

        var settings = new NetworkSettings(Allocator.Temp);

        if (TryGetComponent(out SecureConnectionBehaviour secure))
        {
            secure.ApplyClient(ref settings);
        }

        settings.WithNetworkConfigParameters(receiveQueueCapacity: 16, sendQueueCapacity: 16);

        ushort port = 9000;
        if (LaunchArgUtility.TryGetArg("-port", out var portString))
        {
            port = ushort.Parse(portString);
        }

        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);
        if (LaunchArgUtility.TryGetArg("-ip", out var ipString))
        {
            endpoint = NetworkEndpoint.Parse(ipString, port).WithPort(port);
        }

        _clients = new(connectionsCount, Allocator.Persistent);
        for (var i = 0; i < connectionsCount; i++)
        {
            var driver = NetworkDriver.Create(settings);

            var connectionHandle = new ConnectionHandle
            {
                driver = driver,
                connection = driver.Connect(endpoint)
            };
            _clients.Add(connectionHandle);
        }
    }


    private void OnDisable()
    {
        _prevHandle.Complete();
        OnDisableBurst(ref _clients);
    }

    [BurstCompile]
    private static void OnDisableBurst(ref NativeList<ConnectionHandle> clients)
    {
        foreach (var connectionHandle in clients)
        {
            connectionHandle.driver.Dispose();
        }

        clients.Dispose();
    }

    private int _tick;
    private JobHandle _prevHandle;

    private void FixedUpdate()
    {
        _tick++;
        Schedule(ref _clients, ref _prevHandle, ref _tick, ref randomDataSendInterval);
    }

    [BurstCompile]
    private static void Schedule(ref NativeList<ConnectionHandle> clients, ref JobHandle handle, ref int tick,
        ref int interval)
    {
        handle.Complete();

        for (int i = 0; i < clients.Length; i++)
        {
            var connectionHandle = clients[i];
            var jobDep = connectionHandle.driver.ScheduleUpdate();

            jobDep = new RandomDataJob
            {
                handle = connectionHandle,
                interval = interval,
                currentTick = tick,
                index = i
            }.Schedule(jobDep);

            handle = JobHandle.CombineDependencies(jobDep, handle);
        }

        JobHandle.ScheduleBatchedJobs();
    }


    [BurstCompile]
    private struct RandomDataJob : IJob
    {
        public ConnectionHandle handle;

        public int interval;
        public int currentTick;

        public int index;

        public void Execute()
        {
            var driver = handle.driver;
            NetworkEvent.Type state;
            while ((state = handle.driver.PopEvent(out _, out _)) != NetworkEvent.Type.Empty) { }

            if (driver.GetConnectionState(handle.connection) is NetworkConnection.State.Connected)
            {
                if ((currentTick + index) % interval == 0)
                {
                    var bytes = handle.driver.BeginSend(handle.connection, out var writer);
                    if (bytes < 0)
                    {
                        var statusCode = (StatusCode)bytes;
                        Debug.LogError($"Couldn't send packet on client: {statusCode}");
                        return;
                    }

                    writer.WriteInt(123);
                    writer.WriteInt(63464);
                    writer.WriteInt(12412);
                    writer.WriteInt(56756);

                    handle.driver.EndSend(writer);
                }
            }
        }
    }
}