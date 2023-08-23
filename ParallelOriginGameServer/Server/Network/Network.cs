using LiteNetLib;
using LiteNetLib.Utils;
using ParallelOriginGameServer.Server.Persistence;

namespace ParallelOriginGameServer.Server.Network;

public delegate void OnConnectionRequest(ConnectionRequest connectionRequest);

public delegate void OnDisconnected(NetPeer peer, DisconnectInfo info);

public delegate void OnReceive(NetPeer peer, NetDataReader reader);

public delegate void OnRegister(NetPeer peer, Account account);

public delegate void OnLogin(NetPeer peer, Account account);

public delegate void OnLogout(NetPeer peer, Account account);

/// <summary>
///     The Server-Network which extends <see cref="Network" /> and adds additional functionalities.
/// </summary>
public class ServerNetwork : ParallelOrigin.Core.Network.Network
{
    private const ushort MaxConnections = 10;

    /// <summary>
    ///     Gets invoked once a connection request came in
    /// </summary>
    public OnConnectionRequest OnConnectionRequest { get; set; }

    /// <summary>
    ///     Gets invoked once a user connection is being disconnected
    /// </summary>
    public OnDisconnected OnDisconnected { get; set; }

    /// <summary>
    ///     Gets invoked once a packet was received.
    /// </summary>
    public OnReceive OnReceive { get; set; }

    /// <summary>
    ///     An delegate being invoked once a user was registered sucessfully.
    /// </summary>
    public OnRegister OnRegister { get; set; }

    /// <summary>
    ///     An delegate being invoked once a user was loged in sucessfully
    /// </summary>
    public OnLogin OnLogin { get; set; }

    /// <summary>
    ///     An delegate being invoked once a user was loged out sucessfully.
    /// </summary>
    public OnLogout OnLogout { get; set; }

    protected override void Setup()
    {
        base.Setup();

        // Setting the delegates, otherhwise invoking them causes null pointer exceptions
        OnConnectionRequest = request => { };
        OnDisconnected = (peer, info) => { };
        OnReceive = (peer, reader) => { };
        OnRegister = (peer, account) => { };
        OnLogin = (peer, account) => { };
        OnLogout = (peer, account) => { };

        OnConnectionRequest += ApproveConnection;

        Listener.ConnectionRequestEvent += request => OnConnectionRequest(request);
        Listener.PeerDisconnectedEvent += (peer, info) => OnDisconnected(peer, info);
        Listener.NetworkReceiveEvent += (peer, reader, method) => OnReceive(peer, reader);
    }

    /// <summary>
    ///     Approves an incoming connection if the <see cref="MaxConnections" /> wasnt reached.
    ///     Otherwhise it will reject them.
    /// </summary>
    /// <param name="request"></param>
    private void ApproveConnection(ConnectionRequest request)
    {
        if (Manager.ConnectedPeersCount < MaxConnections)
            request.Accept();
        else
            request.Reject();
    }
}